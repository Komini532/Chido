using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 防御。CurrentTarget からの被ダメージを半減させた上で反撃を受ける (4.2)。
/// 効果量 (現状50%) は今後の拡張で変動しうる。
/// </summary>
public sealed class DefendAction : BattleActionBase
{
    public static readonly Ratio DamageMultiplier = Ratio.FromPercent(50m); // 被ダメージ半減

    public override ActionType Type => ActionType.Defend;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        var target = session.ResolveTarget(actor);
        var logs   = new List<string> { $"{actor.Entity.Name} は防御の構えを取った。" };

        if (target is null)
            return Task.FromResult(Conclude(session, logs));

        session.RecordAction();

        var (_, log) = AttackResolver.Resolve(
            target.Entity, actor.Entity, AttackType.Physical, rng,
            extraMultiplier: DamageMultiplier, extraMultiplierLabel: "防御 ×50%");
        logs.Add(log);

        if (!actor.Entity.IsAlive)
        {
            actor.MarkDefeated();
            logs.Add($"{actor.Entity.Name} は戦闘不能になった！");
        }

        return Task.FromResult(Conclude(session, logs));
    }
}
