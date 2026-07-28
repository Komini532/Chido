using Chido.Core.Battle;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_session: 戦闘セッション。
/// ラウンド／フェーズ／イニシアティブ順は意図的に持たない（ended_atの有無で進行状態を表現する）。
/// </summary>
public class BattleSessionRecord
{
    /// <summary>使い捨てGuid。プレイヤーの最初の戦闘行為時に新規発行される。</summary>
    public Guid SessionId { get; set; }

    /// <summary>戦闘が発生したDiscordサーバーID。</summary>
    public ulong GuildId { get; set; }

    /// <summary>戦闘が発生したチャンネルID。</summary>
    public ulong ChannelId { get; set; }

    /// <summary>戦闘状況を表示している埋め込みメッセージのID（編集対象）。</summary>
    public ulong? MessageId { get; set; }

    // last_action_at は持たない。Timeout による強制終了が廃止され（戦闘システム 6.1）、
    // 非同期設計では長時間放置そのものが許容されるため、読み出す箇所が存在しない。
    // セッションの経過時間は created_at、最終活動時刻が必要になれば chido_battle_log.created_at の最大値で得られる。

    /// <summary>
    /// セッション開始時刻。
    /// DATETIME(3)はタイムゾーン情報を持たないため、アプリ側で常にUTCとして読み書きすること。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>終了時刻。NULL=進行中、NOT NULL=終了（phase列の代わりにこれで進行状態を表現する）。</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// 終了理由。ended_atがNULLの間は常にNULL。
    /// 参加者の状態分布からは事後的に区別できないため、トリガー発火時点で明示的に記録する。
    /// 次に出現する敵の抽選ロジックを分岐させる（戦闘システム 10.3）。
    /// </summary>
    public BattleEndReason? EndReason { get; set; }
}
