using Chido.Administration;
using Chido.Battle;
using Chido.Data;
using Chido.Data.Seeding;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace Chido.Commands.Admin;

/// <summary>
/// 不足しているマスタ行を投入し、読み込み済みのマスタを差し替える管理者コマンド。
///
/// <para>
/// 初回の立ち上げには使えない（Bot が起動していなければコマンドも受け取れない）。
/// そちらは <c>dotnet run -- setup</c> が担う。本コマンドは<b>稼働中に定義が増えたとき</b>のための経路。
/// </para>
/// <para>
/// 投入だけでは足りない。マスタは起動時に一括で読み込んで保持しているため、
/// 行を足しても<b>読み直さなければ実行中の Bot からは見えない</b>。
/// 投入と再読込を1つのコマンドに束ねているのはそのためで、
/// 片方だけを実行して「入れたのに反映されない」と悩む余地を残さない。
/// </para>
/// </summary>
public sealed class AdminSeedMasterCommand(
    IDbContextFactory<ChidoDbContext> dbFactory,
    GameCatalogs catalogs) : ISlashCommand
{
    public string Name => "admin-seed-master";

    public string Description => "[管理者専用] 不足しているマスタデータを投入し、読み込み直します。";

    public SlashCommandBuilder Build()
        => new SlashCommandBuilder()
            .WithName(Name)
            .WithDescription(Description)
            .WithDefaultMemberPermissions(GuildPermission.Administrator);

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        if (!AdminAuthorization.IsAuthorized(command.User.Id))
        {
            await command.RespondAsync("このコマンドを実行する権限がありません。", ephemeral: true);
            return;
        }

        await command.DeferAsync(ephemeral: true);

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            var added = await MasterDataSeeder.SeedAsync(db);

            // 投入した内容を実行中の Bot へ反映する。
            // 起動時検証もここで再度通るため、投入内容が壊れていればこの時点で分かる
            await catalogs.ReloadAsync();

            await command.ModifyOriginalResponseAsync(m => m.Content = added > 0
                ? $"マスタデータを {added} 行投入し、読み込み直しました。"
                : "投入対象はありませんでした。マスタを読み込み直しました。");
        }
        catch (Exception ex)
        {
            await command.ModifyOriginalResponseAsync(m =>
                m.Content = $"マスタの投入中にエラーが発生しました。\n```\n{ex.Message}\n```");
        }
    }
}
