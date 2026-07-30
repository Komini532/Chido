using Chido.Commands;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chido;

/// <summary>
/// Discord クライアントの起動とコマンドの振り分け。
///
/// <para>
/// ギルドIDが指定されていればギルドコマンドとして登録する。ギルドコマンドは即時反映されるため
/// 開発中はこちらを使い、未指定ならグローバルコマンドとして登録する（反映に時間がかかる）。
/// </para>
/// </summary>
public sealed class DiscordBotService(
    DiscordSocketClient client,
    IEnumerable<ISlashCommand> commands,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    public const string TokenEnvVar = "DISCORD_TOKEN";
    public const string GuildIdEnvVar = "DISCORD_GUILD_ID";

    private readonly Dictionary<string, ISlashCommand> handlers =
        commands.ToDictionary(c => c.Name, StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = Environment.GetEnvironmentVariable(TokenEnvVar)
            ?? throw new InvalidOperationException($"環境変数 {TokenEnvVar} が設定されていない。");

        client.Log += OnLogAsync;
        client.Ready += OnReadyAsync;
        client.SlashCommandExecuted += OnSlashCommandAsync;
        client.AutocompleteExecuted += OnAutocompleteAsync;

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        // 停止要求が来るまで常駐する。BackgroundService の戻りがホストの終了と直結するため、
        // ここで待たないと接続直後にプロセスが落ちる
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private Task OnLogAsync(LogMessage message)
    {
        logger.Log(ToLogLevel(message.Severity), message.Exception, "{Source}: {Message}",
            message.Source, message.Message);

        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        logger.LogInformation("Logged in as {User}", client.CurrentUser.Username);

        var builders = handlers.Values.Select(c => c.Build().Build()).ToArray();

        if (ulong.TryParse(Environment.GetEnvironmentVariable(GuildIdEnvVar), out var guildId))
        {
            var guild = client.GetGuild(guildId);
            foreach (var command in builders) await guild.CreateApplicationCommandAsync(command);

            logger.LogInformation(
                "Registered {Count} guild slash commands for guild {GuildId}.", builders.Length, guildId);
            return;
        }

        foreach (var command in builders) await client.CreateGlobalApplicationCommandAsync(command);

        logger.LogInformation("Registered {Count} global slash commands.", builders.Length);
    }

    private async Task OnSlashCommandAsync(SocketSlashCommand command)
    {
        if (!handlers.TryGetValue(command.Data.Name, out var handler)) return;

        try
        {
            await handler.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            // 例外をそのまま握り潰すと、Defer 済みのコマンドが「考え中」のまま固まる。
            // 応答は必ず返し、詳細はログへ送る
            logger.LogError(ex, "コマンド {Command} の実行に失敗した。", command.Data.Name);
            await RespondWithErrorAsync(command);
        }
    }

    private async Task OnAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        if (!handlers.TryGetValue(interaction.Data.CommandName, out var handler)) return;

        try
        {
            await handler.HandleAutocompleteAsync(interaction);
        }
        catch (Exception ex)
        {
            // 候補が出ないだけで入力自体は続けられるため、ここでは通知せずログに留める
            logger.LogError(ex, "オートコンプリート {Command} の実行に失敗した。",
                interaction.Data.CommandName);
        }
    }

    private static async Task RespondWithErrorAsync(SocketSlashCommand command)
    {
        const string message = "処理中にエラーが発生しました。時間をおいて再度お試しください。";

        try
        {
            if (command.HasResponded)
            {
                await command.ModifyOriginalResponseAsync(m => m.Content = message);
                return;
            }

            await command.RespondAsync(message, ephemeral: true);
        }
        catch
        {
            // 応答の期限切れなど、ここでの失敗はどうにもできない
        }
    }

    private static LogLevel ToLogLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Debug,
        LogSeverity.Debug => LogLevel.Trace,
        _ => LogLevel.Information,
    };
}
