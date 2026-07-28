using Chido.Core.Entities;

namespace Chido.Data.Entities;

/// <summary>
/// chido_field_enemy_group_master (44): フィールドに出現する組。
///
/// 草原 Common の行は必須。抽選レアリティに該当する組が現在フィールドに0件の場合、
/// 草原の Common の組へフォールバックするため、そのフォールバック先として
/// field_key=草原 かつ rarity=Common の行が1件以上必ず存在しなければならない。
/// 起動時（またはマスタ投入時）に検証し、0件なら起動を止める。
/// フィールド遷移の草原フォールバック（41番）と合わせ、草原がシステム全体の最終防衛線になる。
/// </summary>
public class FieldEnemyGroupMasterRecord
{
    /// <summary>chido_field_master.field_key を参照。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>chido_enemy_group_master.group_key を参照。</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// chido_enemy_group_master.rarity の非正規化キャッシュ。
    /// 「フィールドF・レアリティRの組」を単一インデックスで引くために複製する。
    /// 真実の情報源は chido_enemy_group_master 側であり、整合性の維持はアプリ側の責務。
    /// </summary>
    public Rarity Rarity { get; set; }
}
