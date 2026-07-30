using Chido.Rendering;
using Xunit;

namespace Chido.Tests.Rendering;

/// <summary>
/// 文字数予算による縮退の検証（戦闘システム 3.1・C-4）。
/// </summary>
public class EmbedBudgetTests
{
    [Fact]
    public void 予算に収まっていれば何も削らない()
    {
        var result = EmbedBudget.Fit(
            protectedText: [],
            sections: [new BudgetSection("ログ", ["a", "b", "c"])]);

        Assert.False(result.Truncated);
        Assert.Equal(["a", "b", "c"], result.Sections[0].Lines);
    }

    [Fact]
    public void 予算を超えると先頭から削られる()
    {
        // 直近の出来事ほど読みたい情報であり、古いログから削るのが最も損失が小さい
        var lines = Enumerable.Range(0, 100).Select(i => new string('x', 100) + i).ToList();

        var result = EmbedBudget.Fit([], [new BudgetSection("ログ", lines)]);

        Assert.True(result.Truncated);
        Assert.NotEmpty(result.Sections[0].Lines);
        // 残ったのは末尾側（新しい方）
        Assert.Equal(lines[^1], result.Sections[0].Lines[^1]);
        Assert.DoesNotContain(lines[0], result.Sections[0].Lines);
    }

    [Fact]
    public void 削る順序は渡した順になる()
    {
        // 戦闘ログ（古いものから）→ 状態変化の詳細 → 敵の詳細
        var logs = Enumerable.Range(0, 60).Select(i => new string('L', 100) + i).ToList();
        var status = Enumerable.Range(0, 5).Select(i => new string('S', 100) + i).ToList();

        var result = EmbedBudget.Fit(
            [],
            [new BudgetSection("ログ", logs), new BudgetSection("状態", status)]);

        Assert.True(result.Truncated);
        // ログが削られる一方で、状態は無傷のまま残る
        Assert.True(result.Sections[0].Lines.Count < logs.Count);
        Assert.Equal(status.Count, result.Sections[1].Lines.Count);
    }

    [Fact]
    public void 縮退対象外の文字列は予算から先に差し引かれる()
    {
        // 縮退通知が真っ先に切り落とされると通知の目的そのものが失われるため、
        // 予算計算には固定長として最初から算入する
        var notice = new string('N', 5000);
        var lines = Enumerable.Range(0, 30).Select(i => new string('x', 100) + i).ToList();

        var withNotice = EmbedBudget.Fit([notice], [new BudgetSection("ログ", lines)]);
        var without = EmbedBudget.Fit([], [new BudgetSection("ログ", lines)]);

        // 通知の分だけ本文に使える予算が減る
        Assert.True(withNotice.Sections[0].Lines.Count < without.Sections[0].Lines.Count);
    }

    [Fact]
    public void 縮退後は通知ぶんを含めても上限を超えない()
    {
        var lines = Enumerable.Range(0, 200).Select(i => new string('x', 100) + i).ToList();

        var result = EmbedBudget.Fit([], [new BudgetSection("ログ", lines)]);

        var total = result.Sections.Sum(s => s.Lines.Sum(l => l.Length + 1))
                    + (result.Truncated ? EmbedBudget.TruncationNotice.Length + 1 : 0);

        Assert.True(total <= EmbedBudget.TotalLimit, $"合計 {total} 文字が上限を超えている");
    }

    [Fact]
    public void 空のセクションでも落ちない()
    {
        var result = EmbedBudget.Fit([], [new BudgetSection("ログ", [])]);

        Assert.False(result.Truncated);
        Assert.Empty(result.Sections[0].Lines);
    }
}
