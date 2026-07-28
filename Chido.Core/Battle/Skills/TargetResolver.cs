using System;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// 敵が target_rule = 味方 のモーションを実行する場合の対象選択。
/// 敵には [対象] 入力も CurrentTarget の流用も無いため、別の規則で解決する必要がある
/// （chido_enemy_master.ally_target_rule。戦闘システム 4.2）。
/// </summary>
/// <param name="actor">実行者。候補集合は自軍側の Active な参加者であり、実行者自身を含む。</param>
public delegate BattleParticipant EnemyAllyTargetSelector(BattleParticipant actor);

/// <summary>
/// target_rule によるモーションの対象解決（戦闘システム 4.2）。
///
/// target_rule は「プレイヤーが選ぶ選択肢」ではなく「対象をどう解決するかの規則」であり、
/// 3つの値のうち2つはユーザー入力を一切消費しない。
/// </summary>
public static class TargetResolver
{
    /// <summary>
    /// モーション1件の対象を解決する。
    /// </summary>
    /// <param name="commandTarget">
    /// コマンドの [対象]。target_rule = 味方 のモーションでのみ消費され、省略時は行動者自身に解決する。
    /// [対象] はコマンドの引数であってモーションの引数ではないため、1スキル内に味方対象モーションが
    /// 複数あってもすべてが同じ1つの [対象] に解決される（曖昧さが生じる余地はない）。
    /// </param>
    /// <param name="enemyTarget">
    /// そのターンの相対する相手。<b>ターン開始時に一度だけ解決された結果</b>を渡すこと
    /// （<see cref="BattleSession.ResolveTarget"/> の導出と書き戻しはターンにつき1回）。
    ///
    /// モーションごとに再導出してはならない。複数モーションの途中で対象が戦闘不能になった場合、
    /// 設計はそのモーションを「ステップ3の対象状態判定でスキップする」と定めており（戦闘システム 4.2）、
    /// 再導出すると別の敵へ乗り換わってしまってこの規則が成立しない。
    /// また、そのターンの反撃者は既に確定しているため、途中で対象が変わること自体が矛盾する。
    /// </param>
    /// <param name="enemyAllySelector">
    /// 敵が味方対象モーションを実行する場合の選択規則。未指定なら実行者自身に解決する。
    /// </param>
    public static BattleParticipant Resolve(
        SkillMotion motion,
        BattleParticipant actor,
        BattleParticipant enemyTarget,
        BattleParticipant? commandTarget = null,
        EnemyAllyTargetSelector? enemyAllySelector = null)
        => motion.TargetRule switch
        {
            // [対象] の指定があっても対象は変わらない。「自分を強化」のように
            // 味方に向けられては困る効果のための、味方よりも強い規則
            TargetRule.Myself => actor,

            // CurrentTarget を読む。target_rule と CurrentTarget は別概念ではなく上位・下位の関係
            TargetRule.Enemy => enemyTarget,

            TargetRule.Ally => ResolveAlly(actor, commandTarget, enemyAllySelector),

            _ => throw new ArgumentOutOfRangeException(
                nameof(motion), motion.TargetRule, "未知の対象解決規則。"),
        };

    /// <summary>
    /// 「味方」は自分自身を含む。同一のスキルエントリが自己対象と味方対象の双方として機能し、
    /// スキルデータの倍増を避けられる。
    /// </summary>
    private static BattleParticipant ResolveAlly(
        BattleParticipant actor,
        BattleParticipant? commandTarget,
        EnemyAllyTargetSelector? enemyAllySelector)
    {
        // 敵は [対象] を持たないため ally_target_rule で解決する。
        // 規則が未提供の場合は実行者自身に解決する（「味方」が自分を含むため、
        // 候補が空にならない値0・24 の挙動とも整合する）
        if (actor.EntityType == EntityType.Enemy)
            return enemyAllySelector?.Invoke(actor) ?? actor;

        return commandTarget ?? actor;
    }
}
