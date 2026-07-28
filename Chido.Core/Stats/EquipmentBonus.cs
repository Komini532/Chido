using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;

namespace Chido.Core.Stats;

/// <summary>
/// 装着中の装備1つ分。chido_equipment_master の列に1対1で対応する。
///
/// HP・攻撃力・防御力については、この1件が供給する補正値そのものが
/// <c>progression_value × 1.2^rarity × *_rate</c> で算出される（DB設計25番）。
/// 算出された補正値が装備レイヤーの加算合成 <c>1 + Σ(各スロットの補正値)</c> に入る（戦闘システム 2.3）。
/// 2つの式は入れ子の関係にあり、前者が1スロット分の補正値を、後者がレイヤー全体の倍率を定める。
///
/// Speed と Luck はこの乗算構造の対象外であり、<c>progression_value</c> と <c>rarity</c> による
/// スケーリングを受けない（戦闘システム 2.3・2.5）。
/// <list type="bullet">
///   <item>HP・攻撃力・防御力 … +%（割増）。progression_value × 1.2^rarity でスケールされる</item>
///   <item>Speed … 絶対値の加減算（例: +50 / -30）。生の値をそのまま加算する</item>
///   <item>Luck  … %ポイントの加算（例: +5% → 500）。生の値をそのまま加算する</item>
/// </list>
///
/// 補正値は負値を取りうる（デメリット装備）。他の計算式に渡す前提の中間値であり、
/// 最終ステータスそのものではないため、この段階でのクランプは行わない。
/// </summary>
public readonly record struct EquipmentBonus(
    BigInteger ProgressionValue,
    Rarity Rarity,
    Ratio MaxLifeRate,
    Ratio PAtkRate,
    Ratio PDefRate,
    Ratio MAtkRate,
    Ratio MDefRate,
    int SpeedBonus,
    Ratio LuckBonusRate,
    Element Elements)
{
    /// <summary>
    /// 補正を一切持たない装備。進行度が 0 のため、HP・攻撃力・防御力への寄与も 0 になる。
    /// スロット未装着の表現やテストの基点に使う。
    /// </summary>
    public static readonly EquipmentBonus None = new(
        BigInteger.Zero, Rarity.Common,
        Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero,
        0, Ratio.Zero, Element.None);

    /// <summary>
    /// 指定ステータスに対する、このスロット1件分の補正値（permyriad）。
    /// 装備レイヤーの Σ に加算される項であり、<see cref="StatCalculator.EquipmentContribution"/> で算出する。
    /// </summary>
    public BigInteger ContributionOf(Ratio rate)
        => StatCalculator.EquipmentContribution(ProgressionValue, Rarity, rate);
}
