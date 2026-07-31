using Chido.Core.Battle;
using Chido.Core.Battle.Effects;

namespace Chido.Rendering;

/// <summary>
/// 1回の戦闘行動ぶんのメッセージ（戦闘システム 3.1・B-3・B-4）。
///
/// <para>
/// <b>進捗メッセージの編集は行わない。</b>1回の戦闘行動につき1つの新規メッセージを送り、
/// 行動レスポンスと進捗表示を同一メッセージへ集約する。重複付与の拒否・<c>[対象]</c> の空振り・
/// 縮退の通知も同じメッセージに載せる（ephemeral の個別返信は使わない）。
/// レスポンスがそのまま1行動ごとの結果として可視化される利点を取り、
/// メッセージ数が増えることは承知の上で受け入れる。
/// </para>
/// <para>
/// <b>表示スコープは「そのターンで状態が変化したエンティティのみ」。</b>
/// 1ターンが1対1で完結するため、この集合は構造的に最大3体に収まる
/// （行動者 + 反撃者 + 味方対象モーションで影響を受けた1体）。
/// 行動種別ごとの表を持たず、実際に変化したかどうかで決めるため、
/// 味方対象モーションを含むスキルも自然に同じ規則で扱える。
/// </para>
/// </summary>
public sealed class BattleMessage
{
    private readonly List<string> logs = [];
    private readonly List<string> notices = [];
    private readonly List<BattleParticipant> changed = [];

    /// <summary>ターン中のログ。実行順に並ぶ。</summary>
    public IReadOnlyList<string> Logs => logs;

    /// <summary>
    /// 行動者へ伝える個別事項（重複拒否・<c>[対象]</c> の空振り・解決不能）。
    ///
    /// 埋め込みは共有表示であり「あなたが付与したもの」という閲覧者依存の情報を載せる手段が
    /// 原理的に存在しないため、これらの通知は行動レスポンス側にしか置けない。
    /// 通知がなければプレイヤーには「スキルが不発になった」としか映らない。
    /// </summary>
    public IReadOnlyList<string> Notices => notices;

    /// <summary>そのターンで状態が変化したエンティティ。</summary>
    public IReadOnlyList<BattleParticipant> ChangedParticipants => changed;

    /// <summary>
    /// 縮退通知（マスタ不整合による草原フォールバック）。
    /// <b>文字数縮退の対象外</b>であり、予算には固定長として最初から算入される。
    /// </summary>
    public List<string> DegradationNotices { get; } = [];

    public BattleMessage AddLogs(IEnumerable<string> entries)
    {
        logs.AddRange(entries);
        return this;
    }

    public BattleMessage AddNotice(string? notice)
    {
        if (!string.IsNullOrEmpty(notice)) notices.Add(notice);
        return this;
    }

    /// <summary>状態が変化したエンティティとして記録する。同じ参加者は1度だけ。</summary>
    public BattleMessage MarkChanged(params BattleParticipant?[] participants)
    {
        foreach (var participant in participants)
        {
            if (participant is null) continue;
            if (changed.Contains(participant)) continue;

            changed.Add(participant);
        }

        return this;
    }

    /// <summary>
    /// 表示用のセクションへ組み立て、文字数予算に収める。
    /// </summary>
    /// <param name="nameOf">effect_key → 表示名。</param>
    public RenderedBattleMessage Render(Func<string, string> nameOf)
    {
        var statusLines = changed
            // 状態変化の表示対象は Active な参加者に限る
            .Where(p => p.IsActive)
            .Select(p => RenderParticipant(p, nameOf))
            .ToList();

        // 削る順序は 戦闘ログ（古いものから）→ 状態変化の詳細 → 敵の詳細。
        // 直近の出来事ほど読みたい情報であり、古いログから削るのが最も損失が小さい
        var budget = EmbedBudget.Fit(
            protectedText: [.. DegradationNotices, .. notices],
            sections:
            [
                new BudgetSection("ログ", logs),
                new BudgetSection("状態", statusLines),
            ]);

        var trailing = new List<string>(notices);
        trailing.AddRange(DegradationNotices);
        if (budget.Truncated) trailing.Add(EmbedBudget.TruncationNotice);

        return new RenderedBattleMessage(
            budget.Sections[0].Lines,
            budget.Sections[1].Lines,
            trailing);
    }

    private static string RenderParticipant(BattleParticipant participant, Func<string, string> nameOf)
    {
        var life = $"HP {participant.Entity.CurrentLife}/{participant.Entity.MaxLife}";

        var effects = participant.Entity is Core.Entities.EntityBase entity
            ? EffectDisplay.Render(entity.Effects, nameOf)
            : [];

        return effects.Count == 0
            ? $"{participant.Entity.Name}: {life}"
            : $"{participant.Entity.Name}: {life} {string.Join(" ", effects)}";
    }
}

/// <summary>
/// 描画済みのメッセージ。Discord の埋め込みへ落とすのは呼び出し側の責務。
/// </summary>
/// <param name="Trailing">
/// 末尾に置く通知。個別事項・縮退通知・省略通知の順。いずれも文字数縮退の対象外。
/// </param>
public readonly record struct RenderedBattleMessage(
    IReadOnlyList<string> Logs,
    IReadOnlyList<string> Status,
    IReadOnlyList<string> Trailing);
