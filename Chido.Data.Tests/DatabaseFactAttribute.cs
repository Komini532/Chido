using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 実DBに接続するテストを表す <see cref="FactAttribute"/>。
///
/// <para>
/// 接続先（<see cref="DatabaseTestEnvironment.ConnectionStringEnvVar"/>）が無い環境では
/// スキップする。Dockerを用意せずに <c>dotnet test</c> を打つ開発者を止めないためであり、
/// CI では <see cref="DatabaseTestEnvironment.RequiredEnvVar"/> を立ててスキップを禁じる。
/// </para>
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        Skip = DatabaseTestEnvironment.SkipReason;
    }
}
