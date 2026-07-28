using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 基礎ダメージ式（戦闘システム 5.1）。攻撃パイプラインとスリップパイプラインが共有する
/// （スリップは「攻撃式と同型」と定められているため）。回復パイプラインは通らない。
///
/// <code>
/// 基礎ダメージ = 有効ATK' + DEF = 0 のとき 0
///                それ以外        max(0, 有効ATK'² ÷ (有効ATK' + DEF))
/// </code>
///
/// 単純減算 ATK − DEF ではなく比率式を用いる。
/// <c>ATK² ÷ (ATK + DEF) = ATK × [ATK ÷ (ATK + DEF)]</c> と書き換えられ、
/// <c>ATK ÷ (ATK + DEF)</c> を<b>被防御係数</b>と呼ぶなら、回復量（＝被防御係数 1 と等価）と
/// 同じ形になる。被防御係数は DEF = ATK のとき 0.5、DEF → 0 で 1、DEF → ∞ で 0 に漸近する。
/// ATK・DEF ともに Scale が 8 であるため同格ではちょうど 0.5 になり、これがバランス較正の根拠になる。
/// </summary>
internal static class BaseDamageFormula
{
    public static BigInteger Calculate(BigInteger effectiveAtk, BigInteger defense)
    {
        var sum = effectiveAtk + defense;

        // ゼロ除算のケースは計算自体を発生させず 0 とする。
        // この後 max(0, ...) を経て最低ダメージ1が適用されるため、最終ダメージは1になる
        if (sum.IsZero) return BigInteger.Zero;

        // max(0, ...) のクランプは維持する。ATK・DEF ≥ 0 であれば新式は常に非負になるが、
        // レイヤー内加算により強力なデバフが累積すると DEF が負値を取りうるため安全策として残す
        return BigInteger.Max(BigInteger.Zero, BigIntegerMath.FloorDiv(effectiveAtk * effectiveAtk, sum));
    }
}
