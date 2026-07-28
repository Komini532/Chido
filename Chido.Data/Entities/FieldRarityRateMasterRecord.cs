using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_field_rarity_rate_master (40): フィールド別レアリティ抽選率。
/// 敵の抽選1段目。合計10000を前提とする確率値であるため、weight（相対重み）ではなく _rate として表現する。
/// </summary>
public class FieldRarityRateMasterRecord
{
    /// <summary>chido_field_master.field_key を参照。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>
    /// レアリティ。Hidden はイベント専用であり通常抽選の対象に一切含まれないため、行として存在させない。
    /// </summary>
    public Rarity Rarity { get; set; }

    /// <summary>
    /// 抽選率。同一 field_key 内の合計が 10000 になる（残差は存在しない＝必ず1つ選ばれる）。
    /// </summary>
    public Ratio RarityRate { get; set; }
}
