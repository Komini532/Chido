using Chido.Battle;
using Chido.Rendering;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 戦闘行動コマンドの共通処理。
///
/// <para>
/// <b><c>DeferAsync()</c> → <c>ModifyOriginalResponseAsync()</c> は必須。</b>
/// チャンネル行のロック待ちは同時行動人数に比例して伸びるため、Discord が要求する
/// 3秒以内の一次応答を計算完了まで待って返すことはできない（戦闘システム 7.3）。
/// これは「共有の進捗メッセージを編集する」こととは別物で、<b>そのインタラクション自身の
/// 応答プレースホルダを埋める</b>操作である。結果として「1行動＝1メッセージ」も同時に満たす。
/// </para>
/// <para>
/// <b>ephemeral にはしない。</b>行動レスポンスと進捗表示を同一メッセージへ集約する方針
/// （B-3・B-4）であり、重複拒否や <c>[対象]</c> の空振りといった行動者向けの通知も
/// 同じメッセージの末尾に載る。
/// </para>
/// </summary>
public abstract class BattleCommandBase(BattleService battles, BattleQueries queries) : ISlashCommand
{
    /// <summary><c>[対象]</c> のオプション名。全コマンドで共通。</summary>
    public const string OptionTarget = "target";

    public abstract string Name { get; }

    public abstract string Description { get; }

    /// <summary>成立時の埋め込みの見出し。</summary>
    protected abstract string Title { get; }

    public virtual SlashCommandBuilder Build()
        => new SlashCommandBuilder().WithName(Name).WithDescription(Description);

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var request = BuildRequest(command);
        var outcome = await battles.ExecuteAsync(request);

        var embed = BattleEmbed.Build(
            outcome.Message,
            outcome.Accepted ? Title : $"{Title}（不成立）",
            outcome.Accepted ? Color.Blue : Color.LightGrey);

        await command.ModifyOriginalResponseAsync(m => m.Embed = embed);
    }

    public virtual async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var focused = interaction.Data.Current;
        var input = focused.Value?.ToString() ?? string.Empty;

        var choices = focused.Name switch
        {
            OptionTarget => await queries.TargetChoicesAsync(interaction.ChannelId ?? 0, input),
            _ => await ChoicesForAsync(interaction, focused.Name, input),
        };

        // Discord のオートコンプリートは最大25件
        await interaction.RespondAsync(
            choices.Take(25).Select(c => new AutocompleteResult(c.Label, c.Value)));
    }

    /// <summary><c>[対象]</c> 以外のオプションの候補。既定は候補なし。</summary>
    protected virtual Task<IReadOnlyList<(string Label, string Value)>> ChoicesForAsync(
        SocketAutocompleteInteraction interaction, string optionName, string input)
        => Task.FromResult<IReadOnlyList<(string, string)>>([]);

    protected abstract BattleActionRequest BuildRequest(SocketSlashCommand command);

    /// <summary>コマンド共通の文脈。ギルド外での実行では GuildId が 0 になる。</summary>
    protected static BattleActionRequest NewRequest(
        SocketSlashCommand command,
        BattleActionKind kind,
        string? skillKey = null,
        string? itemKey = null)
        => new(
            kind,
            command.GuildId ?? 0,
            command.ChannelId ?? 0,
            command.User.Id,
            command.User.GlobalName ?? command.User.Username,
            skillKey,
            itemKey,
            OptionOf(command, OptionTarget));

    protected static string? OptionOf(SocketSlashCommand command, string name)
        => command.Data.Options.FirstOrDefault(o => o.Name == name)?.Value?.ToString();

    /// <summary>
    /// <c>[対象]</c> のオプションを足す。オートコンプリート付きの任意入力文字列であり、
    /// 固定の選択式にしないのは、同時に複数体出現しうる敵から柔軟に指定できるようにするため
    /// （戦闘システム 9.2）。
    /// </summary>
    protected static SlashCommandBuilder WithTarget(SlashCommandBuilder builder)
        => builder.AddOption(
            OptionTarget, ApplicationCommandOptionType.String, "対象（省略可）",
            isRequired: false, isAutocomplete: true);

    /// <summary>基底が握るプロパティ。派生から候補の問い合わせに使う。</summary>
    protected BattleQueries Queries => queries;
}
