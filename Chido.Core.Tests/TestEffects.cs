using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Core.Tests;

/// <summary>
/// 状態変化レイヤーへ直接書き込むためのテスト補助。
///
/// <see cref="EntityBase"/> はステータス変動・一時付与属性・DRR のすべてを
/// <see cref="EffectInstance"/> の集合から導出しており、それ以外の入力経路を持たない
/// （二重管理を避けるための設計。戦闘システム 2.5・5.4）。
///
/// ステータス算出そのものを検証するテストは「どんな効果から来た変動か」に関心を持たないため、
/// マスタと付与経路を毎回組み立てずに済むよう、1つの変動を包んだ最小のインスタンスをここで作る。
/// 付与の重複判定・減衰・スコープ振り分けを検証するテストは、この近道を使わず
/// <see cref="EffectApplier"/> を通すこと。
/// </summary>
internal static class TestEffects
{
    /// <summary>ステータス変動1つだけを持つ、無期限・戦闘内スコープのインスタンスを付与する。</summary>
    public static EffectInstance AddStatusModifier(this EntityBase entity, StatusModifier modifier)
    {
        var definition = new EffectDefinition(
            effectKey: $"test_{modifier.TargetStatus}".ToLowerInvariant(),
            name: $"テスト効果({modifier.TargetStatus})",
            statusModifiers: [new StatusModifierSpec(modifier.TargetStatus, modifier.Rate)]);

        return entity.AddTestEffect(definition, statusModifiers: [modifier]);
    }

    /// <summary>属性を一時付与する。実効属性の和集合の検証に使う。</summary>
    public static EffectInstance GrantElements(this EntityBase entity, Element elements)
        => entity.AddTestEffect(new EffectDefinition(
            effectKey: $"test_grant_{(int)elements}",
            name: "テスト属性付与",
            grantedElements: elements));

    /// <summary>一時付与属性を持つインスタンスだけを取り除く。</summary>
    public static void ClearGrantedElements(this EntityBase entity)
    {
        foreach (var effect in entity.Effects.Where(e => e.Definition.GrantedElements != Element.None).ToList())
            entity.RemoveEffect(effect);
    }

    /// <summary>ステータス変動を持つインスタンスだけを取り除く。</summary>
    public static void ClearStatusModifiers(this EntityBase entity)
    {
        foreach (var effect in entity.Effects.Where(e => e.StatusModifiers.Count > 0).ToList())
            entity.RemoveEffect(effect);
    }

    /// <summary>
    /// 任意のマスタからインスタンスを1つ作って付与する。重複判定を通さないため、
    /// 同一の effect_key を何度でも併存させられる（レイヤー内加算の検証に必要）。
    ///
    /// <paramref name="statusModifiers"/> を省略した場合はマスタの固定変動を複製する。
    /// 不定値（fixed_rate が NULL）の行は付与モーションからしか実値が決まらないため落とす。
    /// </summary>
    public static EffectInstance AddTestEffect(
        this EntityBase entity,
        EffectDefinition definition,
        AffectReason affectReason = AffectReason.Skill,
        Guid? granterEntityId = null,
        EffectScope scope = EffectScope.Battle,
        string? grantSourceKey = "test_skill",
        ushort? remainingActions = null,
        IEnumerable<StatusModifier>? statusModifiers = null,
        AttackType? slipAttackType = null,
        BigInteger slipAttackSnapshot = default,
        Guid? instanceId = null)
    {
        var instance = new EffectInstance(
            definition,
            affectReason,
            granterEntityId ?? entity.Id,
            scope,
            grantSourceKey,
            remainingActions,
            statusModifiers ?? definition.StatusModifiers
                .Where(s => s.FixedRate is not null)
                .Select(s => new StatusModifier(s.TargetStatus, s.FixedRate!.Value)),
            slipAttackType,
            slipAttackSnapshot,
            instanceId);

        entity.AddEffect(instance);
        return instance;
    }
}
