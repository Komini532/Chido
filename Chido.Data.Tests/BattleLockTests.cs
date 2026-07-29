using Chido.Data.Entities;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 正準ロック順序と直列化の検証（戦闘システム 7.1・7.2）。
///
/// <para>
/// 排他が実際に効いていることは、実DBでしか確認できない。ロック待ちは
/// <c>innodb_lock_wait_timeout</c> を短く設定した2本目の接続がタイムアウトすることで
/// 決定的に観測する（スリープによる待ち合わせでは、待たなかったのか待って通ったのかを区別できない）。
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BattleLockTests(DatabaseFixture fixture)
{
    /// <summary>MySQL のロック待ちタイムアウト（ER_LOCK_WAIT_TIMEOUT）。</summary>
    private const int LockWaitTimeoutError = 1205;

    // --- 正準ロック順序 ---

    [DatabaseFact]
    public async Task 逆行するロック取得は拒否される()
    {
        // 上位の段を飛ばすのは順序違反ではないが、逆行はデッドロックを生む。
        // 違反が実行時のデッドロックとしてしか現れない状態を避けるため、その場で例外にする
        var ids = NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedAsync(db, ids);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockChannelAsync(ids.ChannelId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.LockPlayerAsync(ids.UserId));

        Assert.Contains("正準ロック順序違反", error.Message);
    }

    [DatabaseFact]
    public async Task 段を飛ばした取得は順序違反にならない()
    {
        // 装備変更は ① → ③ で②を飛ばす
        var ids = NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedAsync(db, ids);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockPlayerAsync(ids.UserId);
        var session = await new BattleSessionRepository(db).CreateAsync(ids.GuildId, ids.ChannelId);

        await scope.LockSessionAsync(session.SessionId);

        Assert.Equal(LockAnchor.Session, scope.HighestAcquired);
    }

    [DatabaseFact]
    public async Task 同じ行の再取得は冪等になる()
    {
        var ids = NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedAsync(db, ids);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockPlayerAsync(ids.UserId);
        await scope.LockChannelAsync(ids.ChannelId);

        // 段を戻る形になるが、既に保持している同一行の再取得は新たなロックではない
        var again = await scope.LockPlayerAsync(ids.UserId);

        Assert.Equal(ids.UserId, again.UserId);
        Assert.Equal(LockAnchor.Channel, scope.HighestAcquired);
    }

    [DatabaseFact]
    public async Task アンカー行が存在しなければ例外になる()
    {
        // 「行が存在しないこと」をロックで守るとギャップロックに依存し、分離レベルを
        // READ COMMITTED に変えた瞬間に排他が消える。行の存在は前提であり、
        // 欠けている場合は Ensure の呼び忘れとして即座に落とす
        await using var db = await fixture.CreateContextAsync();
        await using var scope = await BattleLock.BeginAsync(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.LockChannelAsync(NewIds().ChannelId));

        Assert.Contains("ロックアンカー", error.Message);
    }

    // --- 直列化 ---

    [DatabaseFact]
    public async Task チャンネル行が同一チャンネルの戦闘行動を直列化する()
    {
        var ids = NewIds();
        await using var seedDb = await fixture.CreateContextAsync();
        await SeedAsync(seedDb, ids);

        await using var holderDb = await fixture.CreateContextAsync();
        await using var holder = await BattleLock.BeginAsync(holderDb);
        await holder.LockChannelAsync(ids.ChannelId);

        // 2本目は待たされる。待ち時間を1秒に切り詰めてタイムアウトを観測する
        await using var waiterDb = await fixture.CreateContextAsync();
        await SetLockWaitTimeoutAsync(waiterDb, seconds: 1);
        await using var waiter = await BattleLock.BeginAsync(waiterDb);

        var error = await Record.ExceptionAsync(() => waiter.LockChannelAsync(ids.ChannelId));

        Assert.Equal(LockWaitTimeoutError, FindMySqlError(error).Number);
    }

    [DatabaseFact]
    public async Task 異なるチャンネルは互いに待たされない()
    {
        // 直列化はチャンネル単位。別チャンネルの戦闘まで巻き込むと全サーバーが1本の列になる
        var a = NewIds();
        var b = NewIds();
        await using var seedDb = await fixture.CreateContextAsync();
        await SeedAsync(seedDb, a);
        await SeedAsync(seedDb, b);

        await using var holderDb = await fixture.CreateContextAsync();
        await using var holder = await BattleLock.BeginAsync(holderDb);
        await holder.LockChannelAsync(a.ChannelId);

        await using var otherDb = await fixture.CreateContextAsync();
        await SetLockWaitTimeoutAsync(otherDb, seconds: 1);
        await using var other = await BattleLock.BeginAsync(otherDb);

        var channel = await other.LockChannelAsync(b.ChannelId);

        Assert.Equal(b.ChannelId, channel.ChannelId);
    }

    [DatabaseFact]
    public async Task コミットせずに破棄すると巻き戻る()
    {
        // 抽選のマスタ不整合などで例外が飛んだ場合、そのターン全体が巻き戻り、
        // 不整合なチャンネル状態が残らない
        var ids = NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedAsync(db, ids);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await new BattleSessionRepository(db).CreateAsync(ids.GuildId, ids.ChannelId);
            // CommitAsync を呼ばずに抜ける
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var channel = await verifyDb.ChannelStates.FirstAsync(x => x.ChannelId == ids.ChannelId);

        Assert.Null(channel.CurrentSessionId);
        Assert.Empty(await verifyDb.BattleSessions.Where(x => x.ChannelId == ids.ChannelId).ToListAsync());
    }

    // --- ヘルパ ---

    internal readonly record struct Ids(ulong GuildId, ulong ChannelId, ulong UserId);

    /// <summary>テスト間の干渉を避けるため、実行ごとに衝突しないIDを配る。</summary>
    internal static Ids NewIds()
    {
        var seed = (ulong)Random.Shared.NextInt64(1, long.MaxValue);

        return new Ids(GuildId: seed, ChannelId: seed + 1, UserId: seed + 2);
    }

    /// <summary>
    /// 例外の連鎖から <see cref="MySqlException"/> を取り出す。
    ///
    /// EF Core の実行戦略は、ロック待ちタイムアウトのような一過性の失敗をドライバ例外のまま
    /// 投げず、<c>EnableRetryOnFailure()</c> を促す <see cref="InvalidOperationException"/> に
    /// 包んで返す。素の型で受けようとすると、排他が効いているのにテストだけが落ちる。
    /// </summary>
    private static MySqlException FindMySqlError(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql) return mysql;
        }

        throw new Xunit.Sdk.XunitException(
            $"MySqlException が連鎖に含まれていない: {exception?.ToString() ?? "(例外が発生しなかった)"}");
    }

    internal static async Task SeedAsync(ChidoDbContext db, Ids ids)
    {
        await new PlayerRepository(db).EnsureAsync(ids.UserId, $"P{ids.UserId}");
        await new ChannelStateRepository(db).EnsureAsync(ids.ChannelId);
    }

    /// <summary>
    /// この接続のロック待ちを短くする。既定（50秒）のままではテストが実質固まる。
    ///
    /// <para>
    /// 先に接続を<b>明示的に開く</b>。EF はコマンドごとに接続を開閉するため、
    /// 暗黙のままだと SET の直後に接続がプールへ返り、リセットされて設定が消える。
    /// 明示的に開いた接続は EF が閉じないので、後続の <see cref="BattleLock"/> の
    /// トランザクションまで同一接続が保たれる。
    /// </para>
    /// </summary>
    internal static async Task SetLockWaitTimeoutAsync(ChidoDbContext db, int seconds)
    {
        await db.Database.OpenConnectionAsync();

        // SET 文はプレースホルダを受け付けないため、値の埋め込みが避けられない。
        // 引数はテストコード内のリテラルのみであり、外部入力は通らない
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"SET SESSION innodb_lock_wait_timeout = {seconds}");
#pragma warning restore EF1002
    }
}
