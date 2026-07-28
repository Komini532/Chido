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

    /// <summary>
    /// 最終行動時刻。放置タイムアウト判定に使用。
    /// DATETIME(3)はタイムゾーン情報を持たないため、アプリ側で常にUTCとして読み書きすること。
    /// </summary>
    public DateTime LastActionAt { get; set; }

    /// <summary>セッション開始時刻（UTC想定）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>終了時刻。NULL=進行中、NOT NULL=終了（phase列の代わりにこれで進行状態を表現する）。</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>終了理由。ended_atがNULLの間は常にNULL。</summary>
    public BattleEndReason? EndReason { get; set; }
}
