using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Tests;

/// <summary>
/// 実DBテストの前処理。テスト実行ごとに1回だけ、データベースを破棄してから
/// マイグレーションを適用し直す。
///
/// <para>
/// 破棄してから作るのは、マイグレーションのDDLが「空のDBに対して通ること」自体を
/// 検証対象にしているため。適用済みのDBに対する <c>Migrate()</c> は何もしないので、
/// 使い回すとDDLの検証にならない。
/// </para>
/// <para>
/// スキップされるテストで無駄に接続しないよう、準備は <see cref="CreateContextAsync"/> の
/// 初回呼び出しまで遅延させている（xUnit のフィクスチャ構築はテストのスキップ判定と独立に走る）。
/// </para>
/// </summary>
public class DatabaseFixture
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool prepared;

    /// <summary>
    /// データベース名に足す接尾辞。<b>テストアセンブリごとに別のDBを使うための指定である。</b>
    /// 本フィクスチャは実行のたびにデータベースごと破棄するため、2つのアセンブリが同じDBを
    /// 指していると、片方が破棄している最中にもう片方が走りうる。アセンブリ内の直列化は
    /// アセンブリ間には効かないため、接続先そのものを分けている。
    /// </summary>
    protected virtual string? DatabaseSuffix => null;

    /// <summary>スキーマ準備済みのDBに接続した DbContext を返す。</summary>
    public async Task<ChidoDbContext> CreateContextAsync()
    {
        var connectionString =
            DatabaseTestEnvironment.ResolveVerifiedConnectionString(DatabaseSuffix);

        await EnsurePreparedAsync(connectionString);

        return ChidoDbContextFactory.CreateDbContext(connectionString);
    }

    private async Task EnsurePreparedAsync(string connectionString)
    {
        if (prepared)
        {
            return;
        }

        await gate.WaitAsync();
        try
        {
            if (prepared)
            {
                return;
            }

            await using var db = ChidoDbContextFactory.CreateDbContext(connectionString);
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            prepared = true;
        }
        finally
        {
            gate.Release();
        }
    }
}
