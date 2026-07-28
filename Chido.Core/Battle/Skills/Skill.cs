using System.Collections.Generic;
using System.Linq;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// スキル（chido_skill_master とそのモーション列）。
///
/// 通常攻撃（Attack）と防御（Defend）も特別扱いの別実装ではなく、本型の通常のインスタンスとして
/// 表現される。Attack は「対象に威力100%の無属性物理攻撃」という単一モーション、
/// Defend は「自分自身への DRR 付与モーション1つ」（反撃モーションなし）である。
/// マスタデータはエンティティ種別を問わず共通のため、これらはプレイヤー・敵間で共有される。
/// </summary>
public sealed class Skill
{
    public string SkillKey { get; }
    public string Name { get; }

    /// <summary>
    /// 行動優先度。行動順は priority 降順 → Speed → Random で決まる。
    /// 既定は 0（Attack・通常スキル）で、Defend には正の値を与える。
    /// Defend は「相手の攻撃を受ける前に軽減の構えを取る」行動であり、
    /// Speed のみで順序を決めると鈍足側の Defend が無意味になるため。
    /// </summary>
    public int Priority { get; }

    /// <summary>TP消費量。発動時に消費する。</summary>
    public ushort RequireTp { get; }

    /// <summary>motion_index 昇順に整列済みのモーション列。</summary>
    public IReadOnlyList<SkillMotion> Motions { get; }

    public Skill(
        string skillKey,
        string name,
        IEnumerable<SkillMotion> motions,
        int priority = 0,
        ushort requireTp = 0)
    {
        SkillKey = skillKey;
        Name = name;
        Priority = priority;
        RequireTp = requireTp;

        // 再生順は motion_index 昇順。呼び出し側の並びには依存しない
        Motions = motions.OrderBy(m => m.MotionIndex).ToList();
    }

    /// <summary>
    /// 味方対象のモーションを1つでも持つか。
    /// [対象] が味方に解決されたにもかかわらずこれが偽なら、入力が結果に反映されないため
    /// 空振りを通知する（戦闘システム 4.2）。スキルの静的な構成による判定であり、
    /// 実行時の失敗（命中失敗・対象状態によるスキップ・重複拒否）とは別レイヤーである。
    /// </summary>
    public bool HasAllyTargetMotion => Motions.Any(m => m.TargetRule == TargetRule.Ally);

    /// <summary>
    /// 指定グループの先頭モーション（motion_index 最小）。
    /// 依存先は常に先頭1件であり、直前のメンバーではない。
    /// メンバー同士が連鎖しないため、1攻撃から複数の状態変化を独立に付与できる。
    /// </summary>
    public SkillMotion? GateLeaderOf(ushort gateGroup)
        => Motions.FirstOrDefault(m => m.AccuracyGateGroup == gateGroup);
}
