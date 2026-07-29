using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;
using Chido.Core.Equipment;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_equipment_master: 装備マスタ。
/// HP・物理攻撃・物理防御・魔法攻撃・魔法防御は P(level) × 1.2^rarity × 補正値 で最終値を算出する。
/// Speed と Luck はこの乗算構造の対象外で、Speed は固定加算の整数、Luck は permyriad の加算値として扱う。
/// </summary>
public class EquipmentMasterRecord
{
    /// <summary>可読キー。</summary>
    public string EquipKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 装備可能パーツ（ビット列）。スロットの種別（候補）を表すものであり、
    /// 物理カラムと1対1対応する保証はない（1つの装備が複数スロットのいずれかを選んで装着できる、択一の候補提示）。
    /// どのスロットに属するかは本列を制約条件としてアプリ側が解決する。
    /// </summary>
    public EquipPart EquipParts { get; set; }

    /// <summary>装備レアリティ。同一進行度内での強さの序列付けに使用する。</summary>
    public Rarity Rarity { get; set; }

    /// <summary>
    /// 装備が付与する属性（ビット列）。0 = 属性なし。
    /// プレイヤーの本体属性は装備由来のみであり、装着中の全スロットの elements の OR で決まる。
    /// 多くの装備は 0 を設定する運用を想定する。
    /// </summary>
    public Element Elements { get; set; }

    /// <summary>
    /// レベルに対する想定進行度 P(level) の結果値のみを格納する（例: Lv5000 で P(5000)=60）。
    /// レアリティ補正（×1.2^rarity）や各ステータス補正の乗算はアプリ側で都度算出する。
    /// 手動設定される基礎値。10進整数文字列として VARCHAR(100) に格納する。
    /// </summary>
    public BigInteger ProgressionValue { get; set; }

    /// <summary>HP補正値。符号あり（10000=等倍、0=このステータスに無効果、負値=デメリット装備）。</summary>
    public Ratio HpRate { get; set; }

    /// <summary>物理攻撃力補正値。</summary>
    public Ratio PAtkRate { get; set; }

    /// <summary>物理防御力補正値。</summary>
    public Ratio PDefRate { get; set; }

    /// <summary>魔法攻撃力補正値。</summary>
    public Ratio MAtkRate { get; set; }

    /// <summary>魔法防御力補正値。</summary>
    public Ratio MDefRate { get; set; }

    /// <summary>素早さ固定変動値。絶対値の加減算（例: +50 / -30）。Ratio への変換対象外。</summary>
    public int SpeedBonus { get; set; }

    /// <summary>運補正値。乗算ではなく%ポイントの加算（例: +5% → 500）。</summary>
    public Ratio LuckBonusRate { get; set; }
}
