using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_equipment_master: 敵の装備マスタ（抽選候補）。
/// どのスロットに属するかを示す列は持たない。各候補がどのスロットに対応するかは
/// equip_key 経由で chido_equipment_master.equip_parts を参照すれば判定できるため。
/// </summary>
public class EnemyEquipmentMasterRecord
{
    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>抽選候補の連番。</summary>
    public byte EnemyEquipmentIndex { get; set; }

    /// <summary>chido_equipment_master.equip_key を参照。</summary>
    public string EquipKey { get; set; } = string.Empty;

    /// <summary>
    /// 装着確率。同一スロット内の候補の合計が 10000 未満の場合、残差は
    /// 「そのスロットに装備なし」を選ぶ暗黙の重みとして扱う。
    /// 残差に意味を持つ確率値であるため weight（相対重み）ではなく _rate として表現する。
    /// 合計が 10000 を超えた場合は 100% 基準を放棄し、候補間の相対比率のみによる
    /// 重み付き抽選にフォールバックする（複数行にまたがる集計を要するためアプリ側の責務）。
    /// </summary>
    public Ratio EquipRate { get; set; }

    /// <summary>
    /// ドロップ率。equip_rate とは別軸の値。
    /// 「そもそも装備を着けている確率」と「その装備を着けた状態で敵が撃破された場合にドロップする確率」は
    /// 独立して判定される。
    /// </summary>
    public Ratio DropRate { get; set; }
}
