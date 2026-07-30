using Chido;
using Chido.Commands;
using Chido.Commands.Admin;
using Chido.Data;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// DbContext はコマンドごとに独立したスコープで使う。Discord のイベントは並行して届き、
// DbContext はスレッドセーフではないため、シングルトンで共有すると同時実行で壊れる。
// ファクトリ経由にすることで、コマンド1回分の寿命を呼び出し側が明示的に区切れる
builder.Services.AddDbContextFactory<ChidoDbContext>(options =>
    options.UseMySql(
        ChidoDbContextFactory.ResolveConnectionString(),
        ChidoDbContextFactory.ServerVersion));

builder.Services.AddSingleton(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.MessageContent,
});

builder.Services.AddSingleton<DiscordSocketClient>();

// コマンドは1つの登録点にまとめる。ここに足し忘れるとコマンドが存在しないのと同じになるため、
// 登録・振り分け・スラッシュコマンドの定義がすべてこの列挙から導かれるようにしている
builder.Services.AddSingleton<ISlashCommand, AttackCommand>();
builder.Services.AddSingleton<ISlashCommand, SkillCommand>();
builder.Services.AddSingleton<ISlashCommand, EscapeCommand>();
builder.Services.AddSingleton<ISlashCommand, UseCommand>();
builder.Services.AddSingleton<ISlashCommand, StatusCommand>();
builder.Services.AddSingleton<ISlashCommand, InventoryCommand>();
builder.Services.AddSingleton<ISlashCommand, AdminDbMigrateCommand>();
builder.Services.AddSingleton<ISlashCommand, AdminDbStatusCommand>();

builder.Services.AddHostedService<DiscordBotService>();

await builder.Build().RunAsync();
