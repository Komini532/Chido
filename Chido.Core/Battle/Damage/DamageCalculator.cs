using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Damage;

public static class DamageCalculator
{
    public static DamageResult Calculate(DamageContext ctx, IEntity target)
    {
        // ── 1. 有効防御値 = DEF * (1 - 貫通率) ──────────────────────────
        var baseDef = ctx.AttackType == AttackType.Physical ? target.PDef : target.MDef;
        var effectiveDef = BigInteger.Max(
            BigInteger.Zero,
            baseDef - ctx.DefensePenetration.Of(baseDef));

        // ── 2. PreDefense フェーズ: 有効 ATK 確定 ────────────────────────
        var effectiveAtk = ApplyPhase(ctx.RawAtk, ModifierPhase.PreDefense, ctx);

        // ── 3. 基礎ダメージ (ATK - DEF, 下限 0) ─────────────────────────
        var baseDamage         = BigInteger.Max(BigInteger.Zero, effectiveAtk - effectiveDef);
        var preModifierDamage  = baseDamage;

        // ── 4. PostDefense フェーズ: スキル倍率・属性・クリティカル ────────
        var damage = ApplyPhase(baseDamage, ModifierPhase.PostDefense, ctx);

        // ── 5. Flat フェーズ ─────────────────────────────────────────────
        damage = ApplyPhase(damage, ModifierPhase.Flat, ctx);

        // ── 6. Final フェーズ ────────────────────────────────────────────
        damage = ApplyPhase(damage, ModifierPhase.Final, ctx);

        // ── 7. 最低ダメージ保証 ──────────────────────────────────────────
        damage = BigInteger.Max(BigInteger.One, damage);

        var modifierLog = ctx.Modifiers
            .Where(m  => m.LogLabel is not null)
            .Select(m => m.LogLabel!)
            .ToList()
            .AsReadOnly();

        return new DamageResult(
            FinalDamage:       damage,
            BaseDefense:       baseDef,
            EffectiveDefense:  effectiveDef,
            PreModifierDamage: preModifierDamage,
            AttackType:        ctx.AttackType,
            AttackerId:        ctx.AttackerId,
            ModifierLog:       modifierLog);
    }

    // ── private ──────────────────────────────────────────────────────────
    private static BigInteger ApplyPhase(
        BigInteger current, ModifierPhase phase, DamageContext ctx)
        => ctx.Modifiers
            .Where(m => m.Phase == phase)
            .Aggregate(current, (dmg, mod) => mod.Apply(dmg, ctx));
}
