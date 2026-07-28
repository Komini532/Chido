namespace Chido.Data.Entities;

/// <summary>
/// chido_player_title: 称号所持状況。
/// chido_player_item と同型の複合PKだが、称号は獲得済みか否かの二値であり個数を持たないため quantity 列は不要。
/// 入手経路も chido_title_master.acquisition_type で一意に決まるため、learned_reason のような記録列も設けない。
/// </summary>
public class PlayerTitleRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_title_master.title_key を参照。</summary>
    public string TitleKey { get; set; } = string.Empty;
}
