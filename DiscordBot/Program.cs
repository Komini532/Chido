using Discord;
using Discord.WebSocket;

var config = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.MessageContent
};

var client = new DiscordSocketClient(config);

client.Log += msg =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};

client.Ready += () =>
{
    Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
    return Task.CompletedTask;
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
