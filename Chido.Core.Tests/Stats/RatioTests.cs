using System.Numerics;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Stats;

/// <summary>
/// Ratio（permyriad 固定小数点）の検証。
/// 設計ドキュメント 戦闘システム 2.2 / DB設計「割合値のスケールと命名規約」を根拠とする。
/// </summary>
public class RatioTests
{
    // --- ファクトリとスケール ---

    [Theory]
    [InlineData(100, 10000)] // 100%   → 10000
    [InlineData(50, 5000)]   //  50%   →  5000
    [InlineData(4, 400)]     //   4%   →   400（クリティカル率、戦闘 5.2）
    [InlineData(0, 0)]
    public void FromPercent_permyriadスケールで保持する(int percent, int expectedPermyriad)
    {
        var ratio = Ratio.FromPercent(percent);
        Assert.Equal(Ratio.FromPermyriad(expectedPermyriad), ratio);
    }

    [Fact]
    public void FromMultiplier_1_5倍は15000permyriadになる()
    {
        // クリティカル倍率 ×1.5（戦闘 5.2）
        Assert.Equal(Ratio.FromPermyriad(15000), Ratio.FromMultiplier(1.5m));
    }

    [Fact]
    public void 定数はそれぞれ0_10000_5000を指す()
    {
        Assert.Equal(Ratio.FromPermyriad(0), Ratio.Zero);
        Assert.Equal(Ratio.FromPermyriad(10000), Ratio.Full);
        Assert.Equal(Ratio.FromPermyriad(5000), Ratio.Half); // Defend の DRR 50%（戦闘 5.4）
    }

    [Fact]
    public void PercentとMultiplierは表示用の逆変換を返す()
    {
        var ratio = Ratio.FromPermyriad(5025);
        Assert.Equal(50.25m, ratio.Percent);
        Assert.Equal(0.5025m, ratio.Multiplier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    [InlineData(-6000)]
    [InlineData(15000)]
    public void Permyriadは内部値をそのまま返す(int permyriad)
    {
        // DBへの永続化と、permyriad のまま加算合成してから別の形で使う箇所
        // （DRRの Σr → (10000 - Σr) / 10000。戦闘システム 5.1）のために必要
        Assert.Equal(permyriad, Ratio.FromPermyriad(permyriad).Permyriad);
    }

    // --- 乗算優先による精度保証 ---

    [Fact]
    public void Of_除算より先に乗算するため小さな値でも精度が落ちない()
    {
        // 3 の 50%。除算を先に行う実装（3 / 10000 * 5000）では 0 になる。
        Assert.Equal(new BigInteger(1), Ratio.Half.Of(new BigInteger(3)));
    }

    [Fact]
    public void Of_巨大なBigIntegerでも桁落ちしない()
    {
        // decimal（有効桁 28〜29）を経由すると壊れる 40 桁の値
        var huge = BigInteger.Parse("1234567890123456789012345678901234567890");
        Assert.Equal(huge / 2, Ratio.Half.Of(huge));
    }

    [Fact]
    public void Of_端数は0方向へ切り捨てられる()
    {
        // 1 * 5000 / 10000 = 0.5 → 0
        Assert.Equal(BigInteger.Zero, Ratio.Half.Of(BigInteger.One));
    }

    // --- 割合加算・減算 ---

    [Fact]
    public void AddTo_は元の値に割合分を上乗せする()
    {
        Assert.Equal(new BigInteger(110), Ratio.FromPercent(10).AddTo(new BigInteger(100)));
    }

    [Fact]
    public void SubtractFrom_は元の値から割合分を差し引く()
    {
        Assert.Equal(new BigInteger(90), Ratio.FromPercent(10).SubtractFrom(new BigInteger(100)));
    }

    // --- 演算子 ---

    [Fact]
    public void BigIntegerとの演算子はメソッドと同じ結果を返す()
    {
        var value = new BigInteger(1000);
        var ratio = Ratio.FromPercent(25);

        Assert.Equal(ratio.Of(value), value * ratio);
        Assert.Equal(ratio.AddTo(value), value + ratio);
        Assert.Equal(ratio.SubtractFrom(value), value - ratio);
    }

    [Fact]
    public void Ratio同士の加算は同一レイヤー内の加算合成を表す()
    {
        // 状態変化補正はレイヤー内で加算合成する（戦闘 2.3）。
        // +10% が 2 つ併存すれば +20% であり、+21%（乗算合成）にはならない。
        Assert.Equal(Ratio.FromPercent(20), Ratio.FromPercent(10) + Ratio.FromPercent(10));
    }

    [Fact]
    public void Ratio同士の減算は負の割合を生みうる()
    {
        // 強力なデバフの累積で補正が負に振れる経路（戦闘 2.3 の加算合成の帰結）
        var negative = Ratio.FromPercent(10) - Ratio.FromPercent(70);
        Assert.Equal(Ratio.FromPercent(-60), negative);
        Assert.True(negative < Ratio.Zero);
    }

    // --- 抽選 ---

    [Fact]
    public void Roll_0パーセントは決して成功しない()
    {
        var rng = new Random(12345);
        for (var i = 0; i < 1000; i++)
        {
            Assert.False(Ratio.Zero.Roll(rng));
        }
    }

    [Fact]
    public void Roll_100パーセントは常に成功する()
    {
        // Attack / Defend の accuracy_rate = 10000 固定（戦闘 4.4）が空振りしないことの根拠
        var rng = new Random(12345);
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(Ratio.Full.Roll(rng));
        }
    }

    [Fact]
    public void Roll_同一シードなら再現する()
    {
        // 戦闘ロジックのテストで乱数を決定的に扱えることの前提
        var ratio = Ratio.FromPercent(50);
        var first = Enumerable.Range(0, 50).Select(_ => ratio.Roll(new Random(7))).ToArray();
        var second = Enumerable.Range(0, 50).Select(_ => ratio.Roll(new Random(7))).ToArray();
        Assert.Equal(first, second);
    }

    // --- 比較・等価 ---

    [Fact]
    public void 比較演算子はpermyriadの大小に従う()
    {
        var small = Ratio.FromPercent(10);
        var large = Ratio.FromPercent(20);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= Ratio.FromPercent(10));
        Assert.True(small >= Ratio.FromPercent(10));
        Assert.True(small != large);
    }

    [Fact]
    public void 同値のRatioはハッシュコードも一致する()
    {
        Assert.Equal(Ratio.FromPercent(33).GetHashCode(), Ratio.FromPermyriad(3300).GetHashCode());
    }

    [Fact]
    public void ToStringはパーセント表記を返す()
    {
        Assert.Equal("50.25%", Ratio.FromPermyriad(5025).ToString());
    }
}
