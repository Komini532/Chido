namespace Chido.Data.Entities;

/// <summary>
/// chido_player: プレイヤー基本情報。
/// </summary>
public class PlayerRecord
{
    /// <summary>Discordユーザーの永続ID（スノーフレーク）。</summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// 表示名のキャッシュ。Discord APIから毎回引くとレイテンシが大きいため保持。
    /// 将来的にニックネーム機能にも転用可能。
    /// </summary>
    public string? UserName { get; set; }
}
