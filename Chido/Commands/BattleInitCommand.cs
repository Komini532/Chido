using Chido.Battle;
using Chido.Rendering;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 戦闘チャンネルの初期化（戦闘システム 10.5）。
///
/// <para>
/// 「敵を倒すと次の敵が出る」という構造である以上、<b>最初の敵をどう出すか</b>が別途必要になる。
/// 実体は <see cref="BattleService.InitializeChannelAsync"/> にあり、本型は Discord の
/// インタラクションとの接続にとどまる。
/// </para>
/// <para>
/// 実行権限は制限しない（現時点の決定）。
/// </para>
/// </summary>
public sealed class BattleInitCommand(BattleService battles) : ISlashCommand
{
    public string Name => "battle-init";

    public string Description => "このチャンネルを戦闘チャンネルとして初期化し、最初の敵を出現させます。";

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        // ロック待ちで3秒を超えうるため、一次応答を先に返してから結果で埋める（戦闘システム 7.3）
        await command.DeferAsync();

        var outcome = await battles.InitializeChannelAsync(command.ChannelId ?? 0);

        var embed = BattleEmbed.Build(
            outcome.Message,
            "戦闘チャンネルの初期化",
            outcome.Accepted ? Color.Green : Color.LightGrey);

        await command.ModifyOriginalResponseAsync(m => m.Embed = embed);
    }
}
