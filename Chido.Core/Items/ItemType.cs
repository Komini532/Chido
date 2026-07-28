namespace Chido.Core.Items;

/// <summary>
/// アイテム種別（表示用カテゴリ）。
/// SkillLearning は chido_item_used_effect_master に item_usage_type = LearnSkill の行が存在することの
/// 非正規化キャッシュであり、真実の情報源はそちら側。整合性の維持はアプリ側の責務。
/// </summary>
// DB(chido_item_master.item_type: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 今後の変更は末尾への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
public enum ItemType
{
    /// <summary>
    /// 戦闘ステータスに作用する戦闘用アイテム。Use アクションの対象になる。
    /// 対象が自分・味方であっても CurrentTarget からの反撃とセットで1ターンとして処理される
    /// （回復アイテムを使ったターンも無防備になる。戦闘システム 4.2参照）。
    /// </summary>
    Battle = 0,

    Material      = 1,
    Collection    = 2,
    SkillLearning = 3,
}
