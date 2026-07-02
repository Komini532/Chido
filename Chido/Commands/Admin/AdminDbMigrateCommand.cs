using Chido.Administration;
using Chido.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace Chido.Commands.Admin;

/// <summary>
/// 未適用のマイグレーションを一括適用する管理者コマンド。
/// 初回実行時はテーブルの一括作成（InitialCreate含む全マイグレーション適用）を兼ね、
/// 以降は設計変更に追随するための増分適用として使う。
/// </summary>
public static class AdminDbMigrateCommand
{
    public const string Name = "admin-db-migrate";
    public const string Description = "[管理者専用] 未適用のDBマイグレーションを一括適用します（初回はテーブル一括作成を兼ねます）。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        if (!AdminAuthorization.IsAuthorized(command.User.Id))
        {
            await command.RespondAsync("このコマンドを実行する権限がありません。", ephemeral: true);
            return;
        }

        // Discordの3秒応答制限に対応するため、重い処理の前に先にDeferする
        await command.DeferAsync(ephemeral: true);

        await using var db = ChidoDbContextFactory.CreateDbContext();

        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

            if (pending.Count == 0)
            {
                await command.ModifyOriginalResponseAsync(m =>
                    m.Content = "適用対象のマイグレーションはありません。DBスキーマは最新です。");
                return;
            }

            await db.Database.MigrateAsync();

            var appliedList = string.Join("\n", pending.Select(name => $"- {name}"));
            await command.ModifyOriginalResponseAsync(m =>
                m.Content = $"以下 {pending.Count} 件のマイグレーションを適用しました。\n{appliedList}");
        }
        catch (Exception ex)
        {
            await command.ModifyOriginalResponseAsync(m =>
                m.Content = $"マイグレーション適用中にエラーが発生しました。\n```\n{ex.Message}\n```");
        }
    }
}
