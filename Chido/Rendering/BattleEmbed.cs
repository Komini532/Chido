using Discord;

namespace Chido.Rendering;

/// <summary>
/// 描画済みメッセージを Discord の埋め込みへ落とす。
///
/// <para>
/// <b>1回の戦闘行動につき1つの新規メッセージを送る</b>（B-3・B-4）。編集し続ける共有の
/// 進捗メッセージは存在しないため、ここで組み立てた埋め込みはそのインタラクション自身の
/// 応答としてのみ使われる。
/// </para>
/// </summary>
public static class BattleEmbed
{
    /// <summary>セクションが空でも埋め込みが成立するようにするための本文。</summary>
    private const string Empty = "—";

    public static Embed Build(RenderedBattleMessage message, string title, Color color)
    {
        var builder = new EmbedBuilder()
            .WithTitle(Truncate(title, EmbedBuilder.MaxTitleLength))
            .WithColor(color);

        // 予算計算（EmbedBudget）は行の総量に対して行われているため、
        // ここでの分割は上限の内側に収まっている
        builder.WithDescription(Join(message.Logs));

        if (message.Status.Count > 0)
        {
            builder.AddField("状態", Join(message.Status));
        }

        if (message.Trailing.Count > 0)
        {
            builder.AddField("通知", Join(message.Trailing));
        }

        return builder.Build();
    }

    private static string Join(IReadOnlyList<string> lines)
        => lines.Count == 0 ? Empty : string.Join("\n", lines);

    private static string Truncate(string value, int limit)
        => value.Length <= limit ? value : value[..limit];
}
