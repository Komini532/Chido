namespace Chido.Data.Entities;

/// <summary>
/// chido_player_equipment_slot: 装備装着状況。
/// 1プレイヤー1行、スロットごとに1カラムを持つ構造。
/// chido_battle_enemy_equipment_slot と完全に対称であり、プレイヤーと敵を共通の戦闘システムで扱う思想に基づく。
/// 本テーブルへの明示的な悲観ロックは不要（chido_player.user_id のロックアンカーに包摂される）。
/// </summary>
public class PlayerEquipmentSlotRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_player_equipment.instance_id を参照。武器スロット。</summary>
    public Guid? WeaponInstanceId { get; set; }

    /// <summary>頭防具スロット。</summary>
    public Guid? HeadInstanceId { get; set; }

    /// <summary>胴防具スロット。</summary>
    public Guid? ChestInstanceId { get; set; }

    /// <summary>脚防具スロット。</summary>
    public Guid? LegsInstanceId { get; set; }

    /// <summary>
    /// アクセサリスロット1。将来的な「アクセサリー2」追加を見越した番号付き命名で、
    /// 追加時は列を足すのみで完結し既存列のリネームは発生しない。
    /// </summary>
    public Guid? Accessory1InstanceId { get; set; }
}
