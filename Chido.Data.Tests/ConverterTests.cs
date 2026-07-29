using System.Numerics;
using Chido.Core.Stats;
using Chido.Data.Conversions;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 値コンバータの往復検証。
/// 既存コードが「読み書きの往復は実際に確認すること」とコメントで自認していた箇所を、
/// 少なくともコンバータの層では機械的に固定する。
/// </summary>
public class ConverterTests
{
    private static readonly BigIntegerToStringConverter Numeric = new();
    private static readonly NullableBigIntegerToStringConverter NullableNumeric = new();
    private static readonly GuidToBinaryConverter Binary = new();
    private static readonly NullableGuidToBinaryConverter NullableBinary = new();
    private static readonly RatioToPermyriadConverter Permyriad = new();
    private static readonly NullableRatioToPermyriadConverter NullablePermyriad = new();

    private static T RoundTrip<T, TProvider>(
        Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, TProvider> converter, T value)
    {
        var stored = (TProvider)converter.ConvertToProvider(value)!;
        return (T)converter.ConvertFromProvider(stored)!;
    }

    // --- BigInteger ---

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("12")]              // 同格Lv1の最大HP
    [InlineData("4")]               // 同格Lv1の通常攻撃ダメージ
    [InlineData("18446744073709551615")] // ulong の上限を超える桁
    public void BigIntegerが文字列を往復しても値が変わらない(string decimalText)
    {
        var value = BigInteger.Parse(decimalText);

        Assert.Equal(value, RoundTrip(Numeric, value));
        Assert.Equal(decimalText, Numeric.ConvertToProvider(value));
    }

    [Fact]
    public void BigIntegerは65桁を超えても往復できる()
    {
        // DECIMAL(65,0) の上限を超える値。VARCHAR(100) を選んでいる理由そのものにあたる
        var value = BigInteger.Pow(10, 80) + 12345;

        Assert.Equal(value, RoundTrip(Numeric, value));
        Assert.Equal(81, Numeric.ConvertToProvider(value)!.ToString()!.Length);
    }

    [Fact]
    public void BigIntegerはVARCHAR100に収まる桁数である()
    {
        // 設計上の上限。100桁ちょうどは通る
        var max = BigInteger.Pow(10, 99);
        Assert.Equal(100, Numeric.ConvertToProvider(max)!.ToString()!.Length);
    }

    [Fact]
    public void BigIntegerは列幅を超えると切り詰めずに例外になる()
    {
        // 非STRICTモードのMySQLは超過分を静かに切り詰めるため、
        // 「桁が落ちた巨大数値」が正しい値として流通する事故が起こりうる。
        // 桁数の生成列によるランキング順序も値が列幅に収まっていることを前提にしている
        var tooLarge = BigInteger.Pow(10, 100); // 101桁

        Assert.Throws<OverflowException>(() => Numeric.ConvertToProvider(tooLarge));
        Assert.Throws<OverflowException>(() => NullableNumeric.ConvertToProvider((BigInteger?)tooLarge));
    }

    [Fact]
    public void 負値は符号を含めて列幅に収まる必要がある()
    {
        // 符号も1文字を占めるため、100桁の負値は VARCHAR(100) に入らない
        var negative = -(BigInteger.Pow(10, 99));
        Assert.Throws<OverflowException>(() => Numeric.ConvertToProvider(negative));
    }

    [Fact]
    public void 桁数と辞書順の組がBigIntegerの数値順に一致する()
    {
        // exp / amount のランキングが依拠する不変条件そのもの。
        // 非負の正準10進文字列では (桁数, 序数比較) が数値順と完全に一致する。
        // これが崩れると exp_len との複合インデックスによる ORDER BY が静かに誤る
        var random = new Random(20260728);
        var values = new List<BigInteger> { 0, 1, 9, 10, 99, 100, 101, 999, 1000 };
        for (var i = 0; i < 500; i++)
        {
            var digits = random.Next(1, 101);
            var text = string.Concat(
                (char)('1' + random.Next(9)),
                string.Concat(Enumerable.Range(1, digits - 1).Select(_ => (char)('0' + random.Next(10)))));
            values.Add(BigInteger.Parse(text));
        }

        var byNumber = values.OrderBy(v => v).ToList();
        var byEncoding = values
            .OrderBy(v => Numeric.ConvertToProvider(v)!.ToString()!.Length)
            .ThenBy(v => Numeric.ConvertToProvider(v)!.ToString()!, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(byNumber, byEncoding);
    }

    [Fact]
    public void BigIntegerは負値も往復できる()
    {
        // 現行スキーマに負値を格納する列は無いが、コンバータ自体は符号を落とさない
        var value = BigInteger.Parse("-987654321098765432109876543210");
        Assert.Equal(value, RoundTrip(Numeric, value));
    }

    [Fact]
    public void NULL許容のBigIntegerがNULLを往復できる()
    {
        Assert.Null(NullableNumeric.ConvertToProvider(null));
        Assert.Null(NullableNumeric.ConvertFromProvider(null));

        BigInteger? value = BigInteger.Pow(10, 40);
        Assert.Equal(value, RoundTrip(NullableNumeric, value));
    }

    // --- Guid ---

    [Fact]
    public void Guidが16バイトを往復しても値が変わらない()
    {
        var value = Guid.NewGuid();

        Assert.Equal(value, RoundTrip(Binary, value));
        Assert.Equal(16, ((byte[])Binary.ConvertToProvider(value)!).Length);
    }

    [Fact]
    public void 空のGuidも往復できる()
    {
        Assert.Equal(Guid.Empty, RoundTrip(Binary, Guid.Empty));
    }

    [Fact]
    public void NULL許容のGuidがNULLを往復できる()
    {
        Assert.Null(NullableBinary.ConvertToProvider(null));
        Assert.Null(NullableBinary.ConvertFromProvider(null));

        Guid? value = Guid.NewGuid();
        Assert.Equal(value, RoundTrip(NullableBinary, value));
    }

    // --- Ratio ---

    [Theory]
    [InlineData(0)]      // 等倍でない 0%
    [InlineData(400)]    // クリティカル率 4%
    [InlineData(5000)]   // 防御の DRR 50%
    [InlineData(10000)]  // 等倍
    [InlineData(15000)]  // クリティカル倍率 ×1.5
    [InlineData(-6000)]  // デバフ（符号ありの補正値）
    public void Ratioがpermyriadを往復しても値が変わらない(int permyriad)
    {
        var value = Ratio.FromPermyriad(permyriad);

        Assert.Equal(value, RoundTrip(Permyriad, value));
        Assert.Equal(permyriad, Permyriad.ConvertToProvider(value));
    }

    [Fact]
    public void NULL許容のRatioがNULLを往復できる()
    {
        // fixed_rate の NULL は「不定値＝インスタンス側が実値を持つ」を表すため、
        // 0 に潰れてはならない
        Assert.Null(NullablePermyriad.ConvertToProvider(null));
        Assert.Null(NullablePermyriad.ConvertFromProvider(null));

        Ratio? zero = Ratio.Zero;
        Assert.Equal(zero, RoundTrip(NullablePermyriad, zero));
        Assert.Equal(0, NullablePermyriad.ConvertToProvider(zero));
    }
}
