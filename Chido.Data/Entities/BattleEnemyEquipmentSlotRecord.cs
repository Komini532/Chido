namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_enemy_equipment_slot: 敵の装着状況。
/// chido_player_equipment_slot と完全に対称な構造。
/// ただし敵の装備は出現時の抽選で確定しセッション中に変化しないため、本テーブルは悲観ロックの対象外
/// （プレイヤー側との意図的な非対称）。
/// </summary>
public class BattleEnemyEquipmentSlotRecord
{
    /// <summary>chido_battle_enemy.enemy_id を参照。</summary>
    public Guid EnemyId { get; set; }

    /// <summary>chido_battle_enemy_equipment.instance_id を参照。武器スロット。</summary>
    public Guid? WeaponInstanceId { get; set; }

    /// <summary>頭防具スロット。</summary>
    public Guid? HeadInstanceId { get; set; }

    /// <summary>胴防具スロット。</summary>
    public Guid? ChestInstanceId { get; set; }

    /// <summary>脚防具スロット。</summary>
    public Guid? LegsInstanceId { get; set; }

    /// <summary>アクセサリスロット1。追加時はプレイヤー側と同時に列追加する運用。</summary>
    public Guid? Accessory1InstanceId { get; set; }
}
