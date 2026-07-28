using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_loots_master: 敵のドロップテーブル。
/// ドロップ判定は撃破に関与したプレイヤーごとに独立して行われる（戦闘システム 10.2）。
/// </summary>
public class EnemyLootsMasterRecord
{
    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>chido_item_master.item_key を参照。</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>ドロップ数量。</summary>
    public ushort Quantity { get; set; }

    /// <summary>ドロップ率。外れた場合は Luck% で1回だけ再抽選される（戦闘システム 10.2）。</summary>
    public Ratio DropRate { get; set; }
}
