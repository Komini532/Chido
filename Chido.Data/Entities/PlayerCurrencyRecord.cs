using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_player_currency: プレイヤー所持金。
/// 将来的に通貨単位を増やす場合は本テーブルにカラムを追加する運用とする。
/// 金額は10進整数文字列として格納されるため SQL 側での加減算はできない。
/// 読み出して BigInteger で計算し、書き戻す（同時更新の直列化は正準ロック順序のアンカーが担う）。
/// </summary>
public class PlayerCurrencyRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// 所持金額。10進整数文字列として VARCHAR(100) に格納する
    /// （chido_battle_status.exp と同じ判断基準）。
    /// </summary>
    public BigInteger Amount { get; set; }

    /// <summary>
    /// <see cref="Amount"/> の桁数。ランキングの第1ソートキーとなる生成列（DB が算出する）。
    /// 並び替えは必ず Chido.Data.Queries.RankingQueries 経由で行うこと。
    /// </summary>
    public byte AmountLength { get; private set; }
}
