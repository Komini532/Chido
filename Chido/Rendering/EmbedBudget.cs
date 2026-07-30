namespace Chido.Rendering;

/// <summary>
/// Discord の文字数予算による縮退（戦闘システム 3.1・C-4）。
///
/// <para>
/// 埋め込みには「フィールド数25 / フィールド値1024文字 / <b>メッセージ内の全埋め込み合計6000文字</b>」
/// という制限があり、複数の埋め込みに分割しても合計6000は回避できない。
/// 件数上限を満たしていても文字数予算を超えうるため、描画層の最後の砦としてここで削る。
/// </para>
/// <para>
/// <b>縮退通知は縮退の対象外</b>とし、予算計算には固定長として最初から算入する。
/// マスタ不整合を伝える一文が真っ先に切り落とされると、通知の目的そのものが失われるため。
/// </para>
/// <para>
/// 削る順序は <b>戦闘ログ（古いものから）→ 状態変化の詳細 → 敵の詳細</b> に固定する。
/// 直近の出来事ほど読みたい情報であり、古いログから削るのが最も損失が小さい。
/// なお1行動につき1メッセージを送る方式（B-3・B-4）により、そもそも予算超過は起きにくい。
/// </para>
/// </summary>
public static class EmbedBudget
{
    /// <summary>メッセージ内の全埋め込みの合計文字数の上限。</summary>
    public const int TotalLimit = 6000;

    /// <summary>削られたことを伝える一文。これ自体は縮退の対象外。</summary>
    public const string TruncationNotice = "※表示量が上限に達したため、一部を省略しました。";

    /// <summary>
    /// 予算に収まるまで、指定された順序でセクションを削る。
    /// </summary>
    /// <param name="protectedText">
    /// 縮退の対象外にする文字列（縮退通知など）。予算から先に差し引く。
    /// </param>
    /// <param name="sections">
    /// 削ってよいセクション。<b>渡した順に削る</b>ため、呼び出し側が
    /// 「戦闘ログ → 状態変化 → 敵の詳細」の順で並べること。
    /// </param>
    /// <returns>縮退後のセクションと、削除が発生したか。</returns>
    public static BudgetResult Fit(
        IEnumerable<string> protectedText, IReadOnlyList<BudgetSection> sections)
    {
        var reserved = protectedText.Sum(t => t.Length + 1);
        var budget = TotalLimit - reserved;

        var lines = sections.Select(s => s.Lines.ToList()).ToList();
        var truncated = false;

        // 削除が発生した時点で通知の一文が増えるため、その分を先に見込んでおく。
        // 削り切った後に通知を足して再び超過する、という往復を避ける
        var noticeCost = TruncationNotice.Length + 1;

        for (var i = 0; i < lines.Count && Total(lines) > budget; i++)
        {
            var effectiveBudget = budget - noticeCost;

            // 先頭（古いもの）から削る
            while (lines[i].Count > 0 && Total(lines) > effectiveBudget)
            {
                lines[i].RemoveAt(0);
                truncated = true;
            }
        }

        var result = sections
            .Select((s, i) => new BudgetSection(s.Name, lines[i]))
            .ToList();

        return new BudgetResult(result, truncated);
    }

    private static int Total(IEnumerable<List<string>> sections)
        => sections.Sum(lines => lines.Sum(l => l.Length + 1));
}

/// <summary>縮退の対象となるセクション1つ。</summary>
/// <param name="Lines">行。先頭ほど古く、削るときは先頭から落とす。</param>
public readonly record struct BudgetSection(string Name, IReadOnlyList<string> Lines);

/// <param name="Truncated">
/// 削除が発生したか。真なら <see cref="EmbedBudget.TruncationNotice"/> を添える。
/// </param>
public readonly record struct BudgetResult(IReadOnlyList<BudgetSection> Sections, bool Truncated);
