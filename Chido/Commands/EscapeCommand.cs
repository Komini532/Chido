using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// [Phase 9b/9c で実装] 戦闘から離脱します。
/// </summary>
public sealed class EscapeCommand : ISlashCommand
{
    public string Name => "escape";

    public string Description => "戦闘から離脱します。";

    public Task ExecuteAsync(SocketSlashCommand command)
        => command.RespondAsync("このコマンドはまだ実装されていません。", ephemeral: true);
}
