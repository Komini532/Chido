using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Entities.Enemies;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// 敵の味方対象モーションの対象解決（<c>ally_target_rule</c>。戦闘システム 4.2）。
///
/// 候補集合は自軍側の Active な参加者であり、<b>実行者自身を含む</b>
/// （「味方」に自分を含む点はプレイヤー側と対称）。そのため値0・24 では候補が空にならず、
/// 単独時は自然に自分が選ばれる。明示的なフォールバックが要るのは候補を意図的に狭める値1のみ。
///
/// 規則は敵の種族単位であるため、1体の敵が持つ複数の味方対象スキルはすべて同じ規則で対象を選ぶ。
/// 「取り巻きを回復しつつ自分だけを強化」は、強化側モーションを target_rule = 自分自身 に
/// することで表現する。
/// </summary>
/// <param name="rng">
/// 同値タイブレーク用の乱数。<b>行動順を決める「そのターンにつき1回引く乱数」とは別系統</b>だが、
/// 再現性のため同一の乱数源から独立に引く（4.2）。
/// </param>
public sealed class AllyTargetResolver(BattleSession session, Random rng)
{
    /// <summary>
    /// <see cref="TargetResolver"/> へ差し込む形に束ねる。
    /// </summary>
    public EnemyAllyTargetSelector AsSelector() => Resolve;

    public BattleParticipant Resolve(BattleParticipant actor)
    {
        var candidates = session.Participants
            .Where(p => p.EntityType == actor.EntityType && p.IsActive)
            .ToList();

        // 実行者自身が候補に含まれない状況は、実行者が Active でなければモーション再生の
        // ステップ1で打ち切られているため起こらない
        if (candidates.Count == 0) return actor;

        var rule = (actor.Entity as Enemy)?.AllyTargetRule ?? AllyTargetRule.PureRandom;

        // 未実装の予約値がマスタに紛れ込んでも戦闘を止めない。判定は IsImplemented の1箇所に集約されており、
        // マスタ検証と実行時のフォールバックが同じ根拠を見る
        if (!rule.IsImplemented()) rule = AllyTargetRule.PureRandom;

        return rule switch
        {
            AllyTargetRule.PureRandom => PickRandom(candidates),
            AllyTargetRule.RandomExceptSelf => PickExceptSelf(candidates, actor),
            AllyTargetRule.LowestLifeRatio => PickLowestLifeRatio(candidates),

            _ => PickRandom(candidates),
        };
    }

    private BattleParticipant PickRandom(IReadOnlyList<BattleParticipant> candidates)
        => candidates[rng.Next(candidates.Count)];

    /// <summary>候補から自分を除いた集合からランダム。空なら自分（単独時のフォールバック）。</summary>
    private BattleParticipant PickExceptSelf(
        IReadOnlyList<BattleParticipant> candidates, BattleParticipant actor)
    {
        var others = candidates.Where(p => p != actor).ToList();

        return others.Count == 0 ? actor : PickRandom(others);
    }

    /// <summary>
    /// <c>CurrentLife ÷ MaxLife</c> が最小の集合からランダム。
    ///
    /// <b>除算せず交差乗算で比較する。</b><c>MaxLife &gt; 0</c> が常に保証されるため
    /// <c>a.CurrentLife × b.MaxLife &lt; b.CurrentLife × a.MaxLife</c> が成立し、
    /// 浮動小数点を通さずオーバーヒール（割合が1超）も自然に扱える。
    /// 丸めを伴う <c>LifeRatio</c> を使うと同順位の判定がずれるため、あちらは使わない。
    /// </summary>
    private BattleParticipant PickLowestLifeRatio(IReadOnlyList<BattleParticipant> candidates)
    {
        // 候補集合を Active で定義したうえでの追加条件（異常系のフェイルセーフ）。
        // 「HP0 だが Active」が生じた場合に、割合0の瀕死者へ回復対象が固定される退化を防ぐ。
        // 正常系を状態で・異常系を値で塞ぐ二重防御であり、レベルのクランプと同じ形をしている
        var alive = candidates.Where(p => p.Entity.CurrentLife > BigInteger.Zero).ToList();
        var pool = alive.Count > 0 ? alive : candidates;

        var lowest = pool[0];
        var tied = new List<BattleParticipant> { lowest };

        foreach (var candidate in pool.Skip(1))
        {
            var comparison = CompareLifeRatio(candidate, lowest);

            if (comparison < 0)
            {
                lowest = candidate;
                tied.Clear();
                tied.Add(candidate);
            }
            else if (comparison == 0)
            {
                tied.Add(candidate);
            }
        }

        return tied.Count == 1 ? tied[0] : PickRandom(tied);
    }

    /// <summary>HP割合の交差乗算による比較。負なら a のほうが割合が小さい。</summary>
    private static int CompareLifeRatio(BattleParticipant a, BattleParticipant b)
        => (a.Entity.CurrentLife * b.Entity.MaxLife).CompareTo(b.Entity.CurrentLife * a.Entity.MaxLife);
}
