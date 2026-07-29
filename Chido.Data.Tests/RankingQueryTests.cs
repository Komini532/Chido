using Chido.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// ランキングの並び順が SQL へどう落ちるかの検証。実DBには接続せず ToQueryString() で確認する。
///
/// 桁数の生成列を第1ソートキーに置き忘れても例外にはならず、静かに辞書順（"9" &gt; "10"）を返す。
/// その書き忘れを RankingQueries が構造的に防いでいることを、生成SQLの側から固定する。
/// </summary>
public class RankingQueryTests
{
    private static ChidoDbContext CreateContext()
        => ChidoDbContextFactory.CreateDbContext("Server=localhost;Database=ranking_test;User=u;Password=p;");

    [Fact]
    public void 経験値の降順は桁数を第1ソートキーにする()
    {
        using var db = CreateContext();

        var sql = db.BattleStatuses.OrderByExpDescending().Take(10).ToQueryString();

        Assert.Contains("ORDER BY `c`.`exp_len` DESC, `c`.`exp` DESC", sql);
    }

    [Fact]
    public void 経験値の昇順も桁数を第1ソートキーにする()
    {
        using var db = CreateContext();

        var sql = db.BattleStatuses.OrderByExp().Take(10).ToQueryString();

        Assert.Contains("ORDER BY `c`.`exp_len`, `c`.`exp`", sql);
    }

    [Fact]
    public void 所持金額の降順は桁数を第1ソートキーにする()
    {
        using var db = CreateContext();

        var sql = db.PlayerCurrencies.OrderByAmountDescending().Take(10).ToQueryString();

        Assert.Contains("ORDER BY `c`.`amount_len` DESC, `c`.`amount` DESC", sql);
    }

    [Fact]
    public void 所持金額の昇順も桁数を第1ソートキーにする()
    {
        using var db = CreateContext();

        var sql = db.PlayerCurrencies.OrderByAmount().Take(10).ToQueryString();

        Assert.Contains("ORDER BY `c`.`amount_len`, `c`.`amount`", sql);
    }

    [Fact]
    public void 素の並べ替えは桁数を見ないため誤った順序になる()
    {
        // 対比。この SQL は "9" > "10" となる辞書順であり、数値順ではない。
        // RankingQueries を経由しない書き方が誤りであることの根拠として残す
        using var db = CreateContext();

        var sql = db.BattleStatuses.OrderByDescending(x => x.Exp).Take(10).ToQueryString();

        Assert.Contains("ORDER BY `c`.`exp` DESC", sql);
        Assert.DoesNotContain("exp_len", sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..]);
    }
}
