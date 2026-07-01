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

    // --- 終了判定 ---
    public (bool ended, BattleEndReason reason) CheckEndCondition()
    {
        bool playersAlive = _participants.Any(p => p.IsPlayer && p.Entity.IsAlive);
        bool enemiesAlive = _participants.Any(p => !p.IsPlayer && p.Entity.IsAlive);

        if (!playersAlive) return (true, BattleEndReason.PlayerDefeat);
        if (!enemiesAlive) return (true, BattleEndReason.PlayerVictory);
        return (false, default);
    }

    public void Finish(BattleEndReason reason)
    {
        EndedAt   = DateTimeOffset.UtcNow;
        EndReason = reason;
    }
}
