using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Stats;

/// <summary>
/// ステータス算出（戦闘システム 2.3）。プレイヤーと敵は共通の式に従い、
/// 差は Shape・強さ倍率・Speed の基本値の幅だけである。
///
/// <code>
/// 基礎ステータス   = レベル × Scale(ステータス区分) × Shape(ステータス区分)
/// 戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正(+%) × 状態変化補正(+%)
///
/// 装備補正(+%)     = 1 + Σ(各スロットの補正値)
/// 状態変化補正(+%) = 1 + Σ(各状態変化インスタンスの effect_rate)
///
/// 1スロットの補正値 = progression_value × 1.2^rarity × *_rate     （DB設計25番）
/// </code>
///
/// <b>レイヤー内は加算、レイヤー間は乗算。</b>
/// 装備と状態変化を別レイヤーとして乗算するのは片方の陳腐化を防ぐためであり、
/// この理由はレイヤー間にのみ当てはまる。同一レイヤー内の複数インスタンス
/// （装備5スロット、併存する複数の状態変化）は加算合成して1つの項にまとめる。
/// したがって +10% の状態変化が2つ併存する場合は ×1.2 であり ×1.21 ではない。
///
/// <b>加算合成の帰結として補正後の値は負になりうる</b>（-60% が2つで 1 - 1.2 = -0.2）。
/// ここではクランプせず、負値をそのまま返す。ダメージ計算式が max(0, ...) を維持している
/// 理由がこれであり、クランプの位置はダメージパイプライン側（戦闘システム 5.1）。
///
/// <b>レイヤーの合計は permyriad の BigInteger で扱う。</b>
/// 割合値そのものは <see cref="Ratio"/>（int）で統一されているが、装備1スロットの補正値は
/// progression_value（実運用で65桁まで想定される BigInteger）でスケールされるため、
/// 合計は int の範囲に収まらない。Ratio を使うのはDBの列に対応する個々の割合値までとし、
/// レイヤーの合計から先は BigInteger に載せ替える。
/// </summary>
public static class StatCalculator
{
    /// <summary>Shape の格納スケール。100 = 1.00（permyriad ではない）。</summary>
    public const int ShapeScale = 100;

    /// <summary>等倍を表す permyriad 値（10000）。レイヤー適用の除数になる。</summary>
    public static readonly BigInteger OnePermyriad = Ratio.Full.Permyriad;

    /// <summary>
    /// 基礎ステータス = レベル × Scale × Shape。
    /// Scale は <see cref="GameConstants.LifeScale"/> / <see cref="GameConstants.AttackScale"/> /
    /// <see cref="GameConstants.DefenseScale"/> のいずれか。
    /// </summary>
    public static BigInteger BaseStat(BigInteger level, int scale, int shape)
        => BigIntegerMath.FloorDiv(level * scale * shape, ShapeScale);

    /// <summary>
    /// 装備1スロットが供給する補正値（permyriad）＝ progression_value × 1.2^rarity × rate。
    ///
    /// 1.2^rarity は 6^rarity ÷ 5^rarity として有理数のまま累乗し、浮動小数点を通さない
    /// （属性補正 1.3^x と同じ方針）。乗算をすべて済ませてから一度だけ床除算する。
    ///
    /// HP・攻撃力・防御力にのみ適用される。Speed と Luck はこの乗算構造の対象外であり、
    /// 装備の生の値をそのまま加算する。
    /// </summary>
    public static BigInteger EquipmentContribution(BigInteger progressionValue, Rarity rarity, Ratio rate)
    {
        var exponent = (int)rarity;
        var numerator = BigInteger.Pow(GameConstants.RarityMultiplierNumerator, exponent);
        var denominator = BigInteger.Pow(GameConstants.RarityMultiplierDenominator, exponent);

        return BigIntegerMath.FloorDiv(progressionValue * numerator * rate.Permyriad, denominator);
    }

    /// <summary>
    /// 戦闘時ステータス。強さ倍率・装備補正・状態変化補正を、それぞれ独立した乗算レイヤーとして順に適用する。
    /// 各レイヤーの境界で床値へ丸める。
    /// </summary>
    /// <param name="equipmentSum">装着中の全スロットの補正値の総和（permyriad）。1 + Σr が乗算項になる。</param>
    /// <param name="statusSum">併存する全状態変化インスタンスの効果量の総和（permyriad）。同上。</param>
    public static BigInteger CombatStat(
        BigInteger baseStat, Ratio strengthRate, BigInteger equipmentSum, BigInteger statusSum)
    {
        var value = ApplyLayer(baseStat, strengthRate.Permyriad);
        value = ApplyLayer(value, OnePermyriad + equipmentSum);
        return ApplyLayer(value, OnePermyriad + statusSum);
    }

    /// <summary>基礎ステータスの算出から戦闘時ステータスまでを一度に行う短縮形。</summary>
    public static BigInteger CombatStat(
        BigInteger level, int scale, int shape, Ratio strengthRate, BigInteger equipmentSum, BigInteger statusSum)
        => CombatStat(BaseStat(level, scale, shape), strengthRate, equipmentSum, statusSum);

    /// <summary>
    /// 乗算レイヤーを1つ適用する。除算は floor（負の無限大方向）。
    /// BigInteger の除算は0方向切り捨てであり、デバフの累積で値が負になった場合に
    /// 丸めの向きが変わってしまうため、明示的に床除算を用いる。
    /// </summary>
    public static BigInteger ApplyLayer(BigInteger value, BigInteger multiplierPermyriad)
        => BigIntegerMath.FloorDiv(value * multiplierPermyriad, OnePermyriad);

    /// <summary>乗算レイヤーを1つ適用する（Ratio 版）。</summary>
    public static BigInteger ApplyLayer(BigInteger value, Ratio multiplier)
        => ApplyLayer(value, multiplier.Permyriad);

    /// <summary>
    /// Speed。Scale × Shape の枠組みには含まれない固定値で、変動要因は装備効果のみ
    /// （強さ倍率・状態変化補正の影響を受けない）。装備補正は絶対値の加減算であり、
    /// progression_value と rarity によるスケーリングも受けない。
    /// </summary>
    public static int Speed(int baseSpeed, int equipmentBonusSum) => baseSpeed + equipmentBonusSum;

    /// <summary>
    /// Luck。基本0%（プレイヤー・敵共通）で、変動要因は装備効果のみ。
    /// 装備補正は乗算ではなく%ポイントの加算であり、Speed と同じくスケーリングを受けない。
    /// </summary>
    public static Ratio Luck(Ratio baseLuck, Ratio equipmentBonusSum) => baseLuck + equipmentBonusSum;
}
