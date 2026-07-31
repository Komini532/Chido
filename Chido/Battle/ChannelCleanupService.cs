using Chido.Core.Battle;
using Chido.Data;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chido.Battle;

/// <summary>
/// チャンネル消失の後始末（戦闘システム 6.1・6.3）。
///
/// <para>
/// <c>ChannelMissing</c> は<b>参加者の状態には現れない事象</b>であり、終了判定の関数からは
/// 導けない。戦闘の場そのものが消えたという外部の出来事を、二層で拾って持ち込む。
/// </para>
/// <list type="number">
/// <item>Discord の <c>ChannelDestroyed</c> イベントによる能動検知（即時・取りこぼしうる）</item>
/// <item>定期検証によるフェイルセーフ（遅い・取りこぼさない）</item>
/// </list>
/// <para>
/// 二層にするのは、イベントが Bot の停止中や再接続の隙間に落ちうるため。
/// 落ちたまま誰も拾わないと、消えたチャンネルのセッションに参加していたプレイヤーが
/// <b>永久に他の戦闘へ参加できなくなる</b>（単一セッション制約の拘束が解けない）。
/// </para>
/// <para>
/// どちらの経路も本型の1つのメソッドへ合流させる。処理の順序や漏れの有無が
/// 検知経路ごとに枝分かれすると、片方でだけ拘束が残るという形の不具合を生む。
/// </para>
/// </summary>
public sealed class ChannelCleanupService(
    IDbContextFactory<ChidoDbContext> dbFactory,
    ILogger<ChannelCleanupService> logger)
{
    /// <summary>
    /// 消えたチャンネルの永続状態を畳む。戦闘チャンネルでなければ何もしない（冪等）。
    ///
    /// <para>
    /// <b>次の敵は出さない。</b>チャンネルごと消えているため出現先が存在しない
    /// （<c>SpawnPlanner</c> も <c>ChannelMissing</c> では例外を投げる）。
    /// 累積敵レベルが失われることは「減少しない」規定と矛盾しない。Discordのチャンネルは
    /// 復活せずIDも再利用されないため、その値が再び参照されることがない。
    /// </para>
    /// </summary>
    /// <returns>実際に畳んだなら true。</returns>
    public async Task<bool> CleanupAsync(ulong channelId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var channels = new ChannelStateRepository(db);

        if (await channels.FindAsync(channelId, cancellationToken) is null) return false;

        // ②チャンネル行のみを取る。①を飛ばすのは順序違反ではなく、他プレイヤーの
        // 参加中セッションの記録を落とす書き込みは②に包摂される（戦闘システム 7.2）
        await using var scope = await BattleLock.BeginAsync(db, cancellationToken);
        await scope.LockChannelAsync(channelId, cancellationToken);

        var sessions = new BattleSessionRepository(db);

        if (await sessions.FindActiveAsync(channelId, cancellationToken) is { } session)
        {
            var participants = await sessions.LoadParticipantsAsync(session.SessionId, cancellationToken);

            // 戦闘内スコープの効果はセッションの終了とともに落ちる。永続スコープには触れない
            await new BattleEffectRepository(db).ClearAsync(
                participants.Select(x => x.EntityId).ToList(), cancellationToken);

            // 終了理由を問わず参加中セッションの記録を一括削除する。
            // ここが漏れると、チャンネル消失時に戦闘不能だったプレイヤーが
            // 永久に他の戦闘へ参加できなくなる（戦闘システム 6.1）
            await sessions.FinishAsync(session.SessionId, BattleEndReason.ChannelMissing, cancellationToken);
        }

        await channels.DeleteAsync(channelId, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        logger.LogInformation("チャンネル {ChannelId} の永続状態を削除した。", channelId);

        return true;
    }

    /// <summary>
    /// 戦闘チャンネルとして記録されているチャンネルIDをすべて返す。
    /// 定期検証が「まだ存在するか」を突き合わせる対象になる。
    /// </summary>
    public async Task<IReadOnlyList<ulong>> TrackedChannelsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ChannelStates
            .Select(x => x.ChannelId)
            .ToListAsync(cancellationToken);
    }
}
