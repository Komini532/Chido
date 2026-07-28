using Chido.Core.Items;

namespace Chido.Data.Entities;

/// <summary>
/// chido_item_used_effect_master: アイテム使用効果。
/// アイテムの効果を「特定スキルの発動」に収束させ、スキル発動ロジックをそのまま再利用できるようにする。
/// </summary>
public class ItemUsedEffectMasterRecord
{
    /// <summary>chido_item_master.item_key を参照。</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>効果の連番。UseSkill は常に1件のみ、LearnSkill は複数件を許容する。</summary>
    public byte UsageIndex { get; set; }

    /// <summary>アイテム効果種別。今後拡張予定。</summary>
    public ItemUsageType ItemUsageType { get; set; }

    /// <summary>
    /// chido_skill_master.skill_key を参照。UseSkill / LearnSkill の双方で使用する。
    /// item_usage_type は今後拡張予定のため、他の効果種別を見据えて NULL 許容としている。
    /// </summary>
    public string? SkillKey { get; set; }
}
