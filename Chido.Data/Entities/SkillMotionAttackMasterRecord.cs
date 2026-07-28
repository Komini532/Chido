using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_motion_attack_master (10a): 攻撃モーション（現在HPへの干渉）。
/// 判別子 motion_type を含む複合FKにより、攻撃行が回復として登録される誤りをDBが弾く。
/// </summary>
public class SkillMotionAttackMasterRecord
{
    /// <summary>chido_skill_motion_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>chido_skill_motion_master.motion_index を参照。</summary>
    public byte MotionIndex { get; set; }

    /// <summary>常に MotionType.Attack。判別子を含む複合FKの構成列。</summary>
    public MotionType MotionType { get; set; } = MotionType.Attack;

    /// <summary>参照する攻撃力（物理／魔法）を選択する。</summary>
    public AttackType AttackType { get; set; }

    /// <summary>
    /// 威力。整数%（通常攻撃=100）。permyriad ではない点に注意（Ratio の対象外）。
    /// ダメージ = 攻撃力 × 威力 × 被防御係数(ATK÷(ATK+DEF))。
    /// </summary>
    public ushort Power { get; set; }

    /// <summary>
    /// モーション属性（ビット列）。攻撃モーションのみが持つ。
    /// ダメージ計算時に対象の実効属性との相性判定に使用される実効値。
    /// 0（属性なし）が意味を持つ既定値（相性計算をスキップ＝全属性等倍）。
    /// </summary>
    public Element Elements { get; set; }
}
