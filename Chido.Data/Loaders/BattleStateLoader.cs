using Chido.Core.Battle;
using Chido.Core.Entities;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Chido.Data.Repositories;
using Chido.Data.World;

namespace Chido.Data.Loaders;

/// <summary>
/// 参加者行から、メモリ上の <see cref="BattleSession"/> を丸ごと組み立て直す。
///
/// <para>
/// <b>1回の戦闘行動は1つのトランザクションで完結し、その外側に記憶を持たない。</b>
/// コマンドは互いに独立したプロセス上の出来事であり、前回のターンで作った参加者オブジェクトは
/// もう存在しない。したがって毎回ここで全体を復元し、ターンを解決し、書き戻す。
/// 復元されるのは<b>算出できない値だけ</b>（状態・現在HP・現在TP・現在ターゲット・
/// ローテーション位置・累積与ダメージ・状態変化）であり、ステータスは常に再計算される。
/// </para>
/// <para>
/// <b>戦闘不能・離脱した参加者も読み込む。</b>参加者行は物理削除されず、報酬の分母には
/// 離脱者の与ダメージも残る（戦闘システム 6.2）。除いて読むと分母が縮み、
/// 「主力が削りきってから離脱すれば残りが満額を得る」という悪用経路が開く。
/// </para>
/// </summary>
public sealed class BattleStateLoader(
    ChidoDbContext db,
    EffectCatalog effects,
    DatabaseWorldCatalog world)
{
    /// <summary>
    /// セッションの全参加者を復元する。
    /// </summary>
    /// <param name="record">対象のセッション行。</param>
    public async Task<BattleSession> LoadAsync(
        BattleSessionRecord record, CancellationToken cancellationToken = default)
    {
        var session = new BattleSession(record.GuildId, record.ChannelId);

        var rows = await new BattleSessionRepository(db)
            .LoadParticipantsAsync(record.SessionId, cancellationToken);

        var battleEffects = await new BattleEffectRepository(db)
            .LoadAsync(rows.Select(x => x.EntityId).ToList(), effects, cancellationToken);

        var players = await LoadPlayersAsync(rows, cancellationToken);
        var enemies = await LoadEnemiesAsync(rows, cancellationToken);

        foreach (var row in rows)
        {
            var entity = row.EntityType == EntityType.Player
                ? players.GetValueOrDefault(row.EntityId)
                : enemies.GetValueOrDefault(row.EntityId);

            // 実体を組み立てられなかった行（敵マスタから種族が消えた等）は参加者として復元しない。
            // 対象解決や終了判定の候補に「中身の無い参加者」が混ざるほうが危険であるため
            if (entity is null) continue;

            var participant = new BattleParticipant(
                entity,
                row.EntityType,
                discordUserId: row.UserId,
                enemyId: row.EnemyId,
                displayOrder: row.DisplayOrder,
                joinedAt: row.JoinedAt);

            participant.RestoreState(
                row.Status, row.CurrentTp, row.RotationIndex,
                row.CurrentTargetId, row.TotalDamageDealt);

            // 戦闘内スコープの効果を載せるのは全快の後（プレイヤーは PlayerLoader が全快させ、
            // 敵はここで現在HPを書き戻す）。最大HPは動的算出であり、効果を先に載せても
            // 現在HPには影響しないが、順序の根拠を1つに保つため出現時と同じ並びにしている
            if (entity is EntityBase holder)
            {
                foreach (var effect in battleEffects.GetValueOrDefault(row.EntityId) ?? [])
                {
                    holder.AddEffect(effect);
                }

                holder.RestoreLife(row.CurrentHp);
            }

            session.AddParticipant(participant);
        }

        return session;
    }

    private async Task<Dictionary<Guid, IEntity>> LoadPlayersAsync(
        IReadOnlyList<BattleParticipantRecord> rows, CancellationToken cancellationToken)
    {
        var loader = new PlayerLoader(db, effects);
        var result = new Dictionary<Guid, IEntity>();

        foreach (var row in rows.Where(x => x.EntityType == EntityType.Player))
        {
            if (row.UserId is not { } userId) continue;

            result[row.EntityId] = await loader.LoadAsync(userId, row.EntityId, cancellationToken);
        }

        return result;
    }

    private async Task<Dictionary<Guid, IEntity>> LoadEnemiesAsync(
        IReadOnlyList<BattleParticipantRecord> rows, CancellationToken cancellationToken)
    {
        var enemyRows = rows
            .Where(x => x.EntityType == EntityType.Enemy && x.EnemyId is not null)
            .ToList();

        if (enemyRows.Count == 0) return [];

        var entityIds = enemyRows.ToDictionary(x => x.EnemyId!.Value, x => x.EntityId);

        var enemies = await new EnemyLoader(db, world).LoadAsync(
            enemyRows.Select(x => x.EnemyId!.Value).ToList(), entityIds, cancellationToken);

        return enemies.ToDictionary(x => x.Id, IEntity (x) => x);
    }
}
