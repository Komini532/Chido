namespace Chido.Core.Battle;

public enum BattleEndReason
{
    PlayerVictory,
    PlayerDefeat,
    Escaped,
    ChannelMissing, // チャンネル消失により戦闘の継続が不可能になった場合
}
