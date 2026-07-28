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
    /// 経験値。レベルは √exp で算出（Chido.Core.Stats.LevelCalculator）。
    /// ランキング等でSQL側の比較・ソートが必要なため DECIMAL(65,0)。
    /// 初期値は GameConstants.InitialExp（= 1）。0 だと level=0 となり基礎ステータスが全て0になって成立しない。
    /// </summary>
    public BigInteger Exp { get; set; }

    // current_hp は持たない（設計ドキュメント 2番）。現在HPの真値は chido_battle_participant.current_hp のみで、
    // 戦闘ごとに全快する仕様のもとでは非戦闘時に保持すべき値が存在しない。
    // 参加中セッションの参照は chido_player_in_battle_session（36番）が担う。
}
