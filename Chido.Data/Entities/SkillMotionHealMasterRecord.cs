using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_motion_heal_master (10b): 回復モーション（現在HPへの干渉）。
/// 攻撃（10a）とは列構成も意味論も異なる（被防御係数・クリティカル・属性相性が攻撃にのみ適用されるため）。
/// </summary>
public class SkillMotionHealMasterRecord
{
    /// <summary>chido_skill_motion_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>chido_skill_motion_master.motion_index を参照。</summary>
    public byte MotionIndex { get; set; }

    /// <summary>常に MotionType.Heal。判別子を含む複合FKの構成列。</summary>
    public MotionType MotionType { get; set; } = MotionType.Heal;

    /// <summary>参照する攻撃力（物理／魔法）を選択する。</summary>
    public AttackType AttackType { get; set; }

    /// <summary>
    /// 威力。整数%。回復量 = 攻撃力 × 威力（対象の防御力は影響しない ＝ 被防御係数1の攻撃）。
    /// 同格では被防御係数が0.5になるため、通常攻撃(100%)と釣り合う回復は威力50%。
    /// 回復モーションは elements（モーション属性）を持たない。
    /// </summary>
    public ushort Power { get; set; }
}
