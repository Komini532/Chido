using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 習得済みスキルの発動。
///
/// 通常攻撃と防御は習得管理の対象外であり、専用のコマンドから使う
/// （<c>chido_player_skill</c> に行を持たないため、ここでは候補にも上がらない）。
/// </summary>
public sealed class SkillCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public const string OptionSkill = "skill-name";

    public override string Name => "skill";

    public override string Description => "習得しているスキルを使います。";

    protected override string Title => "スキル";

    public override SlashCommandBuilder Build()
        => WithTarget(base.Build())
            .AddOption(
                OptionSkill, ApplicationCommandOptionType.String, "使用するスキル",
                isRequired: true, isAutocomplete: true);

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Skill, skillKey: OptionOf(command, OptionSkill));

    protected override async Task<IReadOnlyList<(string Label, string Value)>> ChoicesForAsync(
        SocketAutocompleteInteraction interaction, string optionName, string input)
        => optionName == OptionSkill
            ? await Queries.SkillChoicesAsync(interaction.User.Id, input)
            : [];
}
