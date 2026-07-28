using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 離脱。反撃を受けずに一方的にセッションから離脱する。離脱後は同じ戦闘に再参加できない（戦闘システム 4.3）。
///
/// 戦闘離脱処理を<b>直接</b>呼び出す経路であり、モーションを経由しないため
/// accuracy_rate の判定が入らず<b>必ず成功する</b>（離脱モーションはこれと同じ処理を
/// 間接的に呼ぶが、accuracy_rate を持つため失敗しうる）。
///
/// <b>ターンを消費しない。</b>したがって反撃も、SlipDamage の発動も、
/// 関与者集合の減衰も起こらない。行動枠そのものが開かないためである
/// （離脱<b>モーション</b>ではターンが開くため、そちらは成功・失敗を問わず減衰する。
/// この非対称はコマンドとモーションの経路差から自然に導かれる）。
/// </summary>
public sealed class EscapeAction : BattleActionBase
{
    public override ActionType Type => ActionType.Escape;

    /// <summary>
    /// Active・Defeated のいずれからも実行できる（生死を問わない）。
    /// 戦闘不能プレイヤーが単一セッション制約の拘束を自力で解く唯一の手段であり、
    /// 報酬を放棄する代わりに降りられるという安全弁として設計されている。
    /// </summary>
    protected override bool RequiresActiveActor => false;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        if (actor.Status == ParticipantStatus.Escaped)
        {
            return Task.FromResult(new BattleActionResult(
                false, null, new[] { $"{actor.Entity.Name} は既に戦闘から離脱している。" }));
        }

        actor.MarkEscaped();
        session.RecordAction();

        var logs = new List<string> { $"{actor.Entity.Name} は戦闘から離脱した。" };
        return Task.FromResult(Conclude(session, logs));
    }
}
