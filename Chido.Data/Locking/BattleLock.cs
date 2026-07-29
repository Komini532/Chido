using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Chido.Data.Locking;

/// <summary>
/// 正準ロック順序を強制するトランザクションスコープ（戦闘システム 7.2）。
///
/// <code>
/// 1. プレイヤー行（chido_player.user_id／行動者本人）
/// 2. チャンネル行（chido_channel_state.channel_id）
/// 3. セッション行（chido_battle_session.session_id）
/// </code>
///
/// <b>上位の段を飛ばすことは順序違反ではない。</b>装備変更は ① → ③ で②を飛ばす。
/// 逆行（一度②を取った後に①を取る）だけがデッドロックを生むため、そこだけを弾く。
///
/// | 経路 | 取得する行 |
/// |---|---|
/// | 戦闘行動 | ① → ② → ③ |
/// | セッション新規生成を伴う戦闘行動 | ① → ②（この配下で③を INSERT） |
/// | 装備変更 | ① → ③ |
/// | 次の敵の抽選・出現 | 既に②を保持しているため追加取得なし |
///
/// <b>ロック取得は必ず本型を経由すること。</b>各リポジトリが自前で <c>FOR UPDATE</c> を
/// 書けるようにすると、順序の規約が全経路に散らばり、違反が実行時のデッドロックとしてしか
/// 現れなくなる。順序の知識を本型の1箇所に閉じ込め、違反はその場で例外にしている。
///
/// <b>アンカー行は事前に存在している必要がある。</b>行の作成（<c>PlayerRepository.EnsureAsync</c> /
/// <c>ChannelStateRepository.EnsureAsync</c>）は本スコープの<b>外側</b>で行う。
/// スコープ内で <c>INSERT ... ON DUPLICATE</c> を打つと、既存行に対して共有ロックが乗り、
/// 同一行を狙う2トランザクションが S ロックを持ち合ったまま X への昇格を待ち合うデッドロックになる。
///
/// <b><c>FOR UPDATE</c> の生SQLに LINQ を重ねてはならない。</b>
/// <c>FromSql...().FirstOrDefaultAsync()</c> のように演算子を足すと、EF Core は生SQLを
/// 派生テーブルに包んで <c>SELECT ... FROM (元のSQL) AS t LIMIT 1</c> を組み立てる。
/// ロック句が副問い合わせの内側に入ってしまい、排他が静かに失われる。
/// いずれも主キー等価の取得であり0行か1行しか返らないため、<c>ToListAsync</c> で
/// 生SQLをそのまま実行し、先頭要素の取り出しはC#側で行っている。
/// </summary>
public sealed class BattleLock : IAsyncDisposable
{
    private readonly ChidoDbContext db;
    private readonly IDbContextTransaction transaction;

    // 取得済みの最上位アンカー。逆行の検出にのみ使う
    private LockAnchor? highestAcquired;

    // 同一段の再取得を冪等にするための記録（同じ行なら何度呼んでも無害にする）
    private ulong? lockedUserId;
    private ulong? lockedChannelId;
    private Guid? lockedSessionId;

    private bool committed;

    private BattleLock(ChidoDbContext db, IDbContextTransaction transaction)
    {
        this.db = db;
        this.transaction = transaction;
    }

    /// <summary>ロックスコープを開始する。コミットせずに破棄するとロールバックされる。</summary>
    public static async Task<BattleLock> BeginAsync(
        ChidoDbContext db, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        return new BattleLock(db, transaction);
    }

    /// <summary>取得済みの最上位アンカー（テストと診断用）。</summary>
    public LockAnchor? HighestAcquired => highestAcquired;

    /// <summary>
    /// ① プレイヤー行をロックする。行が無ければ例外
    /// （<c>PlayerRepository.EnsureAsync</c> の呼び忘れを黙って握り潰さない）。
    /// </summary>
    public async Task<PlayerRecord> LockPlayerAsync(
        ulong userId, CancellationToken cancellationToken = default)
    {
        if (lockedUserId == userId) return await FindPlayerAsync(userId, cancellationToken);

        // 別のプレイヤー行を同一スコープで取ると、2人のプレイヤーを逆順に取る経路同士で
        // デッドロックしうる。現行の設計にその経路は無いため、混入したら止める
        if (lockedUserId is { } already)
        {
            throw new InvalidOperationException(
                $"1つのロックスコープで複数のプレイヤー行は取得できない（取得済み: {already} / 要求: {userId}）。" +
                "他プレイヤーの状態への書き込みはチャンネル行②に包摂される。");
        }

        EnterAnchor(LockAnchor.Player);

        var record = (await db.Players
            .FromSqlInterpolated($"SELECT * FROM chido_player WHERE user_id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"ロックアンカーとなる chido_player の行が存在しない（user_id = {userId}）。" +
                "ロックスコープに入る前に PlayerRepository.EnsureAsync を呼ぶこと。");

        lockedUserId = userId;
        return record;
    }

    /// <summary>
    /// ② チャンネル行をロックする。<b>全戦闘行動の直列化点</b>であり、これを保持している間は
    /// 同一チャンネルの戦闘行動が完全に直列化される。
    /// </summary>
    public async Task<ChannelStateRecord> LockChannelAsync(
        ulong channelId, CancellationToken cancellationToken = default)
    {
        if (lockedChannelId == channelId) return await FindChannelAsync(channelId, cancellationToken);

        if (lockedChannelId is { } already)
        {
            throw new InvalidOperationException(
                $"1つのロックスコープで複数のチャンネル行は取得できない（取得済み: {already} / 要求: {channelId}）。");
        }

        EnterAnchor(LockAnchor.Channel);

        var record = (await db.ChannelStates
            .FromSqlInterpolated(
                $"SELECT * FROM chido_channel_state WHERE channel_id = {channelId} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"ロックアンカーとなる chido_channel_state の行が存在しない（channel_id = {channelId}）。" +
                "戦闘チャンネルとして初期化されていないか、ChannelStateRepository.EnsureAsync の呼び忘れ。");

        lockedChannelId = channelId;
        return record;
    }

    /// <summary>
    /// ③ セッション行をロックする。行が無ければ null を返す
    /// （セッションはプレイヤーの最初の戦闘行為時に生成されるため、存在しないことが正常系にある）。
    ///
    /// <b>チャンネル行②を保持している場合、戦闘行動でのセッション行の取得は冗長である。</b>
    /// ②が「1チャンネルにアクティブなセッションは1つ以下」を保証しているため。
    /// 必要なのは②を飛ばす経路（装備変更）との排他のみ。
    /// </summary>
    public async Task<BattleSessionRecord?> LockSessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (lockedSessionId == sessionId) return await FindSessionAsync(sessionId, cancellationToken);

        if (lockedSessionId is { } already)
        {
            throw new InvalidOperationException(
                $"1つのロックスコープで複数のセッション行は取得できない（取得済み: {already} / 要求: {sessionId}）。");
        }

        EnterAnchor(LockAnchor.Session);

        var record = (await db.BattleSessions
            .FromSqlInterpolated(
                $"SELECT * FROM chido_battle_session WHERE session_id = {sessionId} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        lockedSessionId = sessionId;
        return record;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        committed = true;
    }

    /// <summary>
    /// コミットされていなければロールバックする。
    /// 抽選のマスタ不整合などで例外が飛んだ場合、そのターン全体が巻き戻り、
    /// 不整合なチャンネル状態が残らない（戦闘システム 10.3）。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!committed) await transaction.RollbackAsync();

        await transaction.DisposeAsync();
    }

    /// <summary>
    /// 正準ロック順序の逆行を弾く。上位の段を飛ばすのは順序違反ではないため、
    /// 「取得済みの最上位より下のアンカーを新たに取ること」だけを禁じる。
    /// </summary>
    private void EnterAnchor(LockAnchor anchor)
    {
        if (highestAcquired is { } highest && anchor < highest)
        {
            throw new InvalidOperationException(
                $"正準ロック順序違反: {highest} を取得済みの状態で {anchor} を取得しようとした。" +
                "取得順序は プレイヤー行 → チャンネル行 → セッション行 に固定されている（段の飛ばしは可）。");
        }

        highestAcquired = anchor;
    }

    private async Task<PlayerRecord> FindPlayerAsync(ulong userId, CancellationToken cancellationToken)
        => await db.Players.FirstAsync(x => x.UserId == userId, cancellationToken);

    private async Task<ChannelStateRecord> FindChannelAsync(ulong channelId, CancellationToken cancellationToken)
        => await db.ChannelStates.FirstAsync(x => x.ChannelId == channelId, cancellationToken);

    private async Task<BattleSessionRecord?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => await db.BattleSessions.FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
}
