using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_effects_master: 敵の初期付与状態変化。
/// 出現インスタンス（enemy_id）ではなく種別定義（enemy_key）に紐づく点に注意。
/// 実際の付与インスタンスは戦闘開始時に chido_battle_effect へ affect_reason=Auto で書き込まれる。
///
/// duration_actions と attack_type を本テーブルにも持つのは、状態変化の付与元が
/// 「スキルモーション」と「敵の初期付与（auto）」の2つあり、付与時に決まる性質を
/// モーション側にしか置かないと auto 付与側でその出所が消えるため
/// （例:「6行動で自滅する敵」は auto 付与の SlipDamage として表現するのが自然）。
/// </summary>
public class EnemyEffectsMasterRecord
{
    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>付与順序。</summary>
    public byte EnemyEffectIndex { get; set; }

    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 効果量。符号あり（デバフの負値を許容）。
    /// chido_skill_motion_effect_master.effect_rate と同じ性質・同じ書き込み先。
    /// </summary>
    public Ratio EffectRate { get; set; }

    /// <summary>
    /// 付与する状態変化が SlipDamage 成分を持つ場合のみ NOT NULL。
    /// 付与時に chido_effect_slip_damage_instance.attack_type へ複製される。
    /// SlipDamage 成分を持たない付与では NULL（テーブルまたぎのためアプリ側の責務）。
    /// </summary>
    public AttackType? AttackType { get; set; }

    /// <summary>
    /// 持続。「残り有効行動数」であり時計ではない。
    /// NULL = 無期限（戦闘終了まで持続。敵の効果は clear_on_battle_end によらず戦闘終了時に除去される）。
    /// 0 は取らない。
    /// </summary>
    public ushort? DurationActions { get; set; }

    /// <summary>付与確率。</summary>
    public Ratio GrantRate { get; set; }
}
