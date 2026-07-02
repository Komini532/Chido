using System;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public enum EntityType
{
    Player,
    Enemy,
}

public class BattleParticipant
{
    public IEntity        Entity        { get; }
    public EntityType     EntityType    { get; }
    public ulong?         DiscordUserId { get; } // プレイヤーのみ設定 (DB: user_id)
    public Guid?          EnemyId       { get; } // 敵のみ設定。敵出現インスタンスの使い捨てGuid (DB: enemy_id)

    // ターン制御には使わず、Discord埋め込みでの参加者表示順にのみ使う
    public DateTimeOffset JoinedAt { get; }

    public bool IsPlayer => EntityType == EntityType.Player;

    // HP=0 からの間接判定ではなく、状態そのものを一次情報として保持する
    public ParticipantStatus Status { get; private set; } = ParticipantStatus.Active;
    public bool IsActive => Status == ParticipantStatus.Active;

    // 現在相対している敵 (Attack/Skill/Defend の対象解決に使う)。/target や行動コマンドの都度指定で更新される
    public Guid? CurrentTargetId { get; private set; }

    public BattleParticipant(
        IEntity         entity,
        EntityType      entityType,
        ulong?          discordUserId = null,
        Guid?           enemyId       = null,
        DateTimeOffset? joinedAt      = null)
    {
        Entity        = entity;
        EntityType    = entityType;
        DiscordUserId = discordUserId;
        EnemyId       = enemyId;
        JoinedAt      = joinedAt ?? DateTimeOffset.UtcNow;
    }

    public void SetTarget(Guid? targetEntityId) => CurrentTargetId = targetEntityId;

    public void MarkDefeated() => Status = ParticipantStatus.Defeated;

    public void MarkEscaped() => Status = ParticipantStatus.Escaped;
}
