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
    /// 10進整数文字列として VARCHAR(100) に格納する（DECIMAL が使えない理由は BigIntegerToStringConverter 参照）。
    /// 初期値は GameConstants.InitialExp（= 1）。0 だと level=0 となり基礎ステータスが全て0になって成立しない。
    /// </summary>
    public BigInteger Exp { get; set; }

    /// <summary>
    /// <see cref="Exp"/> の桁数。ランキングの第1ソートキーとなる生成列（DB が算出する）。
    /// 並び替えは必ず Chido.Data.Queries.RankingQueries 経由で行うこと。
    /// </summary>
    public byte ExpLength { get; private set; }

    // current_hp は持たない（設計ドキュメント 2番）。現在HPの真値は chido_battle_participant.current_hp のみで、
    // 戦闘ごとに全快する仕様のもとでは非戦闘時に保持すべき値が存在しない。
    // 参加中セッションの参照は chido_player_in_battle_session（36番）が担う。
}
