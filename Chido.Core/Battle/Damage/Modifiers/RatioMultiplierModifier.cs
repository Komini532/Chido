using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage.Modifiers;

/// <summary>
/// 指定フェーズで current に Ratio を乗算する汎用 Modifier。
/// スキル倍率・属性補正・クリティカル倍率など、乗算系すべてをカバーする。
/// </summary>
public sealed class RatioMultiplierModifier : IDamageModifier
{
    private readonly Ratio   _multiplier;
    private readonly string? _logLabel;

    public ModifierPhase Phase    { get; }
    public string?       LogLabel => _logLabel;

    public RatioMultiplierModifier(Ratio multiplier, ModifierPhase phase, string? logLabel = null)
    {
        _multiplier = multiplier;
        _logLabel   = logLabel;
        Phase       = phase;
    }

    // --- ファクトリ (よく使うパターンを意図明示) ---

    /// <summary>スキル倍率 (PostDefense)</summary>
    public static RatioMultiplierModifier SkillPower(Ratio power, string skillName)
        => new(power, ModifierPhase.PostDefense, $"{skillName} ×{power.Percent:F0}%");

    /// <summary>属性補正。攻撃者の ATK に乗算するため防御差し引き前の PreDefense で適用する</summary>
    public static RatioMultiplierModifier Elemental(Ratio multiplier, string elementName)
        => new(multiplier, ModifierPhase.PreDefense, $"{elementName}属性 ×{multiplier.Percent:F0}%");

    /// <summary>クリティカル (PostDefense)</summary>
    public static RatioMultiplierModifier Critical(Ratio multiplier)
        => new(multiplier, ModifierPhase.PostDefense, $"クリティカル ×{multiplier.Percent:F0}%");

    /// <summary>ATK バフ/デバフ (PreDefense)</summary>
    public static RatioMultiplierModifier AtkBuff(Ratio multiplier, string sourceName)
        => new(multiplier, ModifierPhase.PreDefense, $"{sourceName} ATK ×{multiplier.Percent:F0}%");

    public BigInteger Apply(BigInteger current, DamageContext context)
        => _multiplier.Of(current);
}
