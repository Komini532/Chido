using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b/9c で実装] 所持アイテムを表示します。
/// </summary>
public sealed class InventoryCommand : ISlashCommand
{
    public string Name => "inventory";

    public string Description => "所持アイテムを表示します。";

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
