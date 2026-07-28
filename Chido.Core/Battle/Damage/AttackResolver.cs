using System;
using System.Numerics;
using Chido.Core.Battle.Damage.Modifiers;
using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// クリティカル判定・属性補正・スキル倍率を組み込んだ、1回の攻撃 (Attack/Skill/反撃共通) の解決処理。
/// </summary>
public static class AttackResolver
{
    // 発生率・倍率は GameConstants に集約している（設計ドキュメントが「同一の定数を1箇所で参照する」ことを
    // 要求しているため、各利用側で個別に持たない）
    public static (BigInteger Damage, string Log) Resolve(
        IEntity    attacker,
        IEntity    defender,
        AttackType attackType,
        Random     rng,
        Ratio?     skillPower           = null,
        string?    skillName            = null,
        Ratio?     extraMultiplier      = null,
        string?    extraMultiplierLabel = null)
    {
        var builder = DamageContext.Builder.FromEntity(attacker, attackType);

        var elementalMultiplier = ElementAffinity.GetMultiplier(attacker.Elements, defender.Elements);
        if (elementalMultiplier != Ratio.Full)
            builder.AddModifier(RatioMultiplierModifier.Elemental(elementalMultiplier, attacker.Elements.ToString()));

        if (skillPower is { } power)
            builder.AddModifier(RatioMultiplierModifier.SkillPower(power, skillName ?? "スキル"));

        var isCritical = GameConstants.CriticalRate.Roll(rng);
        if (isCritical)
            builder.AddModifier(RatioMultiplierModifier.Critical(GameConstants.CriticalMultiplier));

        if (extraMultiplier is { } extra)
            builder.AddModifier(new RatioMultiplierModifier(extra, ModifierPhase.PostDefense, extraMultiplierLabel));

        var result       = DamageCalculator.Calculate(builder.Build(), defender);
        var actualDamage = defender.TakeDamage(result.FinalDamage);

        var critText = isCritical ? "会心の一撃！ " : string.Empty;
        var log      = $"{attacker.Name} の攻撃！ {critText}{defender.Name} に {actualDamage} ダメージ。";

        return (actualDamage, log);
    }
}
