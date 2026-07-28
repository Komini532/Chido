namespace Chido.Data.Entities;

/// <summary>
/// chido_player_in_battle_session (36): 参加中の戦闘セッション。
/// 1プレイヤー1行という構造により「同時参加は1セッションまで」がテーブル構造から導かれる。
/// 行の不在＝非戦闘中を意味し、NULL 表現を持たない。
///
/// 参加判定（全戦闘コマンドの入口で毎回走る最ホットパス）を user_id のPK直引きで解決するため、
/// PKは user_id とする。(session_id, user_id) の複合PKでは「1プレイヤー1セッション」の一意性が
/// 保証されず、かつ参加判定が全走査になる。
///
/// 行の削除タイミング:
///   自身が Escaped               … 当該 user_id の行のみ
///   セッション終了               … session_id で一括（end_reason を問わず、ChannelMissing を含む）
///   自身が Defeated              … 削除しない（拘束継続）
/// セッション終了時の一括削除が漏れると、チャンネル消失時に Defeated だったプレイヤーが
/// 永久に他の戦闘へ参加できなくなる。
/// </summary>
public class PlayerInBattleSessionRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_battle_session.session_id を参照。</summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// chido_battle_participant.entity_id を参照。
    /// (session_id, entity_id) によるPK直引きを可能にするための非正規化。
    /// これがないと chido_battle_participant に (session_id, user_id) の追加インデックスが必要になる。
    /// </summary>
    public Guid EntityId { get; set; }
}
