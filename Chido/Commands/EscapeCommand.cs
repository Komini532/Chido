using Discord.WebSocket;

namespace Chido.Commands;

public static class EscapeCommand
{
    public const string Name = "escape";
    public const string Description = "戦闘から離脱します。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command escape is working!");
    }
}
