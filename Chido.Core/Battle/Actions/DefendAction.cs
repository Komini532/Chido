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
    // 被ダメージへの乗算係数 (1 - DRR)。軽減率そのものは GameConstants が持ち、ここでは係数へ変換して使う。
    // Phase 5 で Defend を「自分自身への DRR 付与モーション1つを持つスキル」として再構成する際、
    // この係数の算出はダメージパイプラインの PostDefense へ移る（戦闘システム 5.1・5.4）。
    private static readonly Ratio DamageMultiplier = Ratio.Full - GameConstants.DefendDamageResistRate;

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
