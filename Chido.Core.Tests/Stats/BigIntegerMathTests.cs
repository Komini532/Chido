using System.Numerics;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Stats;

/// <summary>
/// BigIntegerMath の検証。
/// 「有理数演算として閉じ、浮動小数点が一切混入しない」（戦闘システム 2.3 / 5.1 / 6.2）ことを
/// 担保するユーティリティであり、double 経由の実装と結果が食い違う領域を重点的に押さえる。
/// </summary>
public class BigIntegerMathTests
{
    // --- 整数平方根 ---

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(15, 3)]
    [InlineData(16, 4)]
    [InlineData(99, 9)]
    [InlineData(100, 10)]
    public void Sqrt_小さな値の床値を返す(int value, int expected)
    {
        Assert.Equal(new BigInteger(expected), BigIntegerMath.Sqrt(value));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(123456)]
    public void Sqrt_完全平方の境界で正しく切り替わる(int n)
    {
        var square = new BigInteger(n) * n;

        Assert.Equal(new BigInteger(n - 1), BigIntegerMath.Sqrt(square - 1));
        Assert.Equal(new BigInteger(n),     BigIntegerMath.Sqrt(square));
        Assert.Equal(new BigInteger(n),     BigIntegerMath.Sqrt(square + 1));
    }

    [Fact]
    public void Sqrt_double_では表現できない桁数でも正確である()
    {
        // 10^40。double の有効桁（15〜17）を大きく超えるため Math.Sqrt では床値が保証されない
        var n      = BigInteger.Pow(10, 20);
        var square = n * n;

        Assert.Equal(n,     BigIntegerMath.Sqrt(square));
        Assert.Equal(n - 1, BigIntegerMath.Sqrt(square - 1));
        Assert.Equal(n,     BigIntegerMath.Sqrt(square + 1));
    }

    [Fact]
    public void Sqrt_結果は常に不変条件を満たす()
    {
        // r² ≤ n < (r+1)² … 床値の定義そのもの
        var values = new BigInteger[]
        {
            5, 26, 99999, BigInteger.Pow(2, 61) + 12345, BigInteger.Parse("987654321098765432109876543210"),
        };

        foreach (var n in values)
        {
            var r = BigIntegerMath.Sqrt(n);
            Assert.True(r * r <= n, $"r² ≤ n が破れた: n={n}, r={r}");
            Assert.True((r + 1) * (r + 1) > n, $"n < (r+1)² が破れた: n={n}, r={r}");
        }
    }

    [Fact]
    public void Sqrt_負の値は例外になる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BigIntegerMath.Sqrt(BigInteger.MinusOne));
    }

    // --- 床除算 ---

    [Theory]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -4)]  // 0方向切り捨てなら -3。floor は -4
    [InlineData(7, -2, -4)]
    [InlineData(-7, -2, 3)]
    [InlineData(6, 2, 3)]
    [InlineData(-6, 2, -3)]  // 割り切れる場合は補正しない
    [InlineData(0, 5, 0)]
    [InlineData(-1, 10, -1)] // 0方向切り捨てなら 0
    public void FloorDiv_負の無限大方向へ丸める(int dividend, int divisor, int expected)
    {
        Assert.Equal(new BigInteger(expected), BigIntegerMath.FloorDiv(dividend, divisor));
    }

    [Fact]
    public void FloorDiv_正の値ではBigIntegerの除算と一致する()
    {
        // 負のオペランドが現れない経路では既存の / と挙動が変わらないことを固定する
        for (var a = 0; a <= 20; a++)
        {
            for (var b = 1; b <= 7; b++)
            {
                Assert.Equal(new BigInteger(a / b), BigIntegerMath.FloorDiv(a, b));
            }
        }
    }

    [Fact]
    public void FloorDiv_0除算は例外になる()
    {
        Assert.Throws<DivideByZeroException>(() => BigIntegerMath.FloorDiv(1, 0));
    }
}
