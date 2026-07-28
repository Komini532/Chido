using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_motion_effect_master (10c): 状態変化付与モーション。
/// 1行が1つの effect_key を指し、その effect_key は effect_types でマルチネイチャーを取りうるため、
/// 性質ごとに排他的に振り分ける判別子が存在しない。ゆえにサブタイプ分割ではなく NULL 許容列で表現する。
/// </summary>
public class SkillMotionEffectMasterRecord
{
    /// <summary>chido_skill_motion_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>chido_skill_motion_master.motion_index を参照。</summary>
    public byte MotionIndex { get; set; }

    /// <summary>常に MotionType.GrantEffect。判別子を含む複合FKの構成列。</summary>
    public MotionType MotionType { get; set; } = MotionType.GrantEffect;

    /// <summary>付与する状態変化。chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 効果量。符号あり（デバフの負値を許容）。
    /// 付与先の chido_effect_status_modifier_master.fixed_rate が NULL の行に対してのみ必須。
    /// SlipDamage／DisableMove の効果量はそれぞれのマスタが持つため本列を使用しない。
    /// 要否が複数テーブルにまたがる条件で決まるため、整合性の担保はアプリ側の責務。
    /// </summary>
    public Ratio? EffectRate { get; set; }

    /// <summary>
    /// 付与する状態変化が SlipDamage 成分を持つ場合に、継続ダメージが物理／魔法どちらの攻撃力を
    /// 基準にするかを決める。付与時に chido_effect_slip_damage_instance.attack_type へ複製される。
    /// 攻撃力の実値ではなく「どちらの攻撃力を読むか」という静的な性質を表す。
    /// SlipDamage 成分を持たない付与では NULL（テーブルまたぎのためアプリ側の責務）。
    /// </summary>
    public AttackType? AttackType { get; set; }

    /// <summary>
    /// 付与する状態変化の持続。「残り有効行動数」であり時計ではない。
    /// chido_battle_effect / chido_player_effect の remaining_actions の初期値として複製される。
    /// NULL = 無期限。0 は取らない。付与先 effect の clear_on_battle_end = 0 の場合は NOT NULL 必須
    /// （永続スコープの状態変化は必ず有限。テーブルまたぎのためアプリ側の責務）。
    /// </summary>
    public ushort? DurationActions { get; set; }
}
