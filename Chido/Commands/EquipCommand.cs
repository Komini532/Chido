using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 装備を装着する（戦闘システム 2.5・9.1）。
///
/// <para>
/// <b>戦闘中でも実行できる。</b>ステータスは動的算出であるため、変更は次の参照から
/// 即座に反映される。そのぶんセッションとの排他が要るため、ロックは ① → ③ を取る
/// （②チャンネル行は飛ばす。装備は所有者本人にしか書き換えられない）。
/// </para>
/// <para>
/// 部位は指定しない。装備可能部位はビット列であり「複数スロットのいずれかを選んで
/// 装着できる」という択一の候補提示であるため、空いている適合部位のうち最も小さいものへ入る
/// （空きが無ければ最小の適合部位を置き換え、外した装備は所持に残る）。
/// </para>
/// </summary>
public sealed class EquipCommand(EquipmentService equipment, BattleQueries queries) : ISlashCommand
{
    public const string OptionEquipment = "equipment";

    public string Name => "equip";

    public string Description => "所持している装備を装着します。";

    public SlashCommandBuilder Build()
        => new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            .AddOption(
                OptionEquipment, ApplicationCommandOptionType.String, "装着する装備",
                isRequired: true, isAutocomplete: true);

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        // 戦闘中はセッション行のロック待ちが起こりうるため、一次応答を先に返す
        await command.DeferAsync();

        var selected = command.Data.Options
            .FirstOrDefault(o => o.Name == OptionEquipment)?.Value?.ToString();

        var outcome = await equipment.EquipAsync(
            command.User.Id, command.User.GlobalName ?? command.User.Username, selected);

        var embed = new EmbedBuilder()
            .WithTitle("装備")
            .WithColor(outcome.Accepted ? Color.Blue : Color.LightGrey)
            .WithDescription(Describe(outcome))
            .Build();

        await command.ModifyOriginalResponseAsync(m => m.Embed = embed);
    }

    public Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
        => queries.RespondWithEquipmentAsync(interaction);

    private static string Describe(EquipOutcome outcome)
    {
        if (outcome.Refusal is { } refusal) return refusal;

        var part = StatusCommand.PartName(outcome.Part);

        return outcome.Displaced is { } displaced
            ? $"{outcome.Name} を{part}に装着した（{displaced} を外した）。"
            : $"{outcome.Name} を{part}に装着した。";
    }
}
