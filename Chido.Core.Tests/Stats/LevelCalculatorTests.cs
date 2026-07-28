using System.Numerics;
using Chido.Core;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Stats;

/// <summary>
/// level = max(1, floor(√exp)) の検証（戦闘システム 2.3）。
/// </summary>
public class LevelCalculatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(99, 9)]
    [InlineData(100, 10)]
    [InlineData(10000, 100)]
    public void FromExp_経験値の整数平方根を返す(int exp, int expectedLevel)
    {
        Assert.Equal(new BigInteger(expectedLevel), LevelCalculator.FromExp(exp));
    }

    [Fact]
    public void FromExp_初期経験値はレベル1になる()
    {
        // 新規プレイヤーは exp = 1 で初期化される（正常系の保証）
        Assert.Equal(new BigInteger(GameConstants.MinLevel), LevelCalculator.FromExp(GameConstants.InitialExp));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10000)]
    public void FromExp_想定外の経験値は下限へクランプされる(int exp)
    {
        // exp = 0 はレベル0＝全ステータス0を意味し、プレイヤーが成立しない。
        // 正規のプレイヤーでは発火しないフェイルセーフだが、あらゆる読み出しで異常値を遮断する
        Assert.Equal(new BigInteger(GameConstants.MinLevel), LevelCalculator.FromExp(exp));
    }

    [Fact]
    public void 初期経験値とレベル下限は同一の定数を参照する()
    {
        // 初期値保証（正常系）とクランプ（異常系）は冗長ではなく二重の防御であり、
        // 一方だけ変えて不整合が出ることを防ぐため同じ定数を指す（戦闘システム 2.3）
        Assert.Equal(GameConstants.MinLevel, GameConstants.InitialExp);
        Assert.Equal(GameConstants.MinLevel, GameConstants.InitialCumulativeEnemyLevel);
    }

    [Fact]
    public void FromExp_巨大な経験値でも浮動小数点の誤差が乗らない()
    {
        // レベル 10^20 ちょうどに到達する経験値と、その1手前
        var level = BigInteger.Pow(10, 20);
        var exp   = level * level;

        Assert.Equal(level,     LevelCalculator.FromExp(exp));
        Assert.Equal(level - 1, LevelCalculator.FromExp(exp - 1));
    }

    [Fact]
    public void FromExp_レベルは経験値に対して単調非減少である()
    {
        BigInteger previous = 0;
        for (var exp = 1; exp <= 500; exp++)
        {
            var level = LevelCalculator.FromExp(exp);
            Assert.True(level >= previous, $"exp={exp} でレベルが下がった");
            previous = level;
        }
    }
}
