namespace Chido.Core.Battle.Actions;

// DB(chido_battle_log.action_type: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 既存ログ行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum ActionType
{
    Attack = 0,
    Skill  = 1,
    Item   = 2,
    Defend = 3,
    Escape = 4,
}
