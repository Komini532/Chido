namespace Chido.Core.Battle;

// DB(chido_battle_session.end_reason: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 既存行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
//
// [要確認] chido-database-design.md の end_reason コメントには
// 「PlayerVictory/PlayerDefeat/Escaped/Timeout」とあるが、実コードは Timeout ではなく
// ChannelMissing になっている。last_action_at は「放置タイムアウト判定に使用」と
// 設計書に明記されている一方、現状 Timeout に対応する BattleEndReason が存在しないため、
// 放置タイムアウトによる終了は現状表現できない。
// 対応する場合は既存メンバーを書き換えず、末尾に Timeout = 4 を追加すること。
public enum BattleEndReason
{
    PlayerVictory  = 0,
    PlayerDefeat   = 1,
    Escaped        = 2,
    ChannelMissing = 3, // チャンネル消失により戦闘の継続が不可能になった場合
}
