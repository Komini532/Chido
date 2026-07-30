using Chido;
using Chido.Battle;
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

// マスタは戦闘中に変化しないため、起動時に一括で読み込んで保持する。
// 参照はチャンネル行ロック下で行われるため、都度クエリはそのままロック保持時間になる
builder.Services.AddSingleton<GameCatalogs>();
builder.Services.AddSingleton<BattleService>();
builder.Services.AddSingleton<BattleQueries>();

// コマンドは1つの登録点にまとめる。ここに足し忘れるとコマンドが存在しないのと同じになるため、
// 登録・振り分け・スラッシュコマンドの定義がすべてこの列挙から導かれるようにしている
builder.Services.AddSingleton<ISlashCommand, AttackCommand>();
builder.Services.AddSingleton<ISlashCommand, SkillCommand>();
builder.Services.AddSingleton<ISlashCommand, DefendCommand>();
builder.Services.AddSingleton<ISlashCommand, EscapeCommand>();
builder.Services.AddSingleton<ISlashCommand, UseCommand>();
builder.Services.AddSingleton<ISlashCommand, TargetCommand>();
builder.Services.AddSingleton<ISlashCommand, BattleInitCommand>();
builder.Services.AddSingleton<ISlashCommand, StatusCommand>();
builder.Services.AddSingleton<ISlashCommand, InventoryCommand>();
builder.Services.AddSingleton<ISlashCommand, AdminDbMigrateCommand>();
builder.Services.AddSingleton<ISlashCommand, AdminDbStatusCommand>();

builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();

// 起動時検証（戦闘システム 10.5）。草原フォールバックは DrawGroup と NextField の
// 両方が依存しているため、欠けていると抽選のたびに例外へ落ちる。
// 実行時の例外だけで守ると発覚がプレイヤーの行動時点まで遅れるため、ここで止める
await host.Services.GetRequiredService<GameCatalogs>().ReloadAsync();

await host.RunAsync();
