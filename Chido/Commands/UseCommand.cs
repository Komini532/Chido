using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b で実装] アイテムを使用します。
/// </summary>
public sealed class UseCommand : ISlashCommand
{
    public const string OptionItemName = "item-name";
    public const string OptionTarget = "target";

    public string Name => "use";

    public string Description => "アイテムを使用します。";

    public SlashCommandBuilder Build()
        => new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            .AddOption(OptionItemName, ApplicationCommandOptionType.String, "使用するアイテム名",
                isRequired: true, isAutocomplete: true)
            .AddOption(OptionTarget, ApplicationCommandOptionType.String, "対象（省略可）",
                isRequired: false, isAutocomplete: true);

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
