using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_player_currency: プレイヤー所持金。
/// 将来的に通貨単位を増やす場合は本テーブルにカラムを追加する運用とする。
/// 加減算は UPDATE ... SET amount = amount ± X で完結し、InnoDBの行ロックにより同時更新も自然に直列化される。
/// </summary>
public class PlayerCurrencyRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// 所持金額。ランキング等でSQL側の比較・ソートが必要なため DECIMAL(65,0) UNSIGNED
    /// （chido_battle_status.exp と同じ判断基準）。
    /// </summary>
    public BigInteger Amount { get; set; }
}
