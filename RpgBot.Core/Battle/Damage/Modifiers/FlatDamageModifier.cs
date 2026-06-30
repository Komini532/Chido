using System.Numerics;

namespace RpgBot.Core.Battle.Damage.Modifiers;

/// <summary>固定ダメージの加算/減算 (Flat フェーズ固定)</summary>
public sealed class FlatDamageModifier : IDamageModifier
{
    private readonly BigInteger _amount;

    public ModifierPhase Phase    => ModifierPhase.Flat;
    public string?       LogLabel { get; }

    public FlatDamageModifier(BigInteger amount, string? logLabel = null)
    {
        _amount  = amount;
        LogLabel = logLabel;
    }

    public BigInteger Apply(BigInteger current, DamageContext context)
        => current + _amount;
}
