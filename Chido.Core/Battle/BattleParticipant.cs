using System;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public class BattleParticipant
{
    public IEntity        Entity        { get; }
    public EntityType     EntityType    { get; }
    public ulong?         DiscordUserId { get; } // プレイヤーのみ設定 (DB: user_id)
    public Guid?          EnemyId       { get; } // 敵のみ設定。敵出現インスタンスの使い捨てGuid (DB: enemy_id)

    /// <summary>
    /// 表示順。entity_type ごとに独立した番号空間を持つ。
    /// Enemy : 組の member_index の恒等複製。ターゲット自動再選定における「先頭の敵」の唯一の根拠。
    /// Player: セッション内の参加順。Discord埋め込みの表示順にのみ使用される。
    ///
    /// 時刻列（JoinedAt）を順序キーに流用しないための専用の列。DATETIME(3) は
    /// 「同時に走らない」ことが保証されていても「別ミリ秒になること」は保証しないため、
    /// 一括INSERTされる敵の組では順序が一意に定まらない。
    /// </summary>
    public ushort DisplayOrder { get; }

    /// <summary>参加時刻の記録。順序付けには使用しない（DisplayOrder がその責務を持つ）。</summary>
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
        ushort          displayOrder  = 0,
        DateTimeOffset? joinedAt      = null)
    {
        Entity        = entity;
        EntityType    = entityType;
        DiscordUserId = discordUserId;
        EnemyId       = enemyId;
        DisplayOrder  = displayOrder;
        JoinedAt      = joinedAt ?? DateTimeOffset.UtcNow;
    }

    public void SetTarget(Guid? targetEntityId) => CurrentTargetId = targetEntityId;

    public void MarkDefeated() => Status = ParticipantStatus.Defeated;

    public void MarkEscaped() => Status = ParticipantStatus.Escaped;
}
