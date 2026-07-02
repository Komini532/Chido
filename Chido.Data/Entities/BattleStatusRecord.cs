using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_status: 戦闘関連ステータス。
/// 各種戦闘ステータス（攻撃力・防御力・素早さ等）はレベルから毎回算出するため、このテーブルには持たない。
/// </summary>
public class BattleStatusRecord
{
    /// <summary>chido_player.user_id を参照（DB上の明示的なFOREIGN KEY制約なし。設計書のSQLに準拠）。</summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// 経験値。レベルは √exp で算出。ランキング等でSQL側の比較・ソートが必要なため DECIMAL(65,0)。
    /// </summary>
    public BigInteger Exp { get; set; }

    /// <summary>
    /// 現在HP。最大値(MaxLife)はレベルから動的算出されるためDB単体で比較する意味がなく、
    /// 常にアプリ側でBigIntegerとして扱う前提の文字列型(VARCHAR(100))。
    /// </summary>
    public BigInteger CurrentHp { get; set; }
}
