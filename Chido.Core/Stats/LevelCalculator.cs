using System.Numerics;

namespace Chido.Core.Stats;

/// <summary>
/// 経験値からレベルを導出する（戦闘システム 2.3）。
///
///     level = max(1, floor(√exp))
///
/// レベルは常にこの式で導出し、値としては保持しない。表示（/status 等）と計算の双方が
/// 同一の導出関数を通るため、あらゆる読み出しで異常値が遮断される。
///
/// 敵のレベルは経験値ではなく累積敵レベルから直接与えられるため、本クラスは通らない。
/// </summary>
public static class LevelCalculator
{
    /// <summary>
    /// 経験値からレベルを導出する。
    ///
    /// クランプ（下限 <see cref="GameConstants.MinLevel"/>）は取得時点で行う。
    /// 新規プレイヤーの経験値は <see cref="GameConstants.InitialExp"/> で初期化され、
    /// 正規のプレイヤーは常に exp ≥ 1 が保証されるため、このクランプは理論上発火しない。
    /// したがってクランプは「初期値保証」ではなく「exp = 0 等の想定外値が万一入った場合の
    /// フェイルセーフ」である。初期値保証とクランプは冗長ではなく二重の防御であり、
    /// 両者は同一の定数を参照して独立に持たない。
    /// </summary>
    public static BigInteger FromExp(BigInteger exp)
    {
        // 負の exp は想定外だが、Sqrt が例外を投げるより下限へ倒すほうが
        // 「あらゆる読み出しで異常値が遮断される」という本メソッドの役割に合う
        if (exp.Sign <= 0) return GameConstants.MinLevel;

        return BigInteger.Max(GameConstants.MinLevel, BigIntegerMath.Sqrt(exp));
    }
}
