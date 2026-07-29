using MySqlConnector;

namespace Chido.Data.Tests;

/// <summary>
/// 実DBを要するテスト（<see cref="DatabaseFactAttribute"/>）の接続先解決。
///
/// <para>
/// 実行時用の <c>CHIDO_MYSQL_CONNECTION</c> とは別の環境変数を使う。テストは
/// スキーマを作り直し、テーブルを TRUNCATE するため、本番や開発用のDBを指した状態で
/// 誤って走らせるとデータが消える。変数を分けたうえで、データベース名にも
/// <c>_test</c> の接尾辞を要求して二重に弾く。
/// </para>
/// </summary>
public static class DatabaseTestEnvironment
{
    /// <summary>テスト用MySQLの接続文字列を格納する環境変数名。</summary>
    public const string ConnectionStringEnvVar = "CHIDO_TEST_MYSQL_CONNECTION";

    /// <summary>
    /// 実DBテストを必須とするかを指定する環境変数名。
    /// CI（サービスコンテナを立てている）では 1 を設定し、接続先が無い場合に
    /// 静かにスキップされて緑になることを防ぐ。
    /// </summary>
    public const string RequiredEnvVar = "CHIDO_REQUIRE_DATABASE_TESTS";

    /// <summary>テスト用DBに要求するデータベース名の接尾辞。</summary>
    public const string RequiredDatabaseSuffix = "_test";

    /// <summary>接続文字列。未設定なら null。</summary>
    public static string? ConnectionString
        => Environment.GetEnvironmentVariable(ConnectionStringEnvVar) is { Length: > 0 } value
            ? value
            : null;

    /// <summary>実DBテストが必須か（CIでの設定漏れをスキップで隠さないためのフラグ）。</summary>
    public static bool IsRequired
        => Environment.GetEnvironmentVariable(RequiredEnvVar) is "1" or "true" or "TRUE";

    /// <summary>
    /// スキップ理由。実行してよい場合は null を返す
    /// （xUnit の <c>Skip</c> は null のとき「スキップしない」を意味する）。
    /// </summary>
    public static string? SkipReason
        => ConnectionString is null && !IsRequired
            ? $"実DBが必要なテスト。環境変数 {ConnectionStringEnvVar} を設定すると実行される " +
              "（起動手順は Chido.Data/Migrations/README.md を参照）"
            : null;

    /// <summary>
    /// 接続文字列を解決する。未設定なら例外（<see cref="IsRequired"/> のときにここへ到達する）。
    /// </summary>
    public static string ResolveConnectionString()
        => ConnectionString
           ?? throw new InvalidOperationException(
               $"環境変数 {ConnectionStringEnvVar} が設定されていない。" +
               $"{RequiredEnvVar} が設定されているため、このテストはスキップできない。");

    /// <summary>
    /// テスト用DBとして安全に破棄・再作成できる接続文字列かを検証する。
    /// データベース名が <see cref="RequiredDatabaseSuffix"/> で終わらない場合は実行を拒否する。
    /// </summary>
    public static string ResolveVerifiedConnectionString()
    {
        var connectionString = ResolveConnectionString();
        var database = new MySqlConnectionStringBuilder(connectionString).Database;

        if (!database.EndsWith(RequiredDatabaseSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"テスト用DBのデータベース名は '{RequiredDatabaseSuffix}' で終わる必要がある（指定値: '{database}'）。" +
                "実DBテストはスキーマを作り直すため、本番・開発用DBを指した状態では実行しない。");
        }

        return connectionString;
    }
}
