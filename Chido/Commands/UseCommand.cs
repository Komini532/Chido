using Discord.WebSocket;

namespace Chido.Commands;

public static class UseCommand
{
    public const string Name = "use";
    public const string Description = "アイテムを使用します。";
    public const string OptionItemName = "item-name";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command use is working!");
    }
}
