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
/// 状態変化の実行時挙動（減衰・継続ダメージ・行動不能）の検証（戦闘システム 5.4）。
/// </summary>
public class EffectRuntimeTests
{
    /// <summary>威力20%の毒。同格同士なら1発動あたり 80 ダメージになる。</summary>
    private static EffectDefinition Poison => new(
        "poison", "毒", slipDamage: new SlipDamageSpec(Power: 20));

    private static EffectDefinition Paralysis(int percent) => new(
        $"paralysis_{percent}", "麻痺", disableRate: Ratio.FromPercent(percent));

    private static EffectDefinition Buff => new(
        "atk_up", "攻撃力上昇",
        statusModifiers: [new StatusModifierSpec(TargetStatus.PAtk, Ratio.FromPercent(10m))]);

    /// <summary>instance_id の格納バイト列がそのまま昇順になるGuidを作る。</summary>
    private static Guid Id(byte n) => new([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, n]);

    private static Random Deterministic => new(20260728);

    // --- 減衰（関与者集合） ---

    [Fact]
    public void 関与者集合の外側は減衰しない()
    {
        // 味方に付与したバフは、その味方自身が行動するまで残り有効行動数を保つ
        // （弾は撃つから減る、という比喩と一貫する）
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var counter = NewParticipant(NewEnemy("敵"), EntityType.Enemy);
        var bystander = NewParticipant(NewPlayer("第三者"), EntityType.Player);

        var inSet = Holder(actor).AddTestEffect(Buff, remainingActions: 3);
        var outOfSet = Holder(bystander).AddTestEffect(Buff, remainingActions: 3);

        EffectDecay.Apply(actor, counter);

        Assert.Equal<ushort?>(2, inSet.RemainingActions);
        Assert.Equal<ushort?>(3, outOfSet.RemainingActions);
    }

    [Fact]
    public void 使い切ったインスタンスは同じ操作の中で取り除かれる()
    {
        // remaining_actions = 0 の行が他から観測されないようにするため、減算と削除を分けない
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var counter = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        Holder(actor).AddTestEffect(Buff, remainingActions: 1);
        var baseAtk = actor.Entity.PAtk;

        var expired = EffectDecay.Apply(actor, counter);

        Assert.Empty(Holder(actor).Effects);
        Assert.Same(actor, Assert.Single(expired).Holder);
        // 補正が実際に消えている（保持リストからの削除がステータスへ直結している）
        Assert.Equal(baseAtk * 10 / 11, actor.Entity.PAtk);
    }

    [Fact]
    public void 無期限のインスタンスは減衰しない()
    {
        // SQL の NULL - 1 = NULL / WHERE remaining_actions = 0 が UNKNOWN を返す挙動と一致する
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var counter = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        var endless = Holder(actor).AddTestEffect(Buff, remainingActions: null);

        for (var i = 0; i < 100; i++) EffectDecay.Apply(actor, counter);

        Assert.Null(endless.RemainingActions);
        Assert.False(endless.IsExpired);
        Assert.Single(Holder(actor).Effects);
    }

    [Fact]
    public void 減衰は両スコープに一律に及ぶ()
    {
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var counter = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        var battleScoped = Holder(actor).AddTestEffect(Buff, scope: EffectScope.Battle, remainingActions: 3);
        var playerScoped = Holder(actor).AddTestEffect(
            Buff, scope: EffectScope.Player, grantSourceKey: "other", remainingActions: 3);

        EffectDecay.Apply(actor, counter);

        Assert.Equal<ushort?>(2, battleScoped.RemainingActions);
        Assert.Equal<ushort?>(2, playerScoped.RemainingActions);
    }

    [Fact]
    public void 戦闘不能になっていても関与者集合に入っていれば減衰する()
    {
        // 「行動しないから減らない」のではなく「関与者集合に入らないから減らない」が正確な理由
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var counter = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        var effect = Holder(counter).AddTestEffect(Buff, remainingActions: 3);
        counter.MarkDefeated();

        EffectDecay.Apply(actor, counter);

        Assert.Equal<ushort?>(2, effect.RemainingActions);
    }

    // --- SlipDamage ---

    [Fact]
    public void 併存するインスタンスはそれぞれ発動する()
    {
        // SlipDamage は補正値ではなく独立したダメージ発生源であるため、レイヤー内加算の対象外。
        // 毒3つなら3回ダメージが入る
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var fullLife = target.Entity.CurrentLife;

        for (var i = 0; i < 3; i++) AddPoison(target, granterId: Guid.NewGuid(), instanceId: Id((byte)i));

        var logs = SlipDamageRunner.Run(target);

        Assert.Equal(3, logs.Count);
        Assert.Equal(fullLife - 240, target.Entity.CurrentLife); // 80 × 3
    }

    [Fact]
    public void 与ダメージは付与者へ帰属する()
    {
        // 素直に実装すると「敵が自分自身に与えたダメージ」になり、毒付与に徹したプレイヤーが
        // 報酬ゲートすら通過できなくなる（戦闘システム 6.2）
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var granterId = Guid.NewGuid();
        AddPoison(target, granterId);

        var ledger = new List<(Guid Attacker, string Victim, BigInteger Damage)>();
        SlipDamageRunner.Run(target, (a, t, d) => ledger.Add((a, t.Entity.Name, d)));

        var entry = Assert.Single(ledger);
        Assert.Equal(granterId, entry.Attacker);
        Assert.Equal("被害者", entry.Victim);
        Assert.Equal(80, entry.Damage);
    }

    [Fact]
    public void とどめ以降のインスタンスの実効ダメージは0になる()
    {
        // instance_id 順に発動するため、「とどめのインスタンス」の帰属は決定的になる
        var target = NewParticipant(NewPlayer("瀕死"), EntityType.Player);
        target.Entity.TakeDamage(target.Entity.CurrentLife - 10);

        var firstGranter = Guid.NewGuid();
        var secondGranter = Guid.NewGuid();
        AddPoison(target, firstGranter, instanceId: Id(1));
        AddPoison(target, secondGranter, instanceId: Id(2));

        var ledger = new List<(Guid Attacker, BigInteger Damage)>();
        SlipDamageRunner.Run(target, (a, _, d) => ledger.Add((a, d)));

        // 発動順は付与順ではなく instance_id 順
        Assert.Equal(firstGranter, ledger[0].Attacker);
        Assert.Equal(10, ledger[0].Damage); // 実効ダメージは適用直前HPで頭打ちになる
        Assert.Equal(secondGranter, ledger[1].Attacker);
        Assert.Equal(BigInteger.Zero, ledger[1].Damage);
        Assert.Equal(ParticipantStatus.Defeated, target.Status);
    }

    [Fact]
    public void 発動順は付与順ではなくinstance_id順になる()
    {
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        var late = Guid.NewGuid();
        var early = Guid.NewGuid();

        // 付与は late → early の順だが、instance_id は early のほうが小さい
        AddPoison(target, late, instanceId: Id(9));
        AddPoison(target, early, instanceId: Id(2));

        var order = new List<Guid>();
        SlipDamageRunner.Run(target, (a, _, _) => order.Add(a));

        Assert.Equal([early, late], order);
    }

    [Fact]
    public void SlipDamageはDRRの影響を受けない()
    {
        var withoutDrr = NewParticipant(NewPlayer("素の被害者"), EntityType.Player);
        var withDrr = NewParticipant(NewPlayer("防御中の被害者"), EntityType.Player);
        Holder(withDrr).AddStatusModifier(
            new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));

        AddPoison(withoutDrr, Guid.NewGuid());
        AddPoison(withDrr, Guid.NewGuid());

        var plain = BigInteger.Zero;
        var guarded = BigInteger.Zero;
        SlipDamageRunner.Run(withoutDrr, (_, _, d) => plain = d);
        SlipDamageRunner.Run(withDrr, (_, _, d) => guarded = d);

        Assert.Equal(plain, guarded);
    }

    [Fact]
    public void 対象の防御力は発動時に取得される()
    {
        // 「毒を受けた後に装備を変えて対象DEFを上げるとスリップが軽くなる」は意図した挙動。
        // スナップショットするのは付与時ATKと攻撃種別のみ
        var target = NewParticipant(NewPlayer("被害者"), EntityType.Player);
        AddPoison(target, Guid.NewGuid());

        var before = BigInteger.Zero;
        SlipDamageRunner.Run(target, (_, _, d) => before = d);

        Holder(target).AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.Full));

        var after = BigInteger.Zero;
        SlipDamageRunner.Run(target, (_, _, d) => after = d);

        Assert.True(after < before);
    }

    [Fact]
    public void 離脱した参加者にはSlipDamageが発動しない()
    {
        // 戦場に存在しない相手を削り続けないための例外。戦闘不能はこの限りではなく、
        // 実効0へ落ちる経路（6.2）がそのまま働く
        var target = NewParticipant(NewPlayer("逃走者"), EntityType.Player);
        AddPoison(target, Guid.NewGuid());
        var lifeAtEscape = target.Entity.CurrentLife;
        target.MarkEscaped();

        Assert.Empty(SlipDamageRunner.Run(target));
        Assert.Equal(lifeAtEscape, target.Entity.CurrentLife);
    }

    // --- DisableMove ---

    [Fact]
    public void 行動不能は毎回抽選される()
    {
        // 付与時に固定せず、行動しようとするたびに引く。50% を何度も引けば両方の結果が出る
        var participant = NewParticipant(NewPlayer("麻痺者"), EntityType.Player);
        Holder(participant).AddTestEffect(Paralysis(50));

        var outcomes = Enumerable.Range(0, 40)
            .Select(seed => DisableMoveJudge.Judge(participant, new Random(seed)) is not null)
            .Distinct()
            .ToList();

        Assert.Equal(2, outcomes.Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    public void 確率の両端は決定的に振る舞う(int percent, bool expected)
    {
        var participant = NewParticipant(NewPlayer("麻痺者"), EntityType.Player);
        Holder(participant).AddTestEffect(Paralysis(percent));

        Assert.Equal(expected, DisableMoveJudge.Judge(participant, Deterministic) is not null);
    }

    [Fact]
    public void 併存する行動不能はinstance_id昇順に引かれ最初の成功で打ち切られる()
    {
        // 確率であるため StatusModifier の加算合成は適用しない。
        // 打ち切りにより消費される乱数の個数が変わるため、抽選順は決定性の一部
        var participant = NewParticipant(NewPlayer("麻痺者"), EntityType.Player);
        var second = Holder(participant).AddTestEffect(
            Paralysis(100), grantSourceKey: "b", instanceId: Id(2));
        var first = Holder(participant).AddTestEffect(
            Paralysis(100), grantSourceKey: "a", instanceId: Id(1));

        var rng = new Random(7);
        var disabled = DisableMoveJudge.Judge(participant, rng);

        Assert.Same(first, disabled);
        Assert.NotSame(second, disabled);

        // 打ち切られているため、消費された乱数はちょうど1つ
        var reference = new Random(7);
        reference.Next(10000);
        Assert.Equal(reference.Next(), rng.Next());
    }

    [Fact]
    public void 行動不能を持たない相手は判定に乱数を消費しない()
    {
        var participant = NewParticipant(NewPlayer("健常者"), EntityType.Player);
        Holder(participant).AddTestEffect(Buff);

        var rng = new Random(7);
        Assert.Null(DisableMoveJudge.Judge(participant, rng));
        Assert.Equal(new Random(7).Next(), rng.Next());
    }

    // --- ターンへの統合 ---

    [Fact]
    public void 行動不能ならモーション再生のみがスキップされ反撃は通常通り起きる()
    {
        // 成否によらず行われるのはターン消費・TP蓄積・相手の反撃・減衰の4項。
        // 成立時にスキップされるのはスキル1本ぶんのモーション再生のみ
        var (session, player, enemy) = NewBattle();
        Holder(player).AddTestEffect(Paralysis(100));
        var enemyLife = enemy.Entity.CurrentLife;

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.NotNull(ResultOf(result, player));
        Assert.Equal(enemyLife, enemy.Entity.CurrentLife);              // 攻撃は届いていない
        Assert.True(player.Entity.CurrentLife < player.Entity.MaxLife); // 反撃は受けている
    }

    [Fact]
    public void 行動不能でもSlipDamageは発動する()
    {
        // 発動しないと、行動不能と毒を併せ持つ相手に対して毒が実質無効化される
        var (session, player, _) = NewBattle();
        Holder(player).AddTestEffect(Paralysis(100));
        AddPoison(player, Guid.NewGuid());

        var slipVictims = new List<string>();
        new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill(),
            onDamageDealt: (_, t, _) => slipVictims.Add(t.Entity.Name));

        Assert.Contains("プレイヤー", slipVictims);
    }

    [Fact]
    public void 行動不能でも残り有効行動数は減衰する()
    {
        // これにより「行動不能が永久に解けない」実装にはならない
        var (session, player, _) = NewBattle();
        var paralysis = Holder(player).AddTestEffect(Paralysis(100), remainingActions: 2);

        new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.Equal<ushort?>(1, paralysis.RemainingActions);
    }

    [Fact]
    public void 後攻がキャンセルされても関与者集合は減衰する()
    {
        // 行動キャンセル・撃破・行動不能はいずれも関与者集合を変えない
        var (session, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1, enemySpeed: 1);
        var playerEffect = Holder(player).AddTestEffect(Buff, remainingActions: 3);
        var enemyEffect = Holder(enemy).AddTestEffect(Buff, remainingActions: 3);

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.True(result.SecondCancelled);
        Assert.Equal<ushort?>(2, playerEffect.RemainingActions);
        Assert.Equal<ushort?>(2, enemyEffect.RemainingActions);
    }

    [Fact]
    public void 後攻がキャンセルされると後攻のSlipDamageは発動しない()
    {
        // 行動枠そのものが開かないため（/escape でスリップが発動しないのと同じ理由）
        var (session, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1, enemySpeed: 1);
        AddPoison(enemy, Guid.NewGuid());

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.True(result.SecondCancelled);
        Assert.DoesNotContain(result.Logs, log => log.Contains("毒"));
    }

    [Fact]
    public void ターンの与ダメージ通知は攻撃者と付与者を同じ形で運ぶ()
    {
        // ライブ攻撃では行動者、SlipDamage では付与者。台帳はどちらも entity_id で受ける
        var (session, player, enemy) = NewBattle();
        var poisonGranter = Guid.NewGuid();
        AddPoison(enemy, poisonGranter);

        var attackers = new List<Guid>();
        new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill(),
            onDamageDealt: (a, _, _) => attackers.Add(a));

        Assert.Contains(player.Entity.Id, attackers);
        Assert.Contains(enemy.Entity.Id, attackers);
        Assert.Contains(poisonGranter, attackers);
    }

    [Fact]
    public void 使い切った状態変化はターン終了時に取り除かれる()
    {
        var (session, player, _) = NewBattle();
        Holder(player).AddTestEffect(Buff, remainingActions: 1);

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.Empty(Holder(player).Effects);
        Assert.Same(player, Assert.Single(result.ExpiredEffects!).Holder);
    }

    // --- ヘルパ ---

    private static EffectInstance? ResultOf(TurnResult result, BattleParticipant participant)
        => result.First.Participant == participant ? result.FirstDisabled : result.SecondDisabled;

    /// <summary>同格同士なら1発動あたり 80 ダメージになる毒を付与する。</summary>
    private static EffectInstance AddPoison(
        BattleParticipant target, Guid granterId, Guid? instanceId = null)
        => Holder(target).AddTestEffect(
            Poison,
            granterEntityId: granterId,
            grantSourceKey: $"poison_{instanceId ?? Guid.NewGuid()}",
            slipAttackType: AttackType.Physical,
            slipAttackSnapshot: new BigInteger(800),
            instanceId: instanceId);

    private static EntityBase Holder(BattleParticipant participant) => (EntityBase)participant.Entity;

    private static Skill AttackSkill() =>
        new(GameConstants.AttackSkillKey, "通常攻撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, GameConstants.PowerScale)]);

    private static Player NewPlayer(string name, int level = 100)
    {
        var p = new Player(userId: 1, name: name, exp: new BigInteger(level) * level);
        p.RestoreToFull();
        return p;
    }

    private static Enemy NewEnemy(string name, int level = 100, int speed = 500)
    {
        var e = new Enemy(
            masterKey: "test", name: name, level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: speed);
        e.RestoreToFull();
        return e;
    }

    private static BattleParticipant NewParticipant(EntityBase entity, EntityType type) =>
        new(entity, type,
            discordUserId: type == EntityType.Player ? 1UL : null,
            enemyId: type == EntityType.Enemy ? Guid.NewGuid() : null);

    private static (BattleSession Session, BattleParticipant Player, BattleParticipant Enemy) NewBattle(
        int playerLevel = 100, int enemyLevel = 100, int enemySpeed = 500)
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー", playerLevel), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵", enemyLevel, enemySpeed), EntityType.Enemy);
        session.AddParticipant(player);
        session.AddParticipant(enemy);
        return (session, player, enemy);
    }
}
