using System;
using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Battle.Damage;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// スキル1本のモーション列を motion_index 昇順に再生する（戦闘システム 4.2）。
///
/// 各モーションは以下の5ステップで処理される。ステップ2〜4は独立に働き、
/// いずれか1つでも該当すればそのモーションはスキップされる。
///
/// <code>
/// 1. 行動者が Active でなければ → 以降の全モーションを打ち切り
/// 2. accuracy_gate_group ゲート判定 → 先頭が効果適用に到達していなければ、そのモーションのみスキップ
/// 3. target_rule で対象を解決し、対象が要求する状態でなければ → そのモーションのみスキップ
/// 4. 命中判定（accuracy_rate）→ 外れたら、そのモーションのみスキップ
/// 5. 効果適用
/// </code>
///
/// ステップ1のショートサーキットの根拠は「<b>行動者が</b>離脱・戦闘不能になったのなら、
/// 行動者の以降のモーションはもう再生されえない」という帰結であり、モーションの種別ではない。
/// したがって離脱モーションを特別扱いせず、全モーション共通のステップ1に一般化されている。
/// 「逃げようとして失敗したので殴る」は成立し、target_rule = 敵 の離脱モーションで敵を追い払った
/// 場合も行動者は Active のままなので末尾の自分自身対象モーションは再生される。
/// </summary>
public sealed class SkillPlayer(IMotionEffectApplier? effectApplier = null)
{
    /// <summary>
    /// スキル1本を再生する。
    /// </summary>
    /// <param name="enemyTarget">
    /// そのターンの相対する相手。ターン開始時に一度だけ解決された結果を渡すこと。
    /// モーションごとに再導出すると、対象を倒した後に別の敵へ乗り換わってしまい、
    /// 「そのモーションのみスキップ」という規則が成立しなくなる。
    /// </param>
    /// <param name="commandTarget">コマンドの [対象]。味方対象モーションでのみ消費される。</param>
    /// <param name="onDamageDealt">
    /// 実効ダメージが確定するたびに呼ばれる。与ダメージの台帳計上（およびTP蓄積）の接続点。
    /// </param>
    public SkillPlayResult Play(
        BattleParticipant actor,
        Skill skill,
        BattleParticipant enemyTarget,
        Random rng,
        BattleParticipant? commandTarget = null,
        EnemyAllyTargetSelector? enemyAllySelector = null,
        Action<BattleParticipant, BattleParticipant, BigInteger>? onDamageDealt = null)
    {
        var logs = new List<string>();
        var outcomes = new Dictionary<byte, MotionOutcome>();

        foreach (var motion in skill.Motions)
        {
            // ステップ1: 行動者が Active でなければ以降を打ち切る
            if (!actor.IsActive)
            {
                outcomes[motion.MotionIndex] = MotionOutcome.ShortCircuited;
                continue;
            }

            var outcome = PlayMotion(
                actor, skill, motion, enemyTarget, rng, commandTarget, enemyAllySelector, onDamageDealt, logs, outcomes);

            outcomes[motion.MotionIndex] = outcome;
        }

        return new SkillPlayResult(logs, outcomes);
    }

    private MotionOutcome PlayMotion(
        BattleParticipant actor,
        Skill skill,
        SkillMotion motion,
        BattleParticipant enemyTarget,
        Random rng,
        BattleParticipant? commandTarget,
        EnemyAllyTargetSelector? enemyAllySelector,
        Action<BattleParticipant, BattleParticipant, BigInteger>? onDamageDealt,
        List<string> logs,
        IReadOnlyDictionary<byte, MotionOutcome> outcomes)
    {
        // ステップ2: ゲート判定。依存先は常に先頭1件であり、直前のメンバーではない。
        // メンバー同士が連鎖しないため「攻撃命中 → 毒30% ＆ 麻痺20% をそれぞれ独立判定」が表現できる
        if (motion.AccuracyGateGroup is { } gateGroup)
        {
            var leader = skill.GateLeaderOf(gateGroup);

            // 自分自身が先頭なら他に依存しない。先頭が未再生（＝自分より後ろ）ということは起こらない
            if (leader is not null && leader.MotionIndex != motion.MotionIndex)
            {
                var leaderOutcome = outcomes.TryGetValue(leader.MotionIndex, out var o) ? o : MotionOutcome.SkippedByGate;

                // ゲートでスキップされたメンバーには個別通知を出さない
                // （先頭の失敗が既に通知されており、帰結が自明であるため）
                if (!leaderOutcome.OpensGate()) return MotionOutcome.SkippedByGate;
            }
        }

        var target = TargetResolver.Resolve(motion, actor, enemyTarget, commandTarget, enemyAllySelector);

        // ステップ3: 対象状態の判定。「モーションの要求する状態」は現行では全モーションが Active。
        // 複数モーションの途中で対象が戦闘不能・離脱した場合もここに従い、そのモーションのみスキップされる
        // （行動者の生存判定であるステップ1とは独立）
        if (!target.IsActive) return MotionOutcome.SkippedByTargetStatus;

        // ステップ4: 命中判定。外れたモーションはダメージ0を通すのではなくパイプラインに入らないため、
        // 最低ダメージ1の保証にも到達しない
        if (!motion.AccuracyRate.Roll(rng))
        {
            logs.Add($"{actor.Entity.Name} の攻撃は外れた。");
            return MotionOutcome.Missed;
        }

        // ステップ5: 効果適用
        Apply(actor, skill, motion, target, rng, onDamageDealt, logs);
        return MotionOutcome.Applied;
    }

    private void Apply(
        BattleParticipant actor,
        Skill skill,
        SkillMotion motion,
        BattleParticipant target,
        Random rng,
        Action<BattleParticipant, BattleParticipant, BigInteger>? onDamageDealt,
        List<string> logs)
    {
        switch (motion)
        {
            case AttackMotion attack:
            {
                var (damage, log) = AttackResolver.Resolve(
                    actor.Entity, target.Entity, attack.AttackType, rng,
                    power: attack.Power, motionElements: attack.Elements);

                logs.Add(log);
                onDamageDealt?.Invoke(actor, target, damage);

                if (!target.Entity.IsAlive)
                {
                    target.MarkDefeated();
                    logs.Add($"{target.Entity.Name} は戦闘不能になった！");
                }
                break;
            }

            case HealMotion heal:
            {
                var amount = HealPipeline.Resolve(actor.Entity, heal.AttackType, heal.Power);
                target.Entity.Heal(amount);
                logs.Add($"{target.Entity.Name} のHPが {amount} 回復した。");
                break;
            }

            case FleeMotion:
            {
                // /escape と同一の戦闘離脱処理。ただし accuracy_rate を持つため失敗しうる点が異なる。
                // 対象が行動者自身なら、次のモーションからステップ1のショートサーキットが働く
                target.MarkEscaped();
                logs.Add($"{target.Entity.Name} は戦闘から離脱した。");
                break;
            }

            case GrantEffectMotion grant:
            {
                var message = effectApplier?.Grant(actor, target, grant, skill.SkillKey);
                if (message is not null) logs.Add(message);
                break;
            }

            case DispelEffectMotion dispel:
            {
                var message = effectApplier?.Dispel(target, dispel);
                if (message is not null) logs.Add(message);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(motion), motion.MotionType, "未知のモーション種別。");
        }
    }
}

/// <summary>スキル1本の再生結果。</summary>
/// <param name="Logs">表示用のログ列。</param>
/// <param name="Outcomes">motion_index ごとの再生結果。ゲート判定とテストの検証に使う。</param>
public sealed record SkillPlayResult(
    IReadOnlyList<string> Logs,
    IReadOnlyDictionary<byte, MotionOutcome> Outcomes);
