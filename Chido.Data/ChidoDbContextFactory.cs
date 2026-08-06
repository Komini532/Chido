using Microsoft.EntityFrameworkCore;

namespace Chido.Data;

/// <summary>
/// 実行時（Botのadminコマンド）とEF Coreツール（dotnet-ef）の両方から
/// 共通で使うDbContext生成ロジック。DIコンテナを使わない現状のBot構成に合わせ、
/// 環境変数から直接接続文字列を解決するシンプルな作りにしている。
/// </summary>
public static class ChidoDbContextFactory
{
    /// <summary>
    /// 接続文字列を格納する環境変数名。
    /// 例: "Server=localhost;Port=3306;Database=chido;User=chido_bot;Password=xxxx;"
    /// </summary>
    public const string ConnectionStringEnvVar = "CHIDO_MYSQL_CONNECTION";

    public static string ResolveConnectionString()
        => Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
           ?? throw new InvalidOperationException(
               $"Environment variable {ConnectionStringEnvVar} is not set.");

    /// <summary>
    /// 対象とするMySQLのバージョン。
    ///
    /// <c>ServerVersion.AutoDetect()</c> ではなく固定バージョン指定にしている。
    /// 理由: <c>dotnet ef migrations add</c> をWindows開発機で実行する際、
    /// 本番MySQLサーバーへ常に到達できるとは限らないため（AutoDetectは実接続が必須）。
    ///
    /// 9.7 は 8.4 の次のLTS（8.0 系は 2026-04-30 にEOL）。実サーバーを移行したら、
    /// メジャー/マイナー（9.7 の部分）を実サーバーに合わせて調整すること。
    /// パッチ番号は実サーバーに追随させず、そのLTS系列の下限である 0 に固定する。
    /// Pomelo が機能の可否を切り替える閾値は 8.0.31 が最大でそれより上には無いため、
    /// 下限を指定しておけば実サーバーに存在しない機能を前提にする事故が起きない。
    ///
    /// なお Pomelo 8.0.2 が公式にテストしている対象は MySQL 8.4 / 8.0 までで、
    /// 9.x は対象外（tracking issue: PomeloFoundation/Pomelo.EntityFrameworkCore.MySql#2022）。
    /// 上記のとおり閾値が 8.0.31 止まりなので Pomelo が組み立てるSQLは 8.4 向けと同一であり、
    /// 実DBテスト（Chido.Data.Tests の DatabaseFact）で 9.7 に対する動作を担保している。
    /// Pomelo を上げる際は、この前提が崩れていないかを実DBに対して取り直すこと。
    /// </summary>
    public static readonly MySqlServerVersion ServerVersion = new(new Version(9, 7, 0));

    public static ChidoDbContext CreateDbContext(string? connectionStringOverride = null)
    {
        var connectionString = connectionStringOverride ?? ResolveConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<ChidoDbContext>();

        optionsBuilder.UseMySql(connectionString, ServerVersion);

        return new ChidoDbContext(optionsBuilder.Options);
    }
}
