using Chido.Battle;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chido;

/// <summary>
/// チャンネル消失のフェイルセーフ検証（戦闘システム 6.3・C-1）。
///
/// <para>
/// <c>ChannelDestroyed</c> イベントは Bot の停止中や再接続の隙間に落ちうる。落ちたまま
/// 誰も拾わないと、消えたチャンネルのセッションに参加していたプレイヤーが
/// <b>永久に他の戦闘へ参加できなくなる</b>。取りこぼしを一定時間内に必ず回収するための層である。
/// </para>
/// <para>
/// 間隔は1時間。チャンネル削除は稀な事象であり、拘束が最大1時間残ることは
/// 非同期・長期間開きっぱなしという前提のもとでは許容できる。逆に短くすると、
/// 戦闘チャンネルの数だけ Discord への問い合わせが定期的に走ることになる。
/// </para>
/// </summary>
public sealed class ChannelWatchdogService(
    DiscordSocketClient client,
    ChannelCleanupService cleanup,
    ILogger<ChannelWatchdogService> logger) : BackgroundService
{
    /// <summary>検証の間隔（C-1）。</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // 1回の失敗で常駐を止めない。次の周期で改めて拾う
                logger.LogError(ex, "チャンネルの定期検証に失敗した。");
            }
        }
    }

    /// <summary>
    /// 記録されている戦闘チャンネルを1つずつ突き合わせ、消えているものを畳む。
    /// </summary>
    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        // 接続が確立していない間は判定できない。全チャンネルが「消えた」に見えるため、
        // ここで走らせると戦闘チャンネルを根こそぎ削除しかねない
        if (client.ConnectionState != Discord.ConnectionState.Connected) return;

        foreach (var channelId in await cleanup.TrackedChannelsAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) return;

            if (client.GetChannel(channelId) is not null) continue;

            logger.LogInformation(
                "チャンネル {ChannelId} が見つからないため、定期検証で畳む。", channelId);

            await cleanup.CleanupAsync(channelId, cancellationToken);
        }
    }

    /// <summary>停止要求による中断を正常終了として扱う。</summary>
    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
