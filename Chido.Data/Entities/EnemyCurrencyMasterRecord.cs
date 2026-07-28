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
    /// 10進整数文字列として VARCHAR(100) に格納する。SQL側でのソートは不要なため桁数の生成列は持たない。
    /// </summary>
    public BigInteger DropAmount { get; set; }
}
