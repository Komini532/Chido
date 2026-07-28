using System;
using System.Numerics;

namespace Chido.Core.Stats;

/// <summary>
/// BigInteger の整数演算ユーティリティ。
///
/// 本システムは経験値按分・レベル導出・ダメージ計算のいずれも
/// 「有理数演算として閉じ、浮動小数点が一切混入しない」ことを設計価値としている
/// （戦闘システム 2.3 / 5.1 / 6.2）。double を経由すると 15〜17 桁で精度が失われ、
/// BigInteger を採用した意味そのものが失われるため、必要な演算はここに実装する。
/// </summary>
public static class BigIntegerMath
{
    /// <summary>
    /// 整数平方根 floor(√value) をニュートン法で求める。Math.Sqrt（浮動小数点）を経由しない。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">value が負の場合。</exception>
    public static BigInteger Sqrt(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "負の数の平方根は定義されない。");
        }

        // 0 と 1 は反復すると x = 0 でゼロ除算になるため先に返す
        if (value <= BigInteger.One) return value;

        // 初期値をビット長から概算する。2^(bitLength/2 + 1) は必ず √value 以上になるため、
        // 以降の反復は単調非減少にならず、必ず上から床値へ収束する。
        var initialShift = (int)(value.GetBitLength() / 2) + 1;
        var current = BigInteger.One << initialShift;

        while (true)
        {
            // x_{n+1} = (x_n + value / x_n) / 2
            var next = (current + value / current) >> 1;

            // 単調非増加でなくなった時点が床値。next > current は収束後の振動を意味する
            if (next >= current) return current;

            current = next;
        }
    }

    /// <summary>
    /// 負の無限大方向への除算（床除算）。
    ///
    /// BigInteger の / 演算子は0方向切り捨てであり、負のオペランドでは floor と結果が異なる
    /// （例: -7 / 2 は 0方向なら -3、floor なら -4）。設計は「すべての除算は floor とし、
    /// 各フェーズ境界で整数へ床る」と定めている（戦闘システム 5.1 の丸め規則）ため、
    /// 負値を扱いうる箇所ではこちらを使う。
    /// </summary>
    /// <exception cref="DivideByZeroException">divisor が 0 の場合。</exception>
    public static BigInteger FloorDiv(BigInteger dividend, BigInteger divisor)
    {
        if (divisor.IsZero)
        {
            throw new DivideByZeroException();
        }

        var quotient = BigInteger.DivRem(dividend, divisor, out var remainder);

        // 余りが 0 でなく、かつ被除数と除数の符号が異なる場合のみ、0方向切り捨てが
        // floor より 1 大きくなっている
        if (!remainder.IsZero && (dividend.Sign < 0) != (divisor.Sign < 0))
        {
            quotient -= BigInteger.One;
        }

        return quotient;
    }
}
