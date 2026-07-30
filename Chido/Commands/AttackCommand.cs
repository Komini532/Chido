using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b で実装] 通常攻撃を行います。
/// </summary>
public sealed class AttackCommand : ISlashCommand
{
    public const string OptionTarget = "target";

    public string Name => "attack";

    public string Description => "通常攻撃を行います。";

    public SlashCommandBuilder Build()
        => new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            // [対象] はオートコンプリート付きの任意入力文字列（戦闘システム 9.2）。
            // 固定の選択式にしないのは、同時に複数体出現しうる敵から柔軟に指定できるようにするため
            .AddOption(OptionTarget, ApplicationCommandOptionType.String, "対象（省略可）",
                isRequired: false, isAutocomplete: true);

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
