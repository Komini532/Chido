using Chido.Core.Battle.Damage;

namespace Chido.Core.Stats;

/// <summary>
/// 装着中の装備1つ分の補正。chido_equipment_master の補正列に1対1で対応する。
///
/// 補正の表現形式はステータスにより3通りに分かれる（戦闘システム 2.3・2.5）。
/// <list type="bullet">
///   <item>HP・攻撃力・防御力 … +%（割増）。同一レイヤー内で加算合成され、1つの乗算項になる</item>
///   <item>Speed … 絶対値の加減算（例: +50 / -30）。Scale × Shape の枠組みの外</item>
///   <item>Luck  … %ポイントの加算（例: +5% → 500）。乗算ではない</item>
/// </list>
///
/// 補正値は負値を取りうる（デメリット装備）。他の計算式に渡す前提の中間値であり、
/// 最終ステータスそのものではないため、この段階でのクランプは行わない。
///
/// <para>
/// <b>chido_equipment_master.progression_value / rarity は本型に含めていない。</b>
/// DB設計25番は「HP・攻撃・防御は P(level) × 1.2^rarity × 補正値 で最終値を算出する<i>想定</i>」と
/// 記しているが、戦闘システム 2.3 は決定事項として装備補正を
/// 「1 + Σ(各スロットの補正値)」という乗算レイヤーと定めており、両者は両立しない
/// （前者は絶対値の供給、後者は倍率の供給）。
/// 戦闘ロジックの正は戦闘システムドキュメントであるため後者を実装している。
/// また progression_value は装備ごとの固定スカラーであり、保持者の現在レベルに応じた
/// P(level) を実行時に再評価できないため、そもそも実行時の乗算には使えない。
/// 装備を作成する際に適切な補正値を導くための設計上の目安と解釈している。
/// </para>
/// </summary>
public readonly record struct EquipmentBonus(
    Ratio MaxLifeRate,
    Ratio PAtkRate,
    Ratio PDefRate,
    Ratio MAtkRate,
    Ratio MDefRate,
    int SpeedBonus,
    Ratio LuckBonusRate,
    Element Elements)
{
    /// <summary>補正を一切持たない装備。テストやスロット未装着の表現に使う。</summary>
    public static readonly EquipmentBonus None = new(
        Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero, 0, Ratio.Zero, Element.None);
}
