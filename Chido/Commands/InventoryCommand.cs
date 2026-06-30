using Discord.WebSocket;

namespace Chido.Commands;

public static class InventoryCommand
{
    public const string Name = "inventory";
    public const string Description = "所持アイテムの一覧を表示します。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command inventory is working!");
    }
}
