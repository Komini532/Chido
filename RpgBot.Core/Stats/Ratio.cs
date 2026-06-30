using System;
using System.Numerics;

namespace RpgBot.Core.Stats;

/// <summary>
/// 割合値構造体。内部は permyriad (10000 = 100%) で管理し BigInteger との計算精度を保証する。
/// </summary>
public readonly struct Ratio : IEquatable<Ratio>, IComparable<Ratio>
{
    // 1 = 0.01%, 100 = 1%, 10000 = 100%, 15000 = 150%
    private readonly int _permyriad;

    public static readonly Ratio Zero = new(0);
    public static readonly Ratio Full = new(10000); // 100%
    public static readonly Ratio Half = new(5000);  // 50%

    private Ratio(int permyriad) => _permyriad = permyriad;

    // --- ファクトリ ---

    /// <summary>50.25m → 50.25%</summary>
    public static Ratio FromPercent(decimal percent) => new((int)(percent * 100));

    /// <summary>1.5m → 150%</summary>
    public static Ratio FromMultiplier(decimal value) => new((int)(value * 10000));

    public static Ratio FromPermyriad(int permyriad) => new(permyriad);

    // --- 表示用 ---
    public decimal Percent    => _permyriad / 100m;   // 5025 → 50.25
    public decimal Multiplier => _permyriad / 10000m; // 5025 → 0.5025

    // --- BigInteger 演算メソッド ---
    // 乗算を先に行い精度を保持 (value / 10000 * rate は NG)

    /// <summary>value の Rate% を返す</summary>
    public BigInteger Of(BigInteger value) => value * _permyriad / 10000;

    /// <summary>value + (value の Rate%) — 割合加算</summary>
    public BigInteger AddTo(BigInteger value) => value + Of(value);

    /// <summary>value - (value の Rate%) — 割合減算</summary>
    public BigInteger SubtractFrom(BigInteger value) => value - Of(value);

    // --- 演算子 ---
    public static BigInteger operator *(BigInteger value, Ratio ratio) => ratio.Of(value);
    public static BigInteger operator +(BigInteger value, Ratio ratio) => ratio.AddTo(value);
    public static BigInteger operator -(BigInteger value, Ratio ratio) => ratio.SubtractFrom(value);

    // Ratio 同士の合成
    public static Ratio operator +(Ratio a, Ratio b) => new(a._permyriad + b._permyriad);
    public static Ratio operator -(Ratio a, Ratio b) => new(a._permyriad - b._permyriad);

    // --- 比較 ---
    public bool Equals(Ratio other) => _permyriad == other._permyriad;
    public int CompareTo(Ratio other) => _permyriad.CompareTo(other._permyriad);
    public override bool Equals(object? obj) => obj is Ratio r && Equals(r);
    public override int GetHashCode() => _permyriad.GetHashCode();
    public static bool operator ==(Ratio a, Ratio b) => a.Equals(b);
    public static bool operator !=(Ratio a, Ratio b) => !a.Equals(b);
    public static bool operator <(Ratio a, Ratio b) => a._permyriad < b._permyriad;
    public static bool operator >(Ratio a, Ratio b) => a._permyriad > b._permyriad;
    public static bool operator <=(Ratio a, Ratio b) => a._permyriad <= b._permyriad;
    public static bool operator >=(Ratio a, Ratio b) => a._permyriad >= b._permyriad;

    public override string ToString() => $"{Percent:F2}%";
}
