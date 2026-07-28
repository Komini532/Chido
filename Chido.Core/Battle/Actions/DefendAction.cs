using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 防御。反撃モーションを持たず、実体は「自分自身への DRR 付与」1つである（戦闘システム 4.2）。
/// 構えを取ったうえで CurrentTarget からの反撃を受ける。
///
/// 軽減は DEF への補正ではなく最終ダメージへの乗算係数として働くため、
/// ここでは対象へ DRR の StatusModifier を載せ、実際の減算はダメージパイプラインの
/// PostDefense が行う（戦闘システム 5.1）。
///
/// 状態変化のライフサイクル（duration_actions = 1 による1行動での消失）が実装されるまでの間、
/// 付与と除去をこの行動の中で完結させている。ライフサイクルが載った時点で
/// 「自分自身が対象・duration_actions = 1 の DRR 付与モーションを1つ持つスキル」という
/// マスタデータへ置き換わり、この特別扱いは不要になる。
/// </summary>
public sealed class DefendAction : BattleActionBase
{
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

        var guard = new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate);
        var defender = actor.Entity as EntityBase;
        defender?.AddStatusModifier(guard);

        try
        {
            var (_, log) = AttackResolver.Resolve(target.Entity, actor.Entity, AttackType.Physical, rng);
            logs.Add(log);
        }
        finally
        {
            defender?.RemoveStatusModifier(guard);
        }

        if (!actor.Entity.IsAlive)
        {
            actor.MarkDefeated();
            logs.Add($"{actor.Entity.Name} は戦闘不能になった！");
        }

        return Task.FromResult(Conclude(session, logs));
    }
}
