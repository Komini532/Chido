namespace Chido.Core.Battle;

/// <summary>
/// 参加者の生死・離脱状態。CurrentLife==0 からの間接判定ではなく、状態そのものを一次情報として持つ。
/// </summary>
public enum ParticipantStatus
{
    Active,
    Escaped,
    Defeated,
}
