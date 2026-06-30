using Discord.WebSocket;

namespace Chido.Commands;

public static class AttackCommand
{
    public const string Name = "attack";
    public const string Description = "通常攻撃を行います。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command attack is working!");
    }
}
