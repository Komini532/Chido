using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// アイテムの使用。
///
/// アイテムの効果は「特定スキルの発動」に収束するため、発動そのものは通常のスキル発動と
/// 同じ経路を通る（戦闘システム 4.2）。<b>習得状況は問わない</b>ため、
/// 習得していないスキルを撃たせるアイテムが成立する。
/// </summary>
public sealed class UseCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public const string OptionItem = "item-name";

    public override string Name => "use";

    public override string Description => "所持しているアイテムを使います。";

    protected override string Title => "アイテム";

    public override SlashCommandBuilder Build()
        => WithTarget(base.Build())
            .AddOption(
                OptionItem, ApplicationCommandOptionType.String, "使用するアイテム",
                isRequired: true, isAutocomplete: true);

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Use, itemKey: OptionOf(command, OptionItem));

    protected override async Task<IReadOnlyList<(string Label, string Value)>> ChoicesForAsync(
        SocketAutocompleteInteraction interaction, string optionName, string input)
        => optionName == OptionItem
            ? await Queries.ItemChoicesAsync(interaction.User.Id, input)
            : [];
}
