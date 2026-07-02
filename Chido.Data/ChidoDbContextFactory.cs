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

    public static ChidoDbContext CreateDbContext(string? connectionStringOverride = null)
    {
        var connectionString = connectionStringOverride ?? ResolveConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<ChidoDbContext>();

        // ServerVersion.AutoDetect() ではなく固定バージョン指定にしている。
        // 理由: dotnet ef migrations add をWindows開発機で実行する際、
        // 本番MySQLサーバーへ常に到達できるとは限らないため（AutoDetectは実接続が必須）。
        // 実際のMySQLサーバーバージョンに合わせて数値を調整すること。
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));

        return new ChidoDbContext(optionsBuilder.Options);
    }
}
