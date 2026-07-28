using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage.Modifiers;

/// <summary>
/// 指定フェーズで current に Ratio を乗算する汎用 Modifier。
///
/// 除算は floor（負の無限大方向）。DRR の合成係数が負に振れうるなど中間値が負になる箇所が
/// あるため、0方向切り捨てでは丸め規則に反する（戦闘システム 5.1）。
/// </summary>
public sealed class RatioMultiplierModifier : IDamageModifier
{
    private readonly Ratio _multiplier;

    public ModifierPhase Phase { get; }
    public string? LogLabel { get; }

    public RatioMultiplierModifier(Ratio multiplier, ModifierPhase phase, string? logLabel = null)
    {
        _multiplier = multiplier;
        LogLabel = logLabel;
        Phase = phase;
    }

    /// <summary>
    /// クリティカル（PostDefense）。最終ダメージへの乗算であり、回復量には適用しない
    /// （回復量は最終ダメージではないため。「会心の回復」は認めない。戦闘システム 5.2）。
    /// </summary>
    public static RatioMultiplierModifier Critical(Ratio multiplier)
        => new(multiplier, ModifierPhase.PostDefense, $"クリティカル ×{multiplier.Percent:F0}%");

    public BigInteger Apply(BigInteger current, DamageContext context)
        => BigIntegerMath.FloorDiv(current * _multiplier.Permyriad, Ratio.Full.Permyriad);
}
