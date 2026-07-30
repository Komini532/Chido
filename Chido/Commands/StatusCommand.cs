using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b/9c で実装] プレイヤー情報を表示します。
/// </summary>
public sealed class StatusCommand : ISlashCommand
{
    public string Name => "status";

    public string Description => "プレイヤー情報を表示します。";

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
