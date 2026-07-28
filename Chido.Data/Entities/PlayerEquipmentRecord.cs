namespace Chido.Data.Entities;

/// <summary>
/// chido_player_equipment: 装備所持状況。
/// 同種の装備を複数所持できること、および将来的な個体差・強化付与の余地を見込み、
/// インスタンス単位の行として管理する。所持数が必要な場合は COUNT(*) で導出し、
/// chido_player_item.quantity のような専用カラムは持たない。
/// </summary>
public class PlayerEquipmentRecord
{
    /// <summary>使い捨てGuid。装備を入手する都度新規発行される。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>chido_player.user_id を参照。所有者。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_equipment_master.equip_key を参照。</summary>
    public string EquipKey { get; set; } = string.Empty;
}
