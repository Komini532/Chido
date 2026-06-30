using System;

namespace RpgBot.Core.Battle;

/// <summary>
/// バトルログ1行分。Discordメッセージ編集時の表示に使用する。
/// </summary>
public sealed record BattleLogEntry(
    int            Round,
    string         ActorName,
    string         Message,
    DateTimeOffset Timestamp
)
{
    public static BattleLogEntry Create(int round, string actorName, string message)
        => new(round, actorName, message, DateTimeOffset.UtcNow);
}
