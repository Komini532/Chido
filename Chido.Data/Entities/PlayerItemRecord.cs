namespace Chido.Data.Entities;

/// <summary>
/// chido_player_item: プレイヤー所持アイテム。
/// </summary>
public class PlayerItemRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_item_master.item_key を参照。</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>所持数。</summary>
    public uint Quantity { get; set; }
}
