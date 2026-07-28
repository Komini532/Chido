using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chido.Core.Battle.Damage;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 戦闘用アイテムの使用。対象が自身/味方であっても、CurrentTarget からの反撃とセットで
/// 1交換として処理する (4.2) — 回復アイテムを使ったターンも無防備になる、という設計。
/// </summary>
public sealed class UseAction : BattleActionBase
{
    private readonly IBattleItemEffect _effect;
    private readonly BattleParticipant _effectTarget; // 自身 or 味方

    public UseAction(IBattleItemEffect effect, BattleParticipant effectTarget)
    {
        _effect       = effect;
        _effectTarget = effectTarget;
    }

    public override ActionType Type => ActionType.Item;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        var logs = new List<string> { _effect.Apply(actor.Entity, _effectTarget.Entity) };

        var target = session.ResolveTarget(actor);
        session.RecordAction();

        if (target is null)
            return Task.FromResult(Conclude(session, logs));

        var (_, log) = AttackResolver.Resolve(target.Entity, actor.Entity, AttackType.Physical, rng);
        logs.Add(log);

        if (!actor.Entity.IsAlive)
        {
            actor.MarkDefeated();
            logs.Add($"{actor.Entity.Name} は戦闘不能になった！");
        }

        return Task.FromResult(Conclude(session, logs));
    }
}
