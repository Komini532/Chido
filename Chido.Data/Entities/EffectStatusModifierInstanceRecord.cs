using Chido.Core.Battle.Effects;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_status_modifier_instance (21): インスタンス側のステータス変動。
/// chido_battle_effect と chido_player_effect の両方の instance_id を受け入れる共有テーブル。
/// GUIDのため衝突せず、親がどちらのテーブルかをサブテーブル側で区別する必要がない。
/// ただし親が2テーブルに分かれるため instance_id への FOREIGN KEY は張れない
/// （MySQLのFKは単一テーブルしか参照できない）。
///
/// effect_key は持たせない。instance_id から親テーブル経由で一意に辿れ、
/// エンティティ単位で状態変化を種別フィルタする要件も現時点で想定されないため。
/// </summary>
public class EffectStatusModifierInstanceRecord
{
    /// <summary>chido_battle_effect.instance_id または chido_player_effect.instance_id を参照。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>chido_effect_status_modifier_master.target_status に対応。</summary>
    public TargetStatus TargetStatus { get; set; }

    /// <summary>
    /// 実際の変動率。符号あり。
    /// chido_effect_status_modifier_master.fixed_rate が NULL の行のみここに実値を持つ。
    /// 値の出所は chido_skill_motion_effect_master.effect_rate または chido_enemy_effects_master.effect_rate。
    /// </summary>
    public Ratio Rate { get; set; }
}
