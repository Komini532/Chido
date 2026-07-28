using System.Numerics;
using Chido.Core;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle.Damage;

/// <summary>
/// ダメージパイプライン3本の検証（戦闘システム 5.1）。
///
/// 較正の起点は「同格」＝ E = L・Shape 1.00・強さ倍率 1・装備なし・状態変化なし。
/// この条件で ATK = DEF = 8L となり被防御係数がちょうど 0.5、
/// 威力100%の通常攻撃は 4L、最大HP 12L に対して 33.3% になる。
/// </summary>
public class DamagePipelineTests
{
    private static Player PlayerAtLevel(int level, string name = "テスト")
    {
        var player = new Player(userId: 1, name: name, exp: new BigInteger(level) * level);
        player.RestoreToFull();
        return player;
    }

    // --- 較正表 ---

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(5000)]
    public void 同格の通常攻撃は4Lになる(int level)
    {
        // ATK = DEF = 8L のとき ATK² ÷ (ATK + DEF) = 64L² ÷ 16L = 4L
        var attacker = PlayerAtLevel(level);
        var defender = PlayerAtLevel(level);

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(new BigInteger(level) * 4, result.FinalDamage);
    }

    [Fact]
    public void 同格の通常攻撃は最大HPの約3分の1を削る()
    {
        // 4L ÷ 12L = 33.3%。3回で倒せる較正
        var attacker = PlayerAtLevel(100);
        var defender = PlayerAtLevel(100);

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(defender.MaxLife, result.FinalDamage * 3);
    }

    [Fact]
    public void 威力は基礎ダメージへ整数パーセントとして乗る()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);
        var baseline = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        var doubled = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, 200);
        var halved = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, 50);

        Assert.Equal(baseline.FinalDamage * 2, doubled.FinalDamage);
        Assert.Equal(baseline.FinalDamage / 2, halved.FinalDamage);
    }

    // --- 基礎ダメージ式 ---

    [Fact]
    public void 防御力が0なら被防御係数は1になる()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);
        // DEF を 0 にする（-100% デバフ）
        defender.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-100m)));

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(BigInteger.Zero, defender.PDef);
        Assert.Equal(attacker.PAtk, result.FinalDamage);
    }

    [Fact]
    public void 有効ATKと防御力の和が0ならゼロ除算を起こさず最低ダメージになる()
    {
        // 計算自体を発生させず基礎ダメージ0とし、その後 max(0, ...) を経て最低1が適用される
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);

        attacker.AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.FromPercent(-100m)));
        defender.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-100m)));

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(BigInteger.Zero, attacker.PAtk + defender.PDef);
        Assert.Equal(new BigInteger(GameConstants.MinimumDamage), result.FinalDamage);
    }

    [Fact]
    public void 防御力が負値でも基礎ダメージは0未満にならない()
    {
        // レイヤー内加算で DEF が負に振れる経路。max(0, ...) のクランプが受け止める
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);

        defender.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-60m)));
        defender.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-60m)));

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.True(defender.PDef < BigInteger.Zero);
        Assert.True(result.FinalDamage >= GameConstants.MinimumDamage);
    }

    [Fact]
    public void 物理と魔法で参照するステータスが切り替わる()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);

        // 魔法防御だけを半減させ、物理側が影響を受けないことを見る
        defender.AddStatusModifier(new StatusModifier(TargetStatus.MDef, Ratio.FromPercent(-50m)));

        var physical = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);
        var magical = AttackPipeline.Resolve(attacker, defender, AttackType.Magical, GameConstants.PowerScale);

        Assert.Equal(defender.PDef, physical.Defense);
        Assert.Equal(defender.MDef, magical.Defense);
        Assert.True(magical.FinalDamage > physical.FinalDamage);
    }

    // --- 最低ダメージ ---

    [Fact]
    public void 最終ダメージは最低1を保証する()
    {
        // 圧倒的な格差でも0にはならない
        var attacker = PlayerAtLevel(1);
        var defender = PlayerAtLevel(100000);

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(new BigInteger(GameConstants.MinimumDamage), result.FinalDamage);
    }

    [Fact]
    public void 威力0でも最低1のダメージになる()
    {
        var attacker = PlayerAtLevel(100);
        var defender = PlayerAtLevel(100);

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, power: 0);

        Assert.Equal(new BigInteger(GameConstants.MinimumDamage), result.FinalDamage);
    }

    // --- クリティカル ---

    [Fact]
    public void クリティカルは最終ダメージに1_5倍を乗じる()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);

        var normal = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);
        var critical = AttackPipeline.Resolve(
            attacker, defender, AttackType.Physical, GameConstants.PowerScale, isCritical: true);

        Assert.Equal(
            StatCalculator.ApplyLayer(normal.FinalDamage, GameConstants.CriticalMultiplier),
            critical.FinalDamage);
    }

    // --- ダメージ軽減率（DRR） ---

    [Fact]
    public void 防御のDRRは最終ダメージを半減させる()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);
        var normal = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        defender.AddStatusModifier(
            new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));
        var guarded = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(normal.FinalDamage / 2, guarded.FinalDamage);
    }

    [Fact]
    public void DRRは加算合成され途中でクランプされない()
    {
        // 50% × 2 でダメージ1、× 3 でも下限1で止まり回復へ反転しない
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);

        var half = new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate);

        defender.AddStatusModifier(half);
        defender.AddStatusModifier(half);
        Assert.Equal(Ratio.Full, defender.DamageResistRate);
        Assert.Equal(new BigInteger(GameConstants.MinimumDamage),
            AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale).FinalDamage);

        defender.AddStatusModifier(half);
        Assert.Equal(new BigInteger(GameConstants.MinimumDamage),
            AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale).FinalDamage);
    }

    [Fact]
    public void DRRはステータスそのものには影響しない()
    {
        var defender = PlayerAtLevel(1000);
        var basePDef = defender.PDef;

        defender.AddStatusModifier(
            new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));

        Assert.Equal(basePDef, defender.PDef);
    }

    // --- 属性補正 ---

    [Fact]
    public void 属性補正は防御差し引き前のATKに乗る()
    {
        // ATK に乗ってから ATK²÷(ATK+DEF) に入るため、倍率が単純に最終ダメージへ乗るのとは結果が異なる
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);
        defender.SetEquipment([
            EquipmentBonus.None with { ProgressionValue = 1, Elements = Element.Grass },
        ]);

        var neutral = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);
        var advantaged = AttackPipeline.Resolve(
            attacker, defender, AttackType.Physical, GameConstants.PowerScale, motionElements: Element.Fire);

        // 火 → 草 は有利。有効ATK が 1.3 倍になる
        var boostedAtk = ElementAffinity.ApplyToAttack(attacker.PAtk, 1);
        Assert.Equal(boostedAtk, advantaged.EffectiveAtk);
        Assert.Equal(attacker.PAtk, neutral.EffectiveAtk);

        // 最終ダメージの伸びは 1.3 倍より大きい（ATK ごと2乗される形になるため）
        Assert.True(advantaged.FinalDamage * 10 > neutral.FinalDamage * 13);
    }

    [Fact]
    public void 属性相性の攻撃側はモーション属性であり攻撃者の実効属性ではない()
    {
        // 攻撃者が火属性の装備を着けていても、モーションが無属性なら相性は等倍
        var attacker = PlayerAtLevel(1000);
        attacker.SetEquipment([
            EquipmentBonus.None with { ProgressionValue = 1, Elements = Element.Fire },
        ]);
        var defender = PlayerAtLevel(1000);
        defender.SetEquipment([
            EquipmentBonus.None with { ProgressionValue = 1, Elements = Element.Water },
        ]);

        var result = AttackPipeline.Resolve(attacker, defender, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(Element.Fire, attacker.Elements);
        Assert.Equal(attacker.PAtk, result.EffectiveAtk);
    }

    [Fact]
    public void 不利属性は有効ATKを減じる()
    {
        var attacker = PlayerAtLevel(1000);
        var defender = PlayerAtLevel(1000);
        defender.SetEquipment([
            EquipmentBonus.None with { ProgressionValue = 1, Elements = Element.Fire },
        ]);

        var result = AttackPipeline.Resolve(
            attacker, defender, AttackType.Physical, GameConstants.PowerScale, motionElements: Element.Grass);

        // 草 → 火 は不利
        Assert.Equal(ElementAffinity.ApplyToAttack(attacker.PAtk, -1), result.EffectiveAtk);
        Assert.True(result.EffectiveAtk < attacker.PAtk);
    }

    // --- 回復パイプライン ---

    [Fact]
    public void 回復量は有効ATKと威力だけで決まる()
    {
        var healer = PlayerAtLevel(1000);

        Assert.Equal(healer.PAtk, HealPipeline.Resolve(healer, AttackType.Physical, GameConstants.PowerScale));
        Assert.Equal(healer.PAtk / 2, HealPipeline.Resolve(healer, AttackType.Physical, 50));
        Assert.Equal(healer.MAtk * 2, HealPipeline.Resolve(healer, AttackType.Magical, 200));
    }

    [Fact]
    public void 同格では威力50パーセントの回復が通常攻撃と釣り合う()
    {
        // 被防御係数が 0.5 であることの帰結（等価な回復威力 = 攻撃威力 × 被防御係数）
        var entity = PlayerAtLevel(1000);
        var damage = AttackPipeline.Resolve(entity, PlayerAtLevel(1000), AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(damage.FinalDamage, HealPipeline.Resolve(entity, AttackType.Physical, 50));
    }

    [Fact]
    public void 回復の下限は0であり最低1ではない()
    {
        // 丸めで0になる回復は0のままとする（攻撃・スリップの最低1とは異なる）
        var healer = PlayerAtLevel(1);

        Assert.Equal(BigInteger.Zero, HealPipeline.Resolve(healer, AttackType.Physical, power: 0));
    }

    [Fact]
    public void 回復は端数を切り捨てる()
    {
        var healer = PlayerAtLevel(1); // PAtk = 8

        // 8 × 33 ÷ 100 = 2.64 → 2
        Assert.Equal(new BigInteger(2), HealPipeline.Resolve(healer, AttackType.Physical, 33));
    }

    // --- スリップパイプライン ---

    [Fact]
    public void スリップは攻撃と同型の基礎ダメージ式を通る()
    {
        var target = PlayerAtLevel(1000);
        var attacker = PlayerAtLevel(1000);

        var attack = AttackPipeline.Resolve(attacker, target, AttackType.Physical, GameConstants.PowerScale);
        var slip = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), attacker.PAtk, target, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(attack.FinalDamage, slip.FinalDamage);
    }

    [Fact]
    public void スリップはDRRの影響を受けない()
    {
        // DRR は攻撃モーション由来のダメージにのみ登録される
        var target = PlayerAtLevel(1000);
        var snapshotAtk = target.PAtk;
        var before = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale);

        target.AddStatusModifier(
            new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));
        var after = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(before.FinalDamage, after.FinalDamage);
    }

    [Fact]
    public void スリップも属性相性と最低1の保証を受ける()
    {
        var target = PlayerAtLevel(1000);
        target.SetEquipment([
            EquipmentBonus.None with { ProgressionValue = 1, Elements = Element.Grass },
        ]);

        var snapshotAtk = new BigInteger(10000);
        var neutral = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale);
        var advantaged = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale,
            elements: Element.Fire);

        Assert.True(advantaged.FinalDamage > neutral.FinalDamage);

        // 威力0でも最低1
        var minimal = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, power: 0);
        Assert.Equal(new BigInteger(GameConstants.MinimumDamage), minimal.FinalDamage);
    }

    [Fact]
    public void スリップは付与者を攻撃者として記録する()
    {
        // 与ダメージは付与者へ帰属する（毒付与に徹したプレイヤーの貢献を計上するため）
        var granterId = Guid.NewGuid();
        var target = PlayerAtLevel(100);

        var result = SlipDamagePipeline.Resolve(
            granterId, 1000, target, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(granterId, result.AttackerId);
    }

    [Fact]
    public void スリップのATKはスナップショットであり対象や付与者の現在値に依存しない()
    {
        var target = PlayerAtLevel(1000);
        var snapshotAtk = new BigInteger(50000);

        var before = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale);

        // 付与者側に何が起きても、渡すスナップショットが同じなら結果は変わらない
        var after = SlipDamagePipeline.Resolve(
            Guid.NewGuid(), snapshotAtk, target, AttackType.Physical, GameConstants.PowerScale);

        Assert.Equal(before.FinalDamage, after.FinalDamage);
    }
}
