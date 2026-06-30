using Discord.WebSocket;

namespace Chido.Commands;

public static class SkillCommand
{
    public const string Name = "skill";
    public const string Description = "スキルを発動します。";
    public const string OptionSkillName = "skill-name";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.RespondAsync("Command skill is working!");
    }
}
