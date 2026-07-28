namespace Chido.Core.Entities;

/// <summary>
/// レアリティ。敵の個体・敵の組・装備で共通の列挙値。
///
/// 敵の出現抽選に使われるのは「組」のレアリティ（chido_enemy_group_master.rarity）であり、
/// 個体のレアリティ（chido_enemy_master.rarity）は表示専用で抽選には使用しない
/// （戦闘システム 10.3参照）。
/// </summary>
// DB(rarity: TINYINT UNSIGNED)にそのまま永続化されるため、数値を明示している。
// 今後の変更は末尾への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
public enum Rarity
{
    Common   = 0,
    Uncommon = 1,
    Rare     = 2,
    Mythic   = 3,

    /// <summary>
    /// イベント専用。通常抽選の対象に一切含まれず、フィールド別レアリティ抽選率
    /// （chido_field_rarity_rate_master）にも行として存在させない（戦闘システム 10.3参照）。
    /// </summary>
    Hidden = 4,
}
