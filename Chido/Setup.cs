using Chido.Data;
using Chido.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Chido;

/// <summary>
/// 初期化専用の実行モード（<c>dotnet run -- setup</c>）。
///
/// <para>
/// <b>Discord へ接続せず、マイグレーションの適用とマスタ投入だけを行って終了する。</b>
/// これが無いと新規デプロイが立ち上がらない。通常起動は
/// <c>GameCatalogs.ReloadAsync</c> の起動時検証（戦闘システム 10.5）を通るが、
/// 空のDBでは草原が無いため必ず失敗する。一方、テーブル作成とマスタ投入の手段は
/// Discord の管理者コマンドしか無く、そのコマンドを打つには Bot が起動していなければならない。
/// 起動できないから初期化できず、初期化できないから起動できない、という循環になる。
/// </para>
/// <para>
/// 起動時に自動でマイグレーション・投入を走らせる案は採らない。スキーマ変更とデータ投入は
/// 「いつ起きたか」を運用側が把握できる操作であるべきで、プロセスの再起動に紐づけると
/// 意図しない再起動が意図しない適用を引き起こす。
/// </para>
/// </summary>
public static class Setup
{
    /// <summary>この実行モードを起動する引数。</summary>
    public const string ArgumentName = "setup";

    /// <summary>指定された引数が初期化モードを要求しているか。</summary>
    public static bool IsRequested(string[] args)
        => args.Any(arg => string.Equals(
            arg.TrimStart('-'), ArgumentName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// マイグレーションを適用し、不足しているマスタ行を投入する。何度実行しても安全。
    /// </summary>
    /// <returns>プロセスの終了コード。成功なら 0。</returns>
    public static async Task<int> RunAsync()
    {
        try
        {
            await using var db = ChidoDbContextFactory.CreateDbContext();

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

            if (pending.Count > 0)
            {
                Console.WriteLine($"マイグレーションを {pending.Count} 件適用します。");
                foreach (var name in pending) Console.WriteLine($"  - {name}");

                await db.Database.MigrateAsync();
            }
            else
            {
                Console.WriteLine("適用対象のマイグレーションはありません。");
            }

            var added = await MasterDataSeeder.SeedAsync(db);

            Console.WriteLine(added > 0
                ? $"マスタデータを {added} 行投入しました。"
                : "マスタデータは既に揃っています。");

            Console.WriteLine("初期化が完了しました。Bot を起動できます。");
            return 0;
        }
        catch (Exception ex)
        {
            // 初期化は人が見ている場面で走る。例外の型と内容をそのまま出す
            Console.Error.WriteLine($"初期化に失敗しました: {ex}");
            return 1;
        }
    }
}
