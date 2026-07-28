namespace Chido.Core.Battle.Actions;

// DB(chido_battle_log.action_type: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 既存ログ行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
// Escape を除く全種別はスキル発動に収束する（戦闘システム 4.2参照）。
// Attack と Defend もスキルマスタ上の通常のスキルエントリとして表現され、特別扱いの別実装ではない。
public enum ActionType
{
    Attack = 0,
    Skill  = 1,
    Use    = 2, // 戦闘用アイテムの使用。対象が自分・味方であっても敵からの反撃とセットで1ターンとして処理される
    Defend = 3,
    Escape = 4,
}
