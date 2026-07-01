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
    public string?        EnemyRef      { get; } // 敵のみ設定 (DB: enemy_ref)

    // ターン制御には使わず、Discord埋め込みでの参加者表示順にのみ使う
    public DateTimeOffset JoinedAt { get; }

    public bool IsPlayer => EntityType == EntityType.Player;

    public BattleParticipant(
        IEntity         entity,
        EntityType      entityType,
        ulong?          discordUserId = null,
        string?         enemyRef      = null,
        DateTimeOffset? joinedAt      = null)
    {
        Entity        = entity;
        EntityType    = entityType;
        DiscordUserId = discordUserId;
        EnemyRef      = enemyRef;
        JoinedAt      = joinedAt ?? DateTimeOffset.UtcNow;
    }
}
