using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage.Modifiers;

/// <summary>
/// 威力（PostDefense）。基礎ダメージに × power ÷ 100 を適用する。
///
/// power は permyriad ではなく<b>整数%</b>であり（通常攻撃 = 100）、Ratio への変換対象外。
/// 計算に使われる値であると同時にプレイヤーへ提示される表示情報でもあるため、
/// 意図的に小数精度を持たせないという決定による（戦闘システム 2.2）。
///
/// power は Time To Kill 尺度の一次元量として扱われるため、攻撃と回復で意味が異なる
/// （攻撃では被防御係数が掛かるが回復では掛からない。等価な回復威力 = 攻撃威力 × 被防御係数）。
/// </summary>
public sealed class PowerModifier : IDamageModifier
{
    private readonly int _power;

    public ModifierPhase Phase => ModifierPhase.PostDefense;
    public string? LogLabel { get; }

    public PowerModifier(int power, string? sourceName = null)
    {
        _power = power;
        LogLabel = sourceName is null ? null : $"{sourceName} 威力{power}%";
    }

    public BigInteger Apply(BigInteger current, DamageContext context)
        => BigIntegerMath.FloorDiv(current * _power, GameConstants.PowerScale);
}
