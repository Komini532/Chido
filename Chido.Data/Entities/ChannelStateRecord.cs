using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_channel_state (37): チャンネル単位の永続状態。
/// 行は戦闘チャンネル初期化コマンドの実行時に INSERT される。
/// PK重複により再実行は失敗し、これが冪等性を担保する
/// （初期化は「既にあるものを消去して作り直す」機能を伴わない）。
/// </summary>
public class ChannelStateRecord
{
    /// <summary>
    /// DiscordチャンネルID。行の存在自体が「このチャンネルは戦闘チャンネルである」ことを意味する。
    /// 常に行が存在するため、チャンネルに関する悲観ロックのアンカーとして使用する
    /// （全戦闘行動の直列化点。正準ロック順序の②）。
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>chido_field_master.field_key を参照。現在のフィールド。</summary>
    public string CurrentFieldKey { get; set; } = string.Empty;

    /// <summary>
    /// 累積敵レベル。初期値 1。敵の組を撃破するたびに +1（減少しない）。
    /// 出現する敵の level にそのまま複製される。
    /// この値が GameConstants.FieldTransitionPeriod の倍数に達するたびにフィールドが切り替わる
    /// （専用カウンターは持たない）。
    /// </summary>
    public BigInteger CumulativeEnemyLevel { get; set; }

    /// <summary>
    /// chido_battle_session.session_id を参照。NULL = 進行中のセッションなし。
    /// 1チャンネル1行という構造により「アクティブなセッションは1つ以下」が導かれ、
    /// セッション生成レースを本行のロックで直列化できる。
    /// </summary>
    public Guid? CurrentSessionId { get; set; }
}
