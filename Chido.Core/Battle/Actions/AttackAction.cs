using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chido.Core.Battle.Damage;

namespace Chido.Core.Battle.Actions;

/// <summary>通常攻撃。CurrentTarget との間で1対1の交換として処理する (4.2)。</summary>
public sealed class AttackAction : BattleActionBase
{
    public override ActionType Type => ActionType.Attack;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        var target = session.ResolveTarget(actor);
        if (target is null)
        {
            return Task.FromResult(new BattleActionResult(
                false, null, new[] { $"{actor.Entity.Name} の攻撃対象が見つからない。" }));
        }

        session.RecordAction();

        var logs = ExchangeRunner.Run(
            actor,  (a, d, r) => AttackResolver.Resolve(a, d, AttackType.Physical, r),
            target, (a, d, r) => AttackResolver.Resolve(a, d, AttackType.Physical, r),
            rng);

        return Task.FromResult(Conclude(session, logs));
    }
}
