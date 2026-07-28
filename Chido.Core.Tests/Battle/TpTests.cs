using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle;

/// <summary>
/// TPシステムの検証（戦闘システム 4.4）。
///
/// レベル100の同格同士では 最大HP = 1200・通常攻撃 = 400 であるため、
/// 被反撃1回あたりの蓄積は floor(500 × 400 ÷ 1200) = 166 になる。
/// </summary>
public class TpTests
{
    private const int SameTierCounterAttackTp = 166;

    /// <summary>防御。自分自身への DRR 付与モーション1つで、反撃モーションを含まない。</summary>
    private static EffectDefinition GuardEffect => new(
        "guard", "防御",
        statusModifiers:
        [
            new StatusModifierSpec(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate),
        ]);

    private static Skill DefendSkill(Ratio? accuracy = null) =>
        new(GameConstants.DefendSkillKey, "防御",
            [new GrantEffectMotion(0, TargetRule.Myself, accuracy ?? Ratio.Full, "guard", DurationActions: 1)],
            priority: 10);

    private static EffectDefinition Paralysis => new(
        "paralysis", "麻痺", disableRate: Ratio.Full);

    private static EffectDefinition Poison => new(
        "poison", "毒", slipDamage: new SlipDamageSpec(Power: 20));

    private static Random Deterministic => new(20260728);

    // --- 蓄積と上限 ---

    [Fact]
    public void 上限を超えた蓄積はカットされ繰り越されない()
    {
        var participant = NewParticipant(NewPlayer("A"), EntityType.Player);

        participant.GainTp(GameConstants.TpMax);
        participant.GainTp(500);

        Assert.Equal(GameConstants.TpMax, participant.CurrentTp);

        // 繰り越されていれば、消費した分だけ即座に戻ってしまう
        participant.TrySpendTp(500);
        Assert.Equal(500, participant.CurrentTp);
    }

    [Fact]
    public void 通常攻撃モーションの再生でTPが100蓄積する()
    {
        var (_, player, enemy) = NewBattle();

        new SkillPlayer().Play(player, AttackSkill(), enemy, Deterministic);

        Assert.Equal(GameConstants.TpGainOnAttackMotion, player.CurrentTp);
    }

    [Fact]
    public void 防御モーションの再生でTPが100蓄積する()
    {
        var (_, player, enemy) = NewBattle();
        var applier = new EffectApplier(new Dictionary<string, EffectDefinition> { ["guard"] = GuardEffect });

        new SkillPlayer(applier).Play(player, DefendSkill(), enemy, Deterministic);

        Assert.Equal(GameConstants.TpGainOnDefendMotion, player.CurrentTp);
        Assert.Equal(GameConstants.DefendDamageResistRate, player.Entity.DamageResistRate);
    }

    [Fact]
    public void 命中を外したモーションはTPを生まない()
    {
        // 「再生されなかったものはTPを生まない」という単一の規則で閉じる。
        // 外れたモーションはステップ4で止まり効果適用に到達しない
        var (_, player, enemy) = NewBattle();
        var neverHits = new Skill(GameConstants.AttackSkillKey, "通常攻撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Zero, AttackType.Physical, GameConstants.PowerScale)]);

        new SkillPlayer().Play(player, neverHits, enemy, Deterministic);

        Assert.Equal(0, player.CurrentTp);
    }

    [Fact]
    public void 通常攻撃以外のスキルの攻撃モーションはTPを生まない()
    {
        // 契機は skill_key に紐づく。任意の攻撃スキルで +100 が出ると、
        // 強力なスキルを撃つほどTPが戻る構造になってしまう
        var (_, player, enemy) = NewBattle();
        var special = new Skill("fire_slash", "火炎斬り",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, 150)]);

        new SkillPlayer().Play(player, special, enemy, Deterministic);

        Assert.Equal(0, player.CurrentTp);
    }

    // --- 被攻撃TP ---

    [Theory]
    [InlineData(1200, 1200, 500)] // 最大HPぶんの被弾で満額
    [InlineData(1200, 600, 250)]
    [InlineData(1200, 400, SameTierCounterAttackTp)] // 同格の反撃1回
    [InlineData(1200, 1, 0)] // floor により0に落ちる
    public void 被弾でfloor500倍の割合が蓄積する(int maxLife, int damage, int expected)
    {
        // 最大HP 1200 のプレイヤーを作るためレベル100を使う
        var participant = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        Assert.Equal(maxLife, participant.Entity.MaxLife);

        participant.GainTpOnDamaged(damage);

        Assert.Equal(expected, participant.CurrentTp);
    }

    [Fact]
    public void 反撃を受けた側にTPが蓄積する()
    {
        var (session, player, _) = NewBattle();

        new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        // 攻撃の +100 と被反撃分
        Assert.Equal(GameConstants.TpGainOnAttackMotion + SameTierCounterAttackTp, player.CurrentTp);
    }

    [Fact]
    public void SlipDamageでもインスタンス単位で被攻撃TPが蓄積する()
    {
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);

        // 威力20%＝80ダメージ。floor(500 × 80 ÷ 1200) = 33
        AddPoison(target, Guid.NewGuid(), Id(1));
        AddPoison(target, Guid.NewGuid(), Id(2));

        SlipDamageRunner.Run(target);

        Assert.Equal(66, target.CurrentTp);
    }

    [Fact]
    public void とどめ以降の実効0ではTPが蓄積しない()
    {
        // 与ダメージ帰属・被攻撃TP・報酬ゲートの三者が同じ実効ダメージを基準量とするため、
        // 三者一貫して「効果なし」に倒れる
        var target = NewParticipant(NewPlayer("瀕死"), EntityType.Player);
        target.Entity.TakeDamage(target.Entity.CurrentLife - 10);

        AddPoison(target, Guid.NewGuid(), Id(1));
        AddPoison(target, Guid.NewGuid(), Id(2));

        SlipDamageRunner.Run(target);

        // 1件目の実効10ぶんのみ。2件目は実効0で蓄積しない
        Assert.Equal(4, target.CurrentTp);
    }

    [Fact]
    public void 自滅スリップでは被弾側である自分自身のTPが蓄積する()
    {
        var enemy = NewParticipant(NewEnemy("自滅する敵"), EntityType.Enemy);
        AddPoison(enemy, granterId: enemy.Entity.Id, instanceId: Id(1));

        SlipDamageRunner.Run(enemy);

        Assert.True(enemy.CurrentTp > 0);
    }

    // --- 消費 ---

    [Fact]
    public void 発動時にrequire_tpが消費される()
    {
        // 上限に張り付かせると被反撃分がカットに吸われ、行動順に依存する期待値になってしまう
        var (session, player, _) = NewBattle();
        player.GainTp(500);

        new TurnResolver(new SkillPlayer()).Resolve(
            player, CostlySkill(requireTp: 300), session, Deterministic, _ => AttackSkill());

        // 500 - 300 + 被反撃166。攻撃スキルだが skill_key が通常攻撃ではないため +100 は付かない
        Assert.Equal(500 - 300 + SameTierCounterAttackTp, player.CurrentTp);
    }

    [Fact]
    public void TPが足りなければ消費されない()
    {
        var participant = NewParticipant(NewPlayer("A"), EntityType.Player);
        participant.GainTp(100);

        Assert.False(participant.TrySpendTp(300));
        Assert.Equal(100, participant.CurrentTp);
        Assert.False(participant.CanAfford(300));
        Assert.True(participant.CanAfford(100));
    }

    [Fact]
    public void 行動不能ならrequire_tpを消費しない()
    {
        // スキル発動そのものが起きていないため、TPを取ると二重罰になる（A-7-g）
        var (session, player, _) = NewBattle();
        player.GainTp(500);
        ((EntityBase)player.Entity).AddTestEffect(Paralysis);

        new TurnResolver(new SkillPlayer()).Resolve(
            player, CostlySkill(requireTp: 300), session, Deterministic, _ => AttackSkill());

        // 消費なし。+100 も無い（モーションが再生されていないため）が、被反撃分は通常通り入る
        Assert.Equal(500 + SameTierCounterAttackTp, player.CurrentTp);
    }

    [Fact]
    public void 行動不能でも被攻撃TPは通常通り蓄積する()
    {
        // 行動不能はTP契機に特別扱いを設けない。相手の反撃は成否によらず起きる
        var (session, player, _) = NewBattle();
        ((EntityBase)player.Entity).AddTestEffect(Paralysis);

        new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.Equal(SameTierCounterAttackTp, player.CurrentTp);
    }

    [Fact]
    public void 敵は初期TPを持って出現しプレイヤーは常に0で始まる()
    {
        // この非対称は意図的な据え置き。初手から require_tp>0 のスキルを撃たせたい敵のための拡張
        var enemy = new BattleParticipant(
            NewEnemy("敵"), EntityType.Enemy, enemyId: Guid.NewGuid(), initialTp: 400);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player);

        Assert.Equal(400, enemy.CurrentTp);
        Assert.Equal(0, player.CurrentTp);
    }

    // --- ヘルパ ---

    private static Guid Id(byte n) => new([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, n]);

    private static void AddPoison(BattleParticipant target, Guid granterId, Guid instanceId)
        => ((EntityBase)target.Entity).AddTestEffect(
            Poison,
            granterEntityId: granterId,
            grantSourceKey: $"poison_{instanceId}",
            slipAttackType: AttackType.Physical,
            slipAttackSnapshot: new BigInteger(800),
            instanceId: instanceId);

    private static Skill AttackSkill() =>
        new(GameConstants.AttackSkillKey, "通常攻撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, GameConstants.PowerScale)]);

    private static Skill CostlySkill(ushort requireTp) =>
        new("heavy_strike", "強撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, 150)],
            requireTp: requireTp);

    private static Player NewPlayer(string name, int level = 100)
    {
        var p = new Player(userId: 1, name: name, exp: new BigInteger(level) * level);
        p.RestoreToFull();
        return p;
    }

    private static Enemy NewEnemy(string name, int level = 100)
    {
        var e = new Enemy(
            masterKey: "test", name: name, level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500);
        e.RestoreToFull();
        return e;
    }

    private static BattleParticipant NewParticipant(EntityBase entity, EntityType type) =>
        new(entity, type,
            discordUserId: type == EntityType.Player ? 1UL : null,
            enemyId: type == EntityType.Enemy ? Guid.NewGuid() : null);

    private static (BattleSession Session, BattleParticipant Player, BattleParticipant Enemy) NewBattle()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);
        session.AddParticipant(player);
        session.AddParticipant(enemy);
        return (session, player, enemy);
    }
}
