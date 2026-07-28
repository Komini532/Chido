using System.Numerics;

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
/// </summary>
public static class StatCalculator
{
    /// <summary>Shape の格納スケール。100 = 1.00（permyriad ではない）。</summary>
    public const int ShapeScale = 100;

    /// <summary>
    /// 基礎ステータス = レベル × Scale × Shape。
    /// Scale は <see cref="GameConstants.LifeScale"/> / <see cref="GameConstants.AttackScale"/> /
    /// <see cref="GameConstants.DefenseScale"/> のいずれか。
    /// </summary>
    public static BigInteger BaseStat(BigInteger level, int scale, int shape)
        => BigIntegerMath.FloorDiv(level * scale * shape, ShapeScale);

    /// <summary>
    /// 戦闘時ステータス。強さ倍率・装備補正・状態変化補正を、それぞれ独立した乗算レイヤーとして順に適用する。
    /// 各レイヤーの境界で床値へ丸める。
    /// </summary>
    /// <param name="equipmentSum">装着中の全スロットの補正値の総和（Σr）。1 + Σr が乗算項になる。</param>
    /// <param name="statusSum">併存する全状態変化インスタンスの効果量の総和（Σr）。同上。</param>
    public static BigInteger CombatStat(
        BigInteger baseStat, Ratio strengthRate, Ratio equipmentSum, Ratio statusSum)
    {
        var value = ApplyLayer(baseStat, strengthRate);
        value = ApplyLayer(value, Ratio.Full + equipmentSum);
        return ApplyLayer(value, Ratio.Full + statusSum);
    }

    /// <summary>
    /// 基礎ステータスの算出から戦闘時ステータスまでを一度に行う短縮形。
    /// </summary>
    public static BigInteger CombatStat(
        BigInteger level, int scale, int shape, Ratio strengthRate, Ratio equipmentSum, Ratio statusSum)
        => CombatStat(BaseStat(level, scale, shape), strengthRate, equipmentSum, statusSum);

    /// <summary>
    /// 乗算レイヤーを1つ適用する。除算は floor（負の無限大方向）。
    /// BigInteger の除算は0方向切り捨てであり、デバフの累積で値が負になった場合に
    /// 丸めの向きが変わってしまうため、明示的に床除算を用いる。
    /// </summary>
    public static BigInteger ApplyLayer(BigInteger value, Ratio multiplier)
        => BigIntegerMath.FloorDiv(value * multiplier.Permyriad, Ratio.Full.Permyriad);

    /// <summary>
    /// Speed。Scale × Shape の枠組みには含まれない固定値で、変動要因は装備効果のみ
    /// （強さ倍率・状態変化補正の影響を受けない）。装備補正は絶対値の加減算。
    /// </summary>
    public static int Speed(int baseSpeed, int equipmentBonusSum) => baseSpeed + equipmentBonusSum;

    /// <summary>
    /// Luck。基本0%（プレイヤー・敵共通）で、変動要因は装備効果のみ。
    /// 装備補正は乗算ではなく%ポイントの加算。
    /// </summary>
    public static Ratio Luck(Ratio baseLuck, Ratio equipmentBonusSum) => baseLuck + equipmentBonusSum;
}
