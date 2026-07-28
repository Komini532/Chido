using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_currency_master: 敵ドロップ金額マスタ。
/// </summary>
public class EnemyCurrencyMasterRecord
{
    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>
    /// 撃破時に確定でドロップする金額（固定値、抽選なし）。
    /// 手動設定される基礎値であり蓄積後の所持金額そのものではないため、
    /// chido_player_currency.amount の型とは独立した判断で DECIMAL(65,0) UNSIGNED としている。
    /// </summary>
    public BigInteger DropAmount { get; set; }
}
