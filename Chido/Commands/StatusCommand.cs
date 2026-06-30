using Discord.WebSocket;

namespace Chido.Commands;

public static class StatusCommand
{
    public const string Name = "status";
    public const string Description = "ステータスを表示します。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command status is working!");
    }
}
