namespace Chido.Data.Entities;

/// <summary>
/// chido_player_title_display: 表示中の称号。
/// 1プレイヤー1行・単一カラムで現在値を保持することにより、
/// 「表示中の称号は常に1つ以下」という制約がテーブル構造そのものから導かれる。
/// 実際に表示可能なのは chido_player_title に存在する（＝獲得済みの）称号に限られるが、
/// その整合性の維持はアプリ側の責務とする。
/// </summary>
public class PlayerTitleDisplayRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_player_title.title_key を参照。NULL = 称号を表示しない。</summary>
    public string? TitleKey { get; set; }
}
