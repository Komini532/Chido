namespace Chido.Core.Battle;

/// <summary>
/// 参加者の生死・離脱状態。CurrentLife==0 からの間接判定ではなく、状態そのものを一次情報として持つ。
/// 現在HPは MaxLife を超えうる（戦闘システム 3.4）ため、戦闘不能の判定根拠を CurrentLife に置けない。
/// EntityType を問わず全参加者に適用される（敵もスキルの戦闘離脱モーションにより Escaped になりうる）。
/// </summary>
// DB(chido_battle_participant.status: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 既存行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum ParticipantStatus
{
    Active   = 0,
    Escaped  = 1,
    Defeated = 2,
}
