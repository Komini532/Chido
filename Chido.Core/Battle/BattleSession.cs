using System;
using System.Collections.Generic;
using System.Linq;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public class BattleSession
{
    public Guid   Id        { get; private set; } = Guid.NewGuid();
    public ulong  GuildId   { get; private set; }
    public ulong  ChannelId { get; private set; }
    public ulong  MessageId { get; set; } // バトル進捗メッセージ (編集用)

    public List<BattleLogEntry> Log { get; } = [];
    public DateTimeOffset  LastActionAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt      { get; private set; }
    public BattleEndReason? EndReason   { get; private set; }

    public bool IsFinished => EndedAt.HasValue;

    // --- 参加者管理 ---
    private readonly List<BattleParticipant> _participants = [];
    public IReadOnlyList<BattleParticipant> Participants => _participants;

    public BattleSession(ulong guildId, ulong channelId)
    {
        GuildId   = guildId;
        ChannelId = channelId;
    }

    // 飛び入り参加が前提のため、進行中セッションへの途中参加も常に許可する
    public void AddParticipant(BattleParticipant participant)
    {
        _participants.Add(participant);
    }

    // プレイヤーの戦闘行為（攻撃/スキル/戦闘用アイテム）が発生するたびに呼び出す
    public void RecordAction()
    {
        LastActionAt = DateTimeOffset.UtcNow;
    }

    // --- ターゲット解決 ---

    /// <summary>
    /// actor の CurrentTarget を解決する。対象が戦闘不能等で Active でなくなっていた場合は自動失効させ、
    /// Participants リスト内で Active を保つ先頭の敵 (行動側と異なる EntityType) を次点として自動選定する。
    /// </summary>
    public BattleParticipant? ResolveTarget(BattleParticipant actor)
    {
        var current = _participants.FirstOrDefault(p => p.Entity.Id == actor.CurrentTargetId);
        if (current is { IsActive: true })
            return current;

        var next = _participants.FirstOrDefault(p => p.EntityType != actor.EntityType && p.IsActive);
        actor.SetTarget(next?.Entity.Id);
        return next;
    }

    /// <summary>
    /// /target 等による明示的なターゲット指定。対象は行動側と異なる EntityType かつ Active である必要がある。
    /// </summary>
    public bool SetTarget(BattleParticipant actor, Guid targetEntityId)
    {
        var target = _participants.FirstOrDefault(p =>
            p.Entity.Id == targetEntityId && p.EntityType != actor.EntityType && p.IsActive);

        if (target is null) return false;

        actor.SetTarget(target.Entity.Id);
        return true;
    }

    // --- 終了判定 ---
    // 発生順序に依存しない、各 ParticipantStatus の最終状態のみを見る集計ベースの判定
    public (bool ended, BattleEndReason reason) CheckEndCondition()
    {
        var enemies = _participants.Where(p => !p.IsPlayer).ToList();
        var players = _participants.Where(p => p.IsPlayer).ToList();

        bool allEnemiesDefeated = enemies.Count > 0 && enemies.All(p => p.Status == ParticipantStatus.Defeated);
        if (allEnemiesDefeated) return (true, BattleEndReason.PlayerVictory);

        bool allPlayersDone = players.Count > 0 && players.All(p => !p.IsActive);
        if (allPlayersDone)
        {
            // Escaped 済みプレイヤーは全滅判定の母数に含めない一方、1人でも Defeated を経験していれば優先する
            bool anyDefeated = players.Any(p => p.Status == ParticipantStatus.Defeated);
            return (true, anyDefeated ? BattleEndReason.PlayerDefeat : BattleEndReason.Escaped);
        }

        return (false, default);
    }

    public void Finish(BattleEndReason reason)
    {
        EndedAt   = DateTimeOffset.UtcNow;
        EndReason = reason;
    }
}
