using Chido.Core;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// チャンネル単位の永続状態（現在フィールド・累積敵レベル・現在セッション）。
/// <c>chido_channel_state</c> はチャンネルに関するロックアンカーを兼ねる。
///
/// <b>行の存在自体が「このチャンネルは戦闘チャンネルである」ことを意味する。</b>
/// 常に行が存在することがアンカー方式の前提であるため、行の作成は戦闘チャンネルの
/// 初期化という明示的な操作に限る。
/// </summary>
public sealed class ChannelStateRepository(ChidoDbContext db)
{
    /// <summary>
    /// 戦闘チャンネルとして初期化する。既に初期化済みなら何もしない（冪等）。
    ///
    /// <b>ロックスコープの外側で呼ぶこと</b>（理由は <c>PlayerRepository.EnsureAsync</c> と同じ）。
    /// 初期フィールドは草原に固定され、累積敵レベルは 1 から始まる。
    /// </summary>
    /// <returns>本呼び出しで新規に初期化したなら true。</returns>
    public async Task<bool> EnsureAsync(ulong channelId, CancellationToken cancellationToken = default)
    {
        if (await db.ChannelStates.AnyAsync(x => x.ChannelId == channelId, cancellationToken)) return false;

        db.ChannelStates.Add(new ChannelStateRecord
        {
            ChannelId = channelId,
            CurrentFieldKey = GameConstants.GrasslandFieldKey,
            CumulativeEnemyLevel = GameConstants.InitialCumulativeEnemyLevel,
            CurrentSessionId = null,
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ChannelStateRecord?> FindAsync(ulong channelId, CancellationToken cancellationToken = default)
        => db.ChannelStates.FirstOrDefaultAsync(x => x.ChannelId == channelId, cancellationToken);

    /// <summary>
    /// チャンネルの永続状態と、そこに出現中の敵の記録を削除する。
    ///
    /// <c>ChannelMissing</c> によるセッション終了時に呼ぶ。Discordのチャンネルは復活せず
    /// IDも再利用されないため、累積敵レベルが失われることは「減少しない」規定と矛盾しない
    /// （戦闘システム 6.3）。
    /// </summary>
    public async Task DeleteAsync(ulong channelId, CancellationToken cancellationToken = default)
    {
        await db.ChannelCurrentEnemies
            .Where(x => x.ChannelId == channelId)
            .ExecuteDeleteAsync(cancellationToken);

        await db.ChannelStates
            .Where(x => x.ChannelId == channelId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
