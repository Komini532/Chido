namespace RpgBot.Core.Battle;

public enum BattleEndReason
{
    PlayerVictory,
    PlayerDefeat,
    Escaped,
    Timeout, // 非同期なので一定時間放置でタイムアウト
}
