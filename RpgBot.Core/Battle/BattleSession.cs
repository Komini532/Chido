using System;
using System.Collections.Generic;
using System.Linq;
using RpgBot.Core.Entities;

namespace RpgBot.Core.Battle;

public class BattleSession
{
    public Guid        Id        { get; private set; } = Guid.NewGuid();
    public ulong        GuildId   { get; private set; }
    public ulong        ChannelId { get; private set; }
    public ulong        MessageId { get; set; } // バトル進捗メッセージ (編集用)
    public BattlePhase  Phase     { get; private set; } = BattlePhase.Recruiting;
    public int          Round     { get; private set; } = 1;

    // 行動順: ラウンド開始時に Initiative 降順でソート済みリスト
    private readonly List<BattleParticipant> _turnOrder = [];
    public IReadOnlyList<BattleParticipant> TurnOrder => _turnOrder;

    private int _turnIndex = 0;
    public BattleParticipant CurrentActor => _turnOrder[_turnIndex];

    public List<BattleLogEntry> Log { get; } = [];
    public DateTimeOffset LastActionAt { get; private set; } = DateTimeOffset.UtcNow;

    // --- 参加者管理 ---
    private readonly List<BattleParticipant> _participants = [];
    public IReadOnlyList<BattleParticipant> Participants => _participants;

    public BattleSession(ulong guildId, ulong channelId)
    {
        GuildId   = guildId;
        ChannelId = channelId;
    }

    public void AddParticipant(BattleParticipant participant)
    {
        if (Phase != BattlePhase.Recruiting)
            throw new InvalidOperationException("バトル開始後は参加不可");
        _participants.Add(participant);
    }

    // --- ラウンド開始 ---
    public void StartBattle(Random rng)
    {
        Phase = BattlePhase.InProgress;
        StartNewRound(rng);
    }

    private void StartNewRound(Random rng)
    {
        foreach (var p in _participants)
        {
            p.RollInitiative(rng);
            p.HasActed = false;
        }

        _turnOrder.Clear();
        _turnOrder.AddRange(_participants
            .Where(p => p.Entity.IsAlive)
            .OrderByDescending(p => p.Initiative));

        _turnIndex = 0;
    }

    // --- ターン進行 ---
    public void AdvanceTurn(Random rng)
    {
        _turnIndex++;

        // ラウンド内の全員が行動済み → 新ラウンド
        if (_turnIndex >= _turnOrder.Count
            || _turnOrder.Skip(_turnIndex).All(p => !p.Entity.IsAlive))
        {
            Round++;
            StartNewRound(rng);
        }
        else
        {
            // 死亡済みはスキップ
            while (_turnIndex < _turnOrder.Count
                   && !_turnOrder[_turnIndex].Entity.IsAlive)
                _turnIndex++;
        }

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

    public void Finish()
    {
        Phase = BattlePhase.Finished;
    }
}
