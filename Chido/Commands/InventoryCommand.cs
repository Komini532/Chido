using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>所持アイテムを表示する。所持数が0のものは並べない。</summary>
public sealed class InventoryCommand(PlayerProfileService profiles) : ISlashCommand
{
    public string Name => "inventory";

    public string Description => "所持アイテムを表示します。";

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var items = await profiles.InventoryAsync(
            command.User.Id, command.User.GlobalName ?? command.User.Username);

        var embed = new EmbedBuilder()
            .WithTitle("所持アイテム")
            .WithColor(Color.DarkBlue)
            .WithDescription(items.Count == 0
                ? "何も持っていません。"
                : string.Join("\n", items.Select(x => $"{x.Name} ×{x.Quantity}")))
            .Build();

        await command.ModifyOriginalResponseAsync(m => m.Embed = embed);
    }
}
