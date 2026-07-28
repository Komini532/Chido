using Chido.Core.Items;

namespace Chido.Data.Entities;

/// <summary>
/// chido_item_master: アイテムマスタ。
/// 使用時の具体的な効果内容は chido_item_used_effect_master（24番）が管理する。
/// </summary>
public class ItemMasterRecord
{
    /// <summary>可読キー。</summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// アイテム種別。表示用カテゴリ。SkillLearning の場合は chido_item_used_effect_master に
    /// item_usage_type=LearnSkill の行が存在することの非正規化キャッシュであり、
    /// 真実の情報源はそちら側。整合性の維持はアプリ側の責務。
    /// </summary>
    public ItemType ItemType { get; set; }

    /// <summary>消費アイテムか。item_type とは独立したフラグとして持つ。</summary>
    public bool IsConsumable { get; set; }

    /// <summary>説明文。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 特殊処理呼び出し記号。NULL=標準処理のみで完結。
    /// 実在するテーブルを指す物理的な外部キーではなく、アプリ側のディスパッチ処理を呼ぶための記号。
    /// </summary>
    public string? SpecialProcessKey { get; set; }
}
