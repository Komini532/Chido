using System.Numerics;
using Chido.Core.Battle.Damage;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_slip_damage_instance (22): インスタンス側の継続ダメージ。
/// 21番と同じく chido_battle_effect / chido_player_effect 双方の instance_id を受け入れる共有テーブル。
/// </summary>
public class EffectSlipDamageInstanceRecord
{
    /// <summary>chido_battle_effect.instance_id または chido_player_effect.instance_id を参照。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// 付与モーション（10c）または auto 付与（14番）から複製した静的な性質。
    /// 「付与時点の術者のステータスに依存する量」ではない（術者依存なのは status_attack_value のみ）。
    /// ダメージ計算時に対象の物理/魔法DEFのどちらを引くかを決めるために保持し続ける。
    /// </summary>
    public AttackType AttackType { get; set; }

    /// <summary>
    /// 付与時点の攻撃力実値のスナップショット。
    /// attack_type が指す側（物理/魔法）の付与者ATK（付与時の StatusModifier 込み）を格納する。
    /// </summary>
    public BigInteger StatusAttackValue { get; set; }
}
