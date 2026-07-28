using Chido.Core.Entities;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_group_master (42): 敵の組マスタ。
/// 敵の出現抽選の単位は個体ではなく「組」である。レアリティ→敵1体という抽選方式では
/// 複数体の同時出現を表現できないため、レアリティ→組という2段階に改めた。
/// 単体の敵は「メンバーが1体の組」として表現する。
///
/// 組をフィールドに従属させず独立した可読キーで定義するのは、同一の組を複数フィールドで再利用可能にするため。
/// </summary>
public class EnemyGroupMasterRecord
{
    /// <summary>可読キー（例: 'slime_x3'）。</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// 組のレアリティ。敵の出現抽選およびEscape時の再抽選例外の判定は、個体ではなく組のレアリティで行う。
    /// </summary>
    public Rarity Rarity { get; set; }
}
