using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage.Modifiers;

/// <summary>
/// ダメージ軽減率（DRR、PostDefense）。係数 (10000 − Σr) ÷ 10000 を乗算する。
///
/// 防御を DEF への補正ではなく最終ダメージへの乗算係数として表現するのは、
/// 数値インフレによりDEFの絶対値が膨張すると「DEF+○%」の意味が変わって防御が陳腐化するため。
/// (1 − DRR) は常に同じ割合だけダメージを削るので、インフレに対して意味が一定に保たれる
/// （戦闘システム 5.1）。
///
/// <b>係数は途中でクランプしない。</b> Σr &gt; 10000 なら係数が負になり基礎ダメージが負に振れるが、
/// 最終の「最低ダメージ1」がこれを吸収する。DRR 50%×2 でダメージ1、×3 でも下限1で止まり、
/// 回復への反転は起きない。
///
/// <b>攻撃モーション由来のダメージにのみ登録する。</b> 回復と SlipDamage には登録しない。
/// </summary>
public sealed class DamageResistModifier : IDamageModifier
{
    private readonly Ratio _resistRate;

    public ModifierPhase Phase => ModifierPhase.PostDefense;
    public string? LogLabel { get; }

    private DamageResistModifier(Ratio resistRate)
    {
        _resistRate = resistRate;
        LogLabel = $"ダメージ軽減 {resistRate.Percent:F0}%";
    }

    /// <summary>
    /// 対象が保持する DRR の合計から生成する。合計が 0 なら補正が不要なため null を返す。
    /// </summary>
    public static DamageResistModifier? Create(Ratio resistRate)
        => resistRate == Ratio.Zero ? null : new DamageResistModifier(resistRate);

    public BigInteger Apply(BigInteger current, DamageContext context)
    {
        var coefficient = Ratio.Full.Permyriad - _resistRate.Permyriad;
        return BigIntegerMath.FloorDiv(current * coefficient, Ratio.Full.Permyriad);
    }
}
