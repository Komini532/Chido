using Chido.Commands;
using Chido.Commands.Admin;
using Discord;
using Discord.WebSocket;

var config = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.MessageContent
};

var client = new DiscordSocketClient(config);

var commandHandlers = new Dictionary<string, Func<SocketSlashCommand, Task>>
{
    [AttackCommand.Name] = AttackCommand.ExecuteAsync,
    [EscapeCommand.Name] = EscapeCommand.ExecuteAsync,
    [InventoryCommand.Name] = InventoryCommand.ExecuteAsync,
    [SkillCommand.Name] = SkillCommand.ExecuteAsync,
    [StatusCommand.Name] = StatusCommand.ExecuteAsync,
    [UseCommand.Name] = UseCommand.ExecuteAsync,
    [AdminDbMigrateCommand.Name] = AdminDbMigrateCommand.ExecuteAsync,
    [AdminDbStatusCommand.Name] = AdminDbStatusCommand.ExecuteAsync,
};

var slashCommands = new SlashCommandBuilder[]
{
    new SlashCommandBuilder()
        .WithName(AttackCommand.Name)
        .WithDescription(AttackCommand.Description),
    new SlashCommandBuilder()
        .WithName(EscapeCommand.Name)
        .WithDescription(EscapeCommand.Description),
    new SlashCommandBuilder()
        .WithName(InventoryCommand.Name)
        .WithDescription(InventoryCommand.Description),
    new SlashCommandBuilder()
        .WithName(SkillCommand.Name)
        .WithDescription(SkillCommand.Description)
        .AddOption(SkillCommand.OptionSkillName, ApplicationCommandOptionType.String, "発動するスキル名", isRequired: true),
    new SlashCommandBuilder()
        .WithName(StatusCommand.Name)
        .WithDescription(StatusCommand.Description),
    new SlashCommandBuilder()
        .WithName(UseCommand.Name)
        .WithDescription(UseCommand.Description)
        .AddOption(UseCommand.OptionItemName, ApplicationCommandOptionType.String, "使用するアイテム名", isRequired: true),
    // 実際の可否判定は AdminAuthorization の許可リストで行うが、
    // 一般ユーザーのコマンド一覧に表示させないための多層防御としてDefaultMemberPermissionsも設定しておく
    new SlashCommandBuilder()
        .WithName(AdminDbMigrateCommand.Name)
        .WithDescription(AdminDbMigrateCommand.Description)
        .WithDefaultMemberPermissions(GuildPermission.Administrator),
    new SlashCommandBuilder()
        .WithName(AdminDbStatusCommand.Name)
        .WithDescription(AdminDbStatusCommand.Description)
        .WithDefaultMemberPermissions(GuildPermission.Administrator),
};

client.Log += msg =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};

client.Ready += async () =>
{
    Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");

    var guildIdValue = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID");
    if (ulong.TryParse(guildIdValue, out var guildId))
    {
        var guild = client.GetGuild(guildId);
        foreach (var builder in slashCommands)
            await guild.CreateApplicationCommandAsync(builder.Build());
        Console.WriteLine($"Registered {slashCommands.Length} guild slash commands for guild {guildId}.");
    }
    else
    {
        foreach (var builder in slashCommands)
            await client.CreateGlobalApplicationCommandAsync(builder.Build());
        Console.WriteLine($"Registered {slashCommands.Length} global slash commands.");
    }
};

client.SlashCommandExecuted += async command =>
{
    if (commandHandlers.TryGetValue(command.Data.Name, out var handler))
        await handler(command);
};

client.MessageReceived += async msg =>
{
    if (msg.Author.IsBot) return;
    if (msg.Content == "!ping")
        await msg.Channel.SendMessageAsync("Pong!");
};

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
    ?? throw new InvalidOperationException("Environment variable DISCORD_TOKEN is not set.");

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await Task.Delay(Timeout.Infinite);
