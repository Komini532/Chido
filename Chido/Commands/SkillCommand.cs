using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b で実装] スキルを発動します。
/// </summary>
public sealed class SkillCommand : ISlashCommand
{
    public const string OptionSkillName = "skill-name";
    public const string OptionTarget = "target";

    public string Name => "skill";

    public string Description => "スキルを発動します。";

    public SlashCommandBuilder Build()
        => new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            .AddOption(OptionSkillName, ApplicationCommandOptionType.String, "発動するスキル名",
                isRequired: true, isAutocomplete: true)
            // [対象] はオートコンプリート付きの任意入力文字列（戦闘システム 9.2）。
            // 固定の選択式にしないのは、同時に複数体出現しうる敵から柔軟に指定できるようにするため
            .AddOption(OptionTarget, ApplicationCommandOptionType.String, "対象（省略可）",
                isRequired: false, isAutocomplete: true);

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
