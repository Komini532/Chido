using Chido.Core.Battle.Damage;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_slip_damage_master (17): 状態変化のうち継続ダメージ成分。
/// ダメージ算出は攻撃モーションと同型（対象DEF・属性相性を考慮、最低1、クリティカルなし・DRRなし）で、
/// 戦闘システム 5.1 のスリップパイプラインを通す。
/// </summary>
public class EffectSlipDamageMasterRecord
{
    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 攻撃属性（ビット列）。マスタ由来のため付与後も不変であり、スナップショット対象ではない。
    /// </summary>
    public Element Elements { get; set; }

    /// <summary>
    /// 威力。整数%。非負。chido_skill_motion_attack_master.power と同一の概念・同一のスケール。
    /// リジェネ的な表現が必要になった場合は本テーブルを流用せず別テーブルを新設する。
    /// </summary>
    public ushort Power { get; set; }
}
