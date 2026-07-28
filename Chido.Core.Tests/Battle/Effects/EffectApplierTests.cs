using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle.Effects;

/// <summary>
/// 状態変化の付与・解除の検証（戦闘システム 5.4）。
/// </summary>
public class EffectApplierTests
{
    private const string PoisonKey = "poison";
    private const string CurseKey = "curse";
    private const string BuffKey = "atk_up";

    /// <summary>戦闘終了で消える、不定値の攻撃力上昇。effect_rate を付与側から受け取る。</summary>
    private static EffectDefinition Buff => new(
        BuffKey, "攻撃力上昇",
        statusModifiers: [new StatusModifierSpec(TargetStatus.PAtk)]);

    /// <summary>固定変動を持つ効果。マスタ側の値を使い、effect_rate を無視する。</summary>
    private static EffectDefinition Guard => new(
        "guard", "防御",
        statusModifiers:
        [
            new StatusModifierSpec(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate),
        ]);

    private static EffectDefinition Poison => new(
        PoisonKey, "毒", slipDamage: new SlipDamageSpec(Power: 20));

    /// <summary>戦闘を跨ぐ効果。プレイヤーに付与すると永続スコープへ入る。</summary>
    private static EffectDefinition Curse => new(
        CurseKey, "呪い", clearOnBattleEnd: false,
        statusModifiers: [new StatusModifierSpec(TargetStatus.PDef, Ratio.FromPercent(-20m))]);

    private static EffectDefinition Charged => new(
        "charged", "帯電", grantedElements: Element.Thunder);

    private static EffectApplier NewApplier(params EffectDefinition[] definitions)
        => new(definitions.ToDictionary(d => d.EffectKey));

    // --- 重複判定 ---

    [Fact]
    public void 重複時は拒否され残り有効行動数が延長されない()
    {
        // 拒否をデフォルトに置いても「解除 → 付与」の2モーションでリフレッシュを表現できるが、
        // リフレッシュをデフォルトにすると拒否は表現できなくなる（非対称な選択）
        var applier = NewApplier(Buff);
        var (actor, target) = NewPair();

        applier.Grant(actor, target, GrantBuff(duration: 5), "skill_a");

        var effect = Assert.Single(Holder(target).Effects);
        effect.Decay();
        effect.Decay();
        Assert.Equal<ushort?>(3, effect.RemainingActions);

        var message = applier.Grant(actor, target, GrantBuff(duration: 5), "skill_a");

        // 既存インスタンスは増えず、残り有効行動数も戻らない
        Assert.Single(Holder(target).Effects);
        Assert.Equal<ushort?>(3, effect.RemainingActions);
        Assert.Contains("既に", message);
    }

    [Fact]
    public void 付与元スキルが異なれば併存する()
    {
        var applier = NewApplier(Buff);
        var (actor, target) = NewPair();

        applier.Grant(actor, target, GrantBuff(), "skill_a");
        applier.Grant(actor, target, GrantBuff(), "skill_b");

        Assert.Equal(2, Holder(target).Effects.Count);
    }

    [Fact]
    public void 戦闘内スコープでは付与者が異なれば併存する()
    {
        // 「複数の敵から同時に毒を受ける」という構造をそのまま表現するための判定キー
        var applier = NewApplier(Poison);
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var enemyA = NewParticipant(NewEnemy("敵A"), EntityType.Enemy);
        var enemyB = NewParticipant(NewEnemy("敵B"), EntityType.Enemy);

        applier.Grant(enemyA, target, GrantPoison(), "poison_touch");
        applier.Grant(enemyB, target, GrantPoison(), "poison_touch");

        Assert.Equal(2, Holder(target).Effects.Count);
    }

    [Fact]
    public void 永続スコープでは付与者を判定に含めない()
    {
        // granter はセッションごとの使い捨てGuid。判定に含めると同じ敵種と戦うたびに
        // 「重複ではない」と判定され、判定そのものが機能しなくなる
        var applier = NewApplier(Curse);
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var enemyA = NewParticipant(NewEnemy("敵A"), EntityType.Enemy);
        var enemyB = NewParticipant(NewEnemy("敵B"), EntityType.Enemy);

        applier.Grant(enemyA, target, GrantCurse(), "curse_touch");
        var message = applier.Grant(enemyB, target, GrantCurse(), "curse_touch");

        var effect = Assert.Single(Holder(target).Effects);
        Assert.Equal(EffectScope.Player, effect.Scope);
        Assert.Contains("既に", message);
    }

    [Fact]
    public void auto付与はgrant_source_keyがNULL同士で一致し重複が拒否される()
    {
        // SQL の素直な等価比較では NULL = NULL が真にならないため、
        // auto 付与だけが無制限に重複するという気づきにくいバグになる
        var applier = NewApplier(Poison);
        var enemy = NewParticipant(NewEnemy("自滅する敵"), EntityType.Enemy);

        applier.GrantAuto(enemy, PoisonKey, attackType: AttackType.Physical, durationActions: 6);
        var message = applier.GrantAuto(enemy, PoisonKey, attackType: AttackType.Physical, durationActions: 6);

        var effect = Assert.Single(Holder(enemy).Effects);
        Assert.Equal(AffectReason.Auto, effect.AffectReason);
        Assert.Null(effect.GrantSourceKey);
        // auto 付与は自己付与であり、付与者は保持者自身になる
        Assert.Equal(enemy.Entity.Id, effect.GranterEntityId);
        Assert.Contains("既に", message);
    }

    [Fact]
    public void 付与要因が異なれば併存する()
    {
        // affect_reason は grant_source_key が「どのテーブルの可読キーか」を示す型タグであり、
        // 名前空間がテーブルごとに独立している以上、キーの値だけでは区別できない
        var applier = NewApplier(Poison);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        applier.GrantAuto(enemy, PoisonKey, attackType: AttackType.Physical, durationActions: 6);
        applier.Grant(enemy, enemy, GrantPoison(), "poison_touch");

        Assert.Equal(2, Holder(enemy).Effects.Count);
    }

    // --- スコープ ---

    [Fact]
    public void 敵の効果は戦闘を跨ぐ設定でも戦闘内スコープになる()
    {
        // 敵は出現の都度使い捨てのインスタンスであり、永続化する意味を持たない
        var applier = NewApplier(Curse);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        applier.Grant(enemy, enemy, GrantCurse(), "curse_touch");

        Assert.Equal(EffectScope.Battle, Assert.Single(Holder(enemy).Effects).Scope);
    }

    [Fact]
    public void 戦闘を跨ぐ効果に持続がなければ例外になる()
    {
        // テーブルをまたぐ条件のため CHECK 制約では表現できず、ここが唯一の防波堤になる。
        // 真に永久な効果を許すと、加算合成される永続デバフが上限なくステータスを蝕む
        var applier = NewApplier(Curse);
        var (actor, target) = NewPair();

        Assert.Throws<InvalidOperationException>(
            () => applier.Grant(actor, target, GrantCurse(duration: null), "curse_touch"));
    }

    [Fact]
    public void 戦闘終了で戦闘内スコープのみが落ちる()
    {
        var applier = NewApplier(Buff, Curse);
        var (actor, target) = NewPair();

        applier.Grant(actor, target, GrantBuff(), "skill_a");
        applier.Grant(actor, target, GrantCurse(), "curse_touch");

        Holder(target).ClearBattleScopedEffects();

        var remaining = Assert.Single(Holder(target).Effects);
        Assert.Equal(CurseKey, remaining.EffectKey);
    }

    // --- ステータス変動 ---

    [Fact]
    public void 併存する同種効果はレイヤー内で加算される()
    {
        // +10% が2つで ×1.2。レイヤーごとに乗算すると ×1.21 になってしまう
        var applier = NewApplier(Buff);
        var (actor, target) = NewPair();
        var baseAtk = target.Entity.PAtk;

        applier.Grant(actor, target, GrantBuff(rate: Ratio.FromPercent(10m)), "skill_a");
        applier.Grant(actor, target, GrantBuff(rate: Ratio.FromPercent(10m)), "skill_b");

        Assert.Equal(baseAtk * 12 / 10, target.Entity.PAtk);
    }

    [Fact]
    public void 固定変動はマスタの値を使い付与側のeffect_rateを無視する()
    {
        var applier = NewApplier(Guard);
        var (actor, target) = NewPair();

        applier.Grant(
            actor, target,
            new GrantEffectMotion(0, TargetRule.Myself, Ratio.Full, "guard", EffectRate: Ratio.FromPercent(99m)),
            GameConstants.DefendSkillKey);

        Assert.Equal(GameConstants.DefendDamageResistRate, target.Entity.DamageResistRate);
    }

    [Fact]
    public void 不定値の効果にeffect_rateが供給されなければ例外になる()
    {
        var applier = NewApplier(Buff);
        var (actor, target) = NewPair();

        // 付与モーションが effect_rate を持たないため、変動量を決める手段がどこにもない
        var motion = new GrantEffectMotion(0, TargetRule.Ally, Ratio.Full, BuffKey, DurationActions: 5);

        Assert.Throws<InvalidOperationException>(() => applier.Grant(actor, target, motion, "skill_a"));
    }

    [Fact]
    public void 一時付与属性は実効属性の和集合に入る()
    {
        var applier = NewApplier(Charged);
        var enemy = NewParticipant(NewEnemy("敵", innate: Element.Fire), EntityType.Enemy);

        Assert.Equal(Element.Fire, enemy.Entity.Elements);

        applier.Grant(enemy, enemy, new GrantEffectMotion(0, TargetRule.Myself, Ratio.Full, "charged"), "charge");

        Assert.Equal(Element.Fire | Element.Thunder, enemy.Entity.Elements);
    }

    // --- SlipDamage のスナップショット ---

    [Fact]
    public void 攻撃種別により物理魔法いずれのATKがスナップショットされるかが決まる()
    {
        // 同一の effect_key でも、物理スキルで付与されたら物理スリップになる
        var applier = NewApplier(Poison);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);
        var physicalTarget = NewParticipant(NewPlayer("A"), EntityType.Player);
        var magicalTarget = NewParticipant(NewPlayer("B"), EntityType.Player);

        applier.Grant(enemy, physicalTarget, GrantPoison(AttackType.Physical), "poison_touch");
        applier.Grant(enemy, magicalTarget, GrantPoison(AttackType.Magical), "poison_bolt");

        Assert.Equal(enemy.Entity.PAtk, Assert.Single(Holder(physicalTarget).Effects).SlipAttackSnapshot);
        Assert.Equal(enemy.Entity.MAtk, Assert.Single(Holder(magicalTarget).Effects).SlipAttackSnapshot);
    }

    [Fact]
    public void スナップショットは付与後の付与者のステータス変動に追随しない()
    {
        var applier = NewApplier(Poison);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var atkAtGrant = enemy.Entity.PAtk;

        applier.Grant(enemy, target, GrantPoison(), "poison_touch");

        // 付与後に付与者が強化されても、既に付与済みのスリップは強くならない
        Holder(enemy).AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.Full));

        Assert.True(enemy.Entity.PAtk > atkAtGrant);
        Assert.Equal(atkAtGrant, Assert.Single(Holder(target).Effects).SlipAttackSnapshot);
    }

    [Fact]
    public void SlipDamage成分に攻撃種別が供給されなければ例外になる()
    {
        var applier = NewApplier(Poison);
        var (actor, target) = NewPair();

        Assert.Throws<InvalidOperationException>(
            () => applier.Grant(actor, target, GrantPoison(attackType: null), "poison_touch"));
    }

    // --- 解除 ---

    [Fact]
    public void 解除は付与者と付与元を問わず全スコープから消す()
    {
        // 「解毒」は毒の出所を問わない。付与の重複判定の5値を反射的に流用しないこと（意図的な非対称）
        var applier = NewApplier(Poison);
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var enemyA = NewParticipant(NewEnemy("敵A"), EntityType.Enemy);
        var enemyB = NewParticipant(NewEnemy("敵B"), EntityType.Enemy);

        applier.Grant(enemyA, target, GrantPoison(), "poison_touch");
        applier.Grant(enemyB, target, GrantPoison(), "poison_bolt");
        applier.GrantAuto(target, PoisonKey, attackType: AttackType.Physical, durationActions: 3);
        Assert.Equal(3, Holder(target).Effects.Count);

        var message = applier.Dispel(target, new DispelEffectMotion(0, TargetRule.Ally, Ratio.Full, PoisonKey));

        Assert.Empty(Holder(target).Effects);
        Assert.Contains("解除", message);
    }

    [Fact]
    public void 解除は一致しない効果を残す()
    {
        var applier = NewApplier(Poison, Buff);
        var (actor, target) = NewPair();

        applier.Grant(actor, target, GrantPoison(), "poison_touch");
        applier.Grant(actor, target, GrantBuff(), "skill_a");

        applier.Dispel(target, new DispelEffectMotion(0, TargetRule.Ally, Ratio.Full, PoisonKey));

        Assert.Equal(BuffKey, Assert.Single(Holder(target).Effects).EffectKey);
    }

    [Fact]
    public void 解除の空振りも通知される()
    {
        // 通知がなければプレイヤーには「スキルが不発になった」としか映らない
        var applier = NewApplier(Poison);
        var (_, target) = NewPair();

        var message = applier.Dispel(target, new DispelEffectMotion(0, TargetRule.Ally, Ratio.Full, PoisonKey));

        Assert.Contains("状態ではありません", message);
    }

    [Fact]
    public void マスタに存在しない効果の付与は例外になる()
    {
        var applier = NewApplier(Buff);
        var (actor, target) = NewPair();

        Assert.Throws<InvalidOperationException>(
            () => applier.Grant(actor, target, GrantPoison(), "poison_touch"));
    }

    // --- ヘルパ ---

    private static GrantEffectMotion GrantBuff(Ratio? rate = null, ushort? duration = 5)
        => new(0, TargetRule.Ally, Ratio.Full, BuffKey,
            EffectRate: rate ?? Ratio.FromPercent(10m), DurationActions: duration);

    private static GrantEffectMotion GrantPoison(
        AttackType? attackType = AttackType.Physical, ushort? duration = 3)
        => new(0, TargetRule.Enemy, Ratio.Full, PoisonKey,
            AttackType: attackType, DurationActions: duration);

    private static GrantEffectMotion GrantCurse(ushort? duration = 10)
        => new(0, TargetRule.Enemy, Ratio.Full, CurseKey, DurationActions: duration);

    private static EntityBase Holder(BattleParticipant participant) => (EntityBase)participant.Entity;

    private static Player NewPlayer(string name, int level = 100)
    {
        var p = new Player(userId: 1, name: name, exp: new BigInteger(level) * level);
        p.RestoreToFull();
        return p;
    }

    private static Enemy NewEnemy(string name, int level = 100, Element innate = Element.None)
    {
        var e = new Enemy(
            masterKey: "test", name: name, level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500, innateElements: innate);
        e.RestoreToFull();
        return e;
    }

    private static BattleParticipant NewParticipant(EntityBase entity, EntityType type) =>
        new(entity, type,
            discordUserId: type == EntityType.Player ? 1UL : null,
            enemyId: type == EntityType.Enemy ? Guid.NewGuid() : null);

    private static (BattleParticipant Actor, BattleParticipant Target) NewPair()
        => (NewParticipant(NewPlayer("行動者"), EntityType.Player),
            NewParticipant(NewPlayer("対象"), EntityType.Player));
}
