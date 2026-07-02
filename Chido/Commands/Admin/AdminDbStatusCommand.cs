using System.Text;
using Chido.Administration;
using Chido.Data;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace Chido.Commands.Admin;

/// <summary>
/// DBマイグレーションの適用状況を確認する管理者コマンド。デバッグ・運用確認用。
/// </summary>
public static class AdminDbStatusCommand
{
    public const string Name = "admin-db-status";
    public const string Description = "[管理者専用] DBマイグレーションの適用状況を確認します。";

    public static async Task ExecuteAsync(SocketSlashCommand command)
    {
        if (!AdminAuthorization.IsAuthorized(command.User.Id))
        {
            await command.RespondAsync("このコマンドを実行する権限がありません。", ephemeral: true);
            return;
        }

        await command.DeferAsync(ephemeral: true);

        await using var db = ChidoDbContextFactory.CreateDbContext();

        try
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"適用済み: {applied.Count} 件");
            sb.AppendLine($"未適用　: {pending.Count} 件");

            if (pending.Count > 0)
            {
                sb.AppendLine("--- 未適用一覧 ---");
                foreach (var name in pending)
                    sb.AppendLine($"- {name}");
            }

            await command.ModifyOriginalResponseAsync(m => m.Content = sb.ToString());
        }
        catch (Exception ex)
        {
            await command.ModifyOriginalResponseAsync(m =>
                m.Content = $"状態取得中にエラーが発生しました。\n```\n{ex.Message}\n```");
        }
    }
}
