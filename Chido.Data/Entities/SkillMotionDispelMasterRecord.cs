using Chido.Core.Battle.Skills;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_motion_dispel_master (10d): 状態変化解除モーション。
/// 列構成が付与（10c）の真部分集合であるため統合せず分けている。
/// 「解除 → 付与」の2モーション構成により、リフレッシュ挙動がデータ側から合成可能になる
/// （重複付与時の挙動として「拒否」をデフォルトに選べる根拠でもある）。
/// </summary>
public class SkillMotionDispelMasterRecord
{
    /// <summary>chido_skill_motion_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>chido_skill_motion_master.motion_index を参照。</summary>
    public byte MotionIndex { get; set; }

    /// <summary>常に MotionType.DispelEffect。判別子を含む複合FKの構成列。</summary>
    public MotionType MotionType { get; set; } = MotionType.DispelEffect;

    /// <summary>
    /// 解除対象。chido_effect_master.effect_key を参照。
    /// 対象が保持する全スコープ（chido_battle_effect + chido_player_effect）から
    /// effect_key が一致する行をすべて削除する。
    /// granter_entity_id / grant_source_key / affect_reason は参照しない
    /// （解毒は毒の出所を問わないため。付与の重複判定とは意図的に非対称）。
    /// </summary>
    public string EffectKey { get; set; } = string.Empty;
}
