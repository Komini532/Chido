using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Entities;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 戦闘セッションと参加者の永続化。
///
/// セッション・参加者・参加中セッションの3テーブルは1つの集約として扱う。
/// 特にセッション終了時の <c>chido_player_in_battle_session</c> の一括削除は、
/// 漏れるとプレイヤーが永久に他の戦闘へ参加できなくなるため、終了処理から分離しない。
///
/// <b>すべてのメソッドはチャンネル行②のロック下で呼ぶこと</b>（戦闘システム 7.2）。
/// 参加者行への明示的なロックは不要であり、②に包摂される。
/// </summary>
public sealed class BattleSessionRepository(ChidoDbContext db)
{
    /// <summary>
    /// チャンネルで進行中のセッションを取得する。無ければ null。
    ///
    /// 真実の情報源は <c>chido_channel_state.current_session_id</c> であり、
    /// <c>ended_at IS NULL</c> の走査ではない。MySQL は「<c>ended_at IS NULL</c> の行が
    /// チャンネルごとに1つ」という部分ユニークインデックスを張れないため、
    /// 終了済みセッションが残る同テーブルを条件で絞る形にすると、
    /// 取りこぼしや二重ヒットが制約ではなくクエリの正しさに依存してしまう。
    /// </summary>
    public async Task<BattleSessionRecord?> FindActiveAsync(
        ulong channelId, CancellationToken cancellationToken = default)
    {
        var channel = await db.ChannelStates
            .FirstOrDefaultAsync(x => x.ChannelId == channelId, cancellationToken);

        if (channel?.CurrentSessionId is not { } sessionId) return null;

        return await db.BattleSessions
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.EndedAt == null, cancellationToken);
    }

    /// <summary>
    /// セッションを新規生成し、チャンネルの現在セッションに紐づける。
    ///
    /// <b>セッション生成レースはチャンネル行②のロックで直列化される。</b>
    /// セッションはプレイヤーの最初の戦闘行為時に生成されるため、その瞬間にはロック対象の
    /// セッション行が存在せず、DB制約でも防げない（部分ユニークインデックスが張れないため）。
    /// ②を先に取得することが唯一の担保であり、本メソッドはそれを前提とする。
    /// </summary>
    public async Task<BattleSessionRecord> CreateAsync(
        ulong guildId, ulong channelId, Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var channel = await db.ChannelStates.FirstAsync(x => x.ChannelId == channelId, cancellationToken);

        if (channel.CurrentSessionId is not null)
        {
            throw new InvalidOperationException(
                $"チャンネル {channelId} には既に進行中のセッションがある。" +
                "1チャンネルにアクティブなセッションは1つ以下でなければならない。");
        }

        var session = new BattleSessionRecord
        {
            SessionId = sessionId ?? Guid.NewGuid(),
            GuildId = guildId,
            ChannelId = channelId,
            CreatedAt = DateTime.UtcNow,
        };

        db.BattleSessions.Add(session);
        channel.CurrentSessionId = session.SessionId;

        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    /// <summary>
    /// プレイヤーをセッションに参加させる。飛び入り参加が前提のため、進行中セッションへの
    /// 途中参加も常に許可する。既に同じセッションに参加していれば何もしない。
    ///
    /// <b>単一セッション制約</b>（1プレイヤーが同時に参加できるセッションは1つ）は
    /// <c>chido_player_in_battle_session</c> の1プレイヤー1行構造が裏付ける。
    /// 別のセッションに参加中なら例外を投げる。
    /// </summary>
    /// <returns>本呼び出しで新規に参加したなら、その参加者行。既に参加済みなら既存の行。</returns>
    public async Task<BattleParticipantRecord> JoinPlayerAsync(
        Guid sessionId, ulong userId, ushort displayOrder,
        CancellationToken cancellationToken = default)
    {
        var membership = await db.PlayerInBattleSessions
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (membership is not null)
        {
            if (membership.SessionId != sessionId)
            {
                throw new InvalidOperationException(
                    $"プレイヤー {userId} は既に別のセッション {membership.SessionId} に参加している。");
            }

            return await db.BattleParticipants
                .FirstAsync(x => x.EntityId == membership.EntityId, cancellationToken);
        }

        var participant = new BattleParticipantRecord
        {
            SessionId = sessionId,
            EntityId = Guid.NewGuid(),
            EntityType = EntityType.Player,
            UserId = userId,
            EnemyId = null,
            Status = ParticipantStatus.Active,
            CurrentHp = 0,
            CurrentTp = 0,
            CurrentTargetId = null,
            RotationIndex = 0,
            DisplayOrder = displayOrder,
            TotalDamageDealt = 0,
            JoinedAt = DateTime.UtcNow,
        };

        db.BattleParticipants.Add(participant);
        db.PlayerInBattleSessions.Add(new PlayerInBattleSessionRecord
        {
            UserId = userId,
            SessionId = sessionId,
            EntityId = participant.EntityId,
        });

        await db.SaveChangesAsync(cancellationToken);
        return participant;
    }

    /// <summary>
    /// セッション内のプレイヤー参加者行を引く。参加中セッションの記録（<c>membership</c>）とは独立に、
    /// <b>参加者行の有無そのもの</b>を見る。
    ///
    /// 離脱すると参加中セッションの記録は外れるが参加者行は残るため、この2つは食い違う。
    /// 「離脱後は同じ戦闘に再参加できない」（戦闘システム 4.3・B-13）を成り立たせているのは
    /// 残り続ける参加者行のほうであり、記録の有無で判定すると再参加を許してしまう。
    /// </summary>
    public Task<BattleParticipantRecord?> FindParticipantAsync(
        Guid sessionId, ulong userId, CancellationToken cancellationToken = default)
        => db.BattleParticipants
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.UserId == userId, cancellationToken);

    /// <summary>
    /// 参加中セッションの記録を外す。参加者行は<b>残す</b>。
    ///
    /// <b>離脱したプレイヤーの拘束を解く唯一の経路である</b>（戦闘システム 4.3）。
    /// 呼び忘れると、離脱したはずのプレイヤーがそのセッションが終わるまで
    /// 他の戦闘に参加できないままになる。セッションの終了を待たずに解ける点が
    /// <see cref="FinishAsync"/> の一括削除との違い。
    /// </summary>
    public async Task LeaveAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await db.PlayerInBattleSessions
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete は変更追跡を経由しないため、削除済みの行が Unchanged のまま残る。
        // 同じ DbContext で別セッションへ参加すると主キーが衝突するため追跡から外す
        foreach (var entry in db.ChangeTracker.Entries<PlayerInBattleSessionRecord>()
                     .Where(e => e.Entity.UserId == userId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>セッションの参加者行をすべて読む。表示順は entity_type ごとに独立。</summary>
    public Task<List<BattleParticipantRecord>> LoadParticipantsAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
        => db.BattleParticipants
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.EntityType)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// セッションを終了する。
    ///
    /// <b><c>end_reason</c> の値を問わず（<c>ChannelMissing</c> を含む）、参加していた
    /// 全プレイヤーの「参加中セッション」の記録を削除する。</b>
    /// これが漏れると、チャンネル消失時に <c>Defeated</c> だったプレイヤーが
    /// 永久に他の戦闘へ参加できなくなる（戦闘システム 6.1）。
    ///
    /// 参加者行と敵の出現インスタンスは<b>物理削除しない</b>。戦闘終了後も記録として残す
    /// （戦闘システム 3.2・8.1）。
    /// </summary>
    public async Task FinishAsync(
        Guid sessionId, BattleEndReason reason, CancellationToken cancellationToken = default)
    {
        var session = await db.BattleSessions.FirstAsync(x => x.SessionId == sessionId, cancellationToken);

        session.EndedAt = DateTime.UtcNow;
        session.EndReason = reason;

        // 終了理由に依らず必ず外す。単一セッション制約の拘束はここでしか解けない
        await db.PlayerInBattleSessions
            .Where(x => x.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete は変更追跡を経由しないため、追跡済みの行が Unchanged のまま残る。
        // 同じ DbContext で同じプレイヤーが別セッションへ参加すると、削除済みのはずの主キーが
        // 「既に追跡されている」と衝突して例外になるため、ここで追跡から外す
        foreach (var entry in db.ChangeTracker.Entries<PlayerInBattleSessionRecord>()
                     .Where(e => e.Entity.SessionId == sessionId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        var channel = await db.ChannelStates
            .FirstOrDefaultAsync(x => x.CurrentSessionId == sessionId, cancellationToken);

        if (channel is not null) channel.CurrentSessionId = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 参加者の可変状態を書き戻す。
    ///
    /// ステータスは保持せず参照のたびに算出するため（戦闘システム 2.5）、
    /// 永続化するのは<b>算出できない値のみ</b>＝状態・現在HP・現在TP・現在ターゲット・
    /// ローテーション位置・累積与ダメージに限る。
    ///
    /// 消失順（<c>DeactivationOrder</c>）には対応する列が無く、書き戻しの対象でもない。
    /// あの値が要るのは「敵側の生存が0になった、まさにそのターン」だけであり、
    /// そのターンのうちにセッションが終了するため、次のコマンドまで持ち越す必要がない
    /// （<c>BattleParticipant.RestoreState</c> 参照）。
    /// </summary>
    public async Task SaveParticipantStateAsync(
        BattleParticipant participant, CancellationToken cancellationToken = default)
    {
        var record = await db.BattleParticipants
            .FirstAsync(x => x.EntityId == participant.Entity.Id, cancellationToken);

        record.Status = participant.Status;
        record.CurrentHp = participant.Entity.CurrentLife;
        record.CurrentTp = participant.CurrentTp;
        record.CurrentTargetId = participant.CurrentTargetId;
        record.RotationIndex = (byte)participant.RotationIndex;

        // 台帳。報酬の按分・付与ゲート・被攻撃TPが同じ量を参照するため、
        // ここが漏れるとコマンドをまたいだ貢献が積み上がらず、全員が最後の1行動ぶんで評価される
        record.TotalDamageDealt = participant.TotalDamageDealt;
    }

    /// <summary>
    /// 敵を参加者として登録する。<b>チャンネルに出現中の敵がセッションへ引き込まれる契機</b>であり、
    /// セッション生成の直後に組の全メンバーぶんを呼ぶ。
    ///
    /// <c>display_order</c> は組の <c>member_index</c>（＝<c>spawn_index</c>）の恒等複製であり、
    /// ターゲット自動再選定における「先頭の敵」の唯一の根拠になる。
    /// </summary>
    public async Task<BattleParticipantRecord> JoinEnemyAsync(
        Guid sessionId, Guid enemyId, ushort displayOrder, ushort initialTp,
        BigInteger currentHp, CancellationToken cancellationToken = default)
    {
        var existing = await db.BattleParticipants
            .FirstOrDefaultAsync(x => x.SessionId == sessionId && x.EnemyId == enemyId, cancellationToken);

        if (existing is not null) return existing;

        var participant = new BattleParticipantRecord
        {
            SessionId = sessionId,
            EntityId = Guid.NewGuid(),
            EntityType = EntityType.Enemy,
            UserId = null,
            EnemyId = enemyId,
            Status = ParticipantStatus.Active,
            CurrentHp = currentHp,
            CurrentTp = initialTp,
            CurrentTargetId = null,
            RotationIndex = 0,
            DisplayOrder = displayOrder,
            TotalDamageDealt = 0,
            JoinedAt = DateTime.UtcNow,
        };

        db.BattleParticipants.Add(participant);
        await db.SaveChangesAsync(cancellationToken);

        return participant;
    }

    /// <summary>プレイヤー参加者の次の表示順。参加順にそのまま並ぶ。</summary>
    public async Task<ushort> NextPlayerDisplayOrderAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
        => (ushort)await db.BattleParticipants
            .CountAsync(x => x.SessionId == sessionId && x.EntityType == EntityType.Player, cancellationToken);
}
