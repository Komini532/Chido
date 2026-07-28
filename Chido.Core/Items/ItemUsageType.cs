namespace Chido.Core.Items;

/// <summary>
/// アイテム使用時の効果種別。
/// アイテムの効果は「特定スキルの発動」に収束するため、スキル発動ロジックをそのまま再利用できる
/// （戦闘システム 4.2参照）。
/// </summary>
// DB(chido_item_used_effect_master.item_usage_type: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum ItemUsageType
{
    /// <summary>習得状況に関わらず特定スキルを発動する。1アイテムにつき常に1件のみ。</summary>
    UseSkill = 0,

    /// <summary>スキルを習得する。1アイテムにつき複数件を許容する。</summary>
    LearnSkill = 1,
}
