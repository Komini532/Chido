using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Rewards;

/// <summary>
/// 貢献度による報酬の按分（戦闘システム 6.2）。
///
/// <code>
/// own    = そのプレイヤーが敵参加者へ与えた実効与ダメージの累計
/// sumDmg = 全プレイヤー参加者の own の合計（Escaped 者の分も含む）
/// sumHp  = 組の全メンバーの「出現時MaxLife」の合計（状態変化補正を含まない）
///
/// 分母 = max(sumDmg, sumHp)      ← 常に正。ゼロ除算は構造的に発生しない
/// c    = own ÷ 分母
/// t    = min(c ÷ 0.2, 1)
/// s    = t²                      ← 按分率
/// </code>
///
/// <b>按分率を <c>t²</c>（凸曲線）にするのは寄生を抑止するため。</b>
/// 線形（<c>s = 5c</c>）を採ると、キャップ未満の全員がダメージあたりソロの5倍の効率を得てしまい、
/// 効率が貢献率に依存しなくなるため抑止が数学的にゼロになる。<c>t²</c> では効率が貢献率に比例し、
/// <b>貢献率4%が損益分岐点</b>になる（<c>s/c = 25c = 1</c> となる点）。
///
/// | 貢献率 | 按分率 |
/// |---|---|
/// | 1% | 0.2% |
/// | 4%（損益分岐） | 4.0% |
/// | 10%（10人均等） | 25.0% |
/// | 20%以上 | 100%（頭打ち） |
///
/// <b>浮動小数点を一切通さない。</b>指数が2（整数）であることにより按分計算全体が有理数演算として
/// 閉じ、<see cref="BigInteger"/> の経験値を扱うパイプラインに誤差が混入しない。
/// </summary>
public static class Apportionment
{
    /// <summary>頭打ちに達する貢献率の逆数。20% で満額のため 5。</summary>
    public const int FullShareThreshold = 5;

    /// <summary><c>t = c ÷ 0.2</c> を整数で扱うための係数（<c>5² = 25</c>）。</summary>
    public const int ShareNumerator = 25;

    /// <summary>
    /// 按分の分母。<c>max(sumDmg, sumHp)</c>。
    ///
    /// <b>下限（sumHp）を置くのは、分母が相対値であるため総仕事量が小さいほど
    /// わずかな貢献が100%に化けるから。</b>「6行動で自滅する敵」に1ダメージだけ入れて放置すると
    /// <c>own = 1, sumDmg = 1</c> で貢献率100%となり、レベル100の敵を単独撃破したのと同額が入る。
    /// <c>t²</c> の抑止曲線は <c>c</c> を入力にしているためこの経路には無力である。
    ///
    /// <b>通常戦闘では一切発火しない。</b>敵は満タンで出現しプレイヤーがHPを0にして倒す以上、
    /// <c>sumDmg ≥ sumHp</c> が常に成立する。発火するのはプレイヤー以外の要因でHPが減った場合だけ。
    /// <c>sumHp &gt; 0</c> は常に成立するため、ゼロ除算は定義として吸収される。
    /// </summary>
    public static BigInteger Denominator(BigInteger sumDamage, BigInteger spawnMaxLifeSum)
        => BigInteger.Max(sumDamage, spawnMaxLifeSum);

    /// <summary>
    /// 按分後の値を求める。<b>丸めは最後に1回だけ</b>行う。
    ///
    /// 満額の式（<c>分子 ÷ 分母</c>）と部分の式（<c>分子 × 25 × own² ÷ (分母 × den²)</c>）を
    /// 途中で分けて丸めると、満額側の丸め誤差が部分側に持ち越されて按分率がわずかにずれる。
    /// </summary>
    /// <param name="fullNumerator">満額の分子。経験値なら <c>E × ΣexpRate</c>、通貨なら金額そのもの。</param>
    /// <param name="fullDenominator">満額の分母。経験値なら 10000（permyriad）、通貨なら 1。</param>
    /// <param name="own">そのプレイヤーの累計与ダメージ。</param>
    /// <param name="denominator"><see cref="Denominator"/> の結果。</param>
    public static BigInteger Apportion(
        BigInteger fullNumerator, BigInteger fullDenominator, BigInteger own, BigInteger denominator)
    {
        if (own <= BigInteger.Zero || denominator <= BigInteger.Zero) return BigInteger.Zero;

        // 貢献率が 20% 以上なら満額。5人までは全員が満額に到達し、6人以上から希釈が始まる
        if (FullShareThreshold * own >= denominator)
        {
            return BigIntegerMath.FloorDiv(fullNumerator, fullDenominator);
        }

        return BigIntegerMath.FloorDiv(
            fullNumerator * ShareNumerator * own * own,
            fullDenominator * denominator * denominator);
    }
}
