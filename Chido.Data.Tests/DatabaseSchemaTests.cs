using System.Data;
using System.Data.Common;
using System.Numerics;
using Chido.Data.Entities;
using Chido.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 実DB（MySQL 8.4）に対する検証。<see cref="SchemaTests"/> / <see cref="RankingQueryTests"/> が
/// EF Core のモデルと生成SQLの側から固定している内容のうち、<b>DBに投げて初めて分かる部分</b>だけを扱う。
///
/// <para>
/// 具体的には (1) マイグレーションのDDLが空のDBに対して通ること、
/// (2) 桁数の生成列をDBが実際に算出すること、
/// (3) ランキングのORDER BYが辞書順ではなく数値順を返すこと、
/// (4) その並びがインデックスの逆走査で処理され filesort に落ちないこと。
/// いずれもモデルの検査では確認できず、従来は手元での手作業に頼っていた
/// （Chido.Data/Migrations/README.md「実DBに対する適用の確認」）。
/// </para>
/// <para>
/// 接続先が無い環境ではスキップされる。<see cref="DatabaseFactAttribute"/> を参照。
/// </para>
/// </summary>
public class DatabaseSchemaTests : IClassFixture<DatabaseFixture>
{
    /// <summary>設計ドキュメントの採番1〜45番＋スキルモーションのサブタイプ 10a〜10d。</summary>
    private const int ExpectedTableCount = 49;

    private readonly DatabaseFixture fixture;

    public DatabaseSchemaTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [DatabaseFact]
    public async Task マイグレーションが空のDBに適用できる()
    {
        await using var db = await fixture.CreateContextAsync();

        // フィクスチャがDBを破棄してから MigrateAsync() を実行済み。ここではその結果を見る
        var pending = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }

    [DatabaseFact]
    public async Task 設計ドキュメントの全テーブルが実DBに作成される()
    {
        await using var db = await fixture.CreateContextAsync();

        var tables = await QueryTableNamesAsync(db);

        // __EFMigrationsHistory は EF Core の管理テーブルであり設計の採番外
        var designTables = tables.Where(t => t != "__EFMigrationsHistory").ToList();

        Assert.Equal(ExpectedTableCount, designTables.Count);
    }

    [DatabaseFact]
    public async Task 桁数の生成列をDBが算出する()
    {
        await using var db = await fixture.CreateContextAsync();
        await TruncateAsync(db, "chido_battle_status", "chido_player_currency");

        // 生成列は STORED。アプリ側は書き込まず、DBが CHAR_LENGTH() で埋める
        db.BattleStatuses.Add(new BattleStatusRecord { UserId = 1, Exp = BigInteger.Parse("123456789") });
        db.PlayerCurrencies.Add(new PlayerCurrencyRecord { UserId = 1, Amount = 42 });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var status = await db.BattleStatuses.SingleAsync();
        var currency = await db.PlayerCurrencies.SingleAsync();

        Assert.Equal(9, status.ExpLength);
        Assert.Equal(2, currency.AmountLength);
    }

    [DatabaseFact]
    public async Task 経験値ランキングが辞書順ではなく数値順を返す()
    {
        await using var db = await fixture.CreateContextAsync();
        await TruncateAsync(db, "chido_battle_status");

        // 辞書順なら "9" > "10" > "100" となり、降順の先頭は 9 になる
        foreach (var (userId, exp) in new (ulong, string)[]
                 {
                     (1, "9"), (2, "10"), (3, "100"),
                     (4, "99999999999999999999999999999999999999999"), // 41桁。ulong では表せない
                 })
        {
            db.BattleStatuses.Add(new BattleStatusRecord { UserId = userId, Exp = BigInteger.Parse(exp) });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var descending = await db.BattleStatuses.OrderByExpDescending().Select(x => x.UserId).ToListAsync();
        Assert.Equal(new ulong[] { 4, 3, 2, 1 }, descending);

        var ascending = await db.BattleStatuses.OrderByExp().Select(x => x.UserId).ToListAsync();
        Assert.Equal(new ulong[] { 1, 2, 3, 4 }, ascending);
    }

    [DatabaseFact]
    public async Task 所持金額ランキングが辞書順ではなく数値順を返す()
    {
        await using var db = await fixture.CreateContextAsync();
        await TruncateAsync(db, "chido_player_currency");

        foreach (var (userId, amount) in new (ulong, string)[] { (1, "9"), (2, "10"), (3, "100") })
        {
            db.PlayerCurrencies.Add(new PlayerCurrencyRecord { UserId = userId, Amount = BigInteger.Parse(amount) });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var descending = await db.PlayerCurrencies.OrderByAmountDescending().Select(x => x.UserId).ToListAsync();
        Assert.Equal(new ulong[] { 3, 2, 1 }, descending);

        var ascending = await db.PlayerCurrencies.OrderByAmount().Select(x => x.UserId).ToListAsync();
        Assert.Equal(new ulong[] { 1, 2, 3 }, ascending);
    }

    [DatabaseFact]
    public async Task 経験値ランキングがインデックスの逆走査で処理される()
    {
        await using var db = await fixture.CreateContextAsync();
        await TruncateAsync(db, "chido_battle_status");

        for (ulong userId = 1; userId <= 20; userId++)
        {
            db.BattleStatuses.Add(new BattleStatusRecord { UserId = userId, Exp = userId * 1000 });
        }

        await db.SaveChangesAsync();

        var plan = await ExplainAsync(db, "SELECT user_id FROM chido_battle_status ORDER BY exp_len DESC, exp DESC LIMIT 10");

        // 降順インデックスを張らずに済ませている前提そのもの。filesort に落ちるとランキングが
        // 全行ソートになり、行数の増加に対して破綻する
        Assert.Equal("idx_exp_rank", plan.Key);
        Assert.Contains("Backward index scan", plan.Extra);
        Assert.DoesNotContain("filesort", plan.Extra);
    }

    [DatabaseFact]
    public async Task 所持金額ランキングがインデックスの逆走査で処理される()
    {
        await using var db = await fixture.CreateContextAsync();
        await TruncateAsync(db, "chido_player_currency");

        for (ulong userId = 1; userId <= 20; userId++)
        {
            db.PlayerCurrencies.Add(new PlayerCurrencyRecord { UserId = userId, Amount = userId * 1000 });
        }

        await db.SaveChangesAsync();

        var plan = await ExplainAsync(db, "SELECT user_id FROM chido_player_currency ORDER BY amount_len DESC, amount DESC LIMIT 10");

        Assert.Equal("idx_amount_rank", plan.Key);
        Assert.Contains("Backward index scan", plan.Extra);
        Assert.DoesNotContain("filesort", plan.Extra);
    }

    // --- ヘルパー ---

    private static async Task TruncateAsync(ChidoDbContext db, params string[] tables)
    {
        foreach (var table in tables)
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE `" + table + "`");
        }
    }

    private static async Task<List<string>> QueryTableNamesAsync(ChidoDbContext db)
    {
        var connection = await OpenConnectionAsync(db);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>EF Core が接続を閉じた状態で返すことがあるため、開いていなければ開く。</summary>
    private static async Task<DbConnection> OpenConnectionAsync(ChidoDbContext db)
    {
        var connection = db.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    /// <summary>EXPLAIN の key 列と Extra 列を読む。</summary>
    private static async Task<(string Key, string Extra)> ExplainAsync(ChidoDbContext db, string sql)
    {
        var connection = await OpenConnectionAsync(db);

        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN " + sql;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "EXPLAIN が行を返さなかった");

        var key = reader["key"];
        var extra = reader["Extra"];

        return (key is DBNull ? "" : (string)key, extra is DBNull ? "" : (string)extra);
    }
}
