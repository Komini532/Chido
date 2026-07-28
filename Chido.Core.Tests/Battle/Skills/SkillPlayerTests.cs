using System.Numerics;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle.Skills;

/// <summary>
/// モーション再生の5ステップの検証（戦闘システム 4.2）。
///
/// ステップ2〜4は独立に働き、いずれか1つでも該当すればそのモーションはスキップされる。
/// ステップ1のみが「以降の全モーションの打ち切り」であり、他と性質が異なる。
/// </summary>
public class SkillPlayerTests
{
    /// <summary>命中率100%。命中判定を検証対象から外したいモーションで使う。</summary>
    private static readonly Ratio Always = Ratio.Full;

    /// <summary>命中率0%。必ず外す。</summary>
    private static readonly Ratio Never = Ratio.Zero;

    private static Player NewPlayer(string name, int level = 100) =>
        Prepare(new Player(userId: 1, name: name, exp: new BigInteger(level) * level));

    private static Enemy NewEnemy(string name, int level = 100) =>
        Prepare(new Enemy(
            masterKey: "test", name: name, level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500));

    private static T Prepare<T>(T entity) where T : EntityBase
    {
        entity.RestoreToFull();
        return entity;
    }

    /// <summary>プレイヤー1人・敵1体の最小構成。</summary>
    private static (BattleSession Session, BattleParticipant Player, BattleParticipant Enemy) NewBattle(
        int playerLevel = 100, int enemyLevel = 100)
    {
        var session = new BattleSession(guildId: 1, channelId: 1);

        var player = new BattleParticipant(
            NewPlayer("プレイヤー", playerLevel), EntityType.Player, discordUserId: 1, displayOrder: 0);
        var enemy = new BattleParticipant(
            NewEnemy("敵", enemyLevel), EntityType.Enemy, enemyId: Guid.NewGuid(), displayOrder: 0);

        session.AddParticipant(player);
        session.AddParticipant(enemy);
        return (session, player, enemy);
    }

    private static AttackMotion Attack(byte index = 0, int power = GameConstants.PowerScale,
        Ratio? accuracy = null, ushort? gate = null, TargetRule rule = TargetRule.Enemy) =>
        new(index, rule, accuracy ?? Always, AttackType.Physical, power, AccuracyGateGroup: gate);

    private static Skill SkillOf(params SkillMotion[] motions) => new("test_skill", "テスト", motions);

    private static readonly Random Deterministic = new(12345);

    // --- ステップ1: 行動者のショートサーキット ---

    [Fact]
    public void 離脱に成功すると以降のモーションが打ち切られる()
    {
        // 「戦闘離脱 → 追加攻撃」なら追加攻撃は発動しない
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            new FleeMotion(0, TargetRule.Myself, Always),
            Attack(1));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Applied, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.ShortCircuited, result.Outcomes[1]);
        Assert.Equal(ParticipantStatus.Escaped, player.Status);
        Assert.Equal(enemy.Entity.MaxLife, enemy.Entity.CurrentLife);
    }

    [Fact]
    public void 離脱の前のモーションは通常通り再生される()
    {
        // 「バフ付与 → 戦闘離脱」ならバフは発動した上で離脱する
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0),
            new FleeMotion(1, TargetRule.Myself, Always));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Applied, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);
        Assert.True(enemy.Entity.CurrentLife < enemy.Entity.MaxLife);
        Assert.Equal(ParticipantStatus.Escaped, player.Status);
    }

    [Fact]
    public void 離脱に失敗しても以降のモーションは再生される()
    {
        // 「逃げようとして失敗したので殴る」が成立する。
        // ショートサーキットの根拠はモーションの種別ではなく「行動者が Active か」であるため
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            new FleeMotion(0, TargetRule.Myself, Never),
            Attack(1));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Missed, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);
        Assert.Equal(ParticipantStatus.Active, player.Status);
        Assert.True(enemy.Entity.CurrentLife < enemy.Entity.MaxLife);
    }

    [Fact]
    public void 敵を追い払っても行動者自身のモーションは再生される()
    {
        // 打ち切られるのは敵対象モーションのステップ3だけであり、行動者は Active のまま
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            new FleeMotion(0, TargetRule.Enemy, Always),
            new HealMotion(1, TargetRule.Myself, Always, AttackType.Physical, GameConstants.PowerScale),
            Attack(2));

        player.Entity.TakeDamage(100);
        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(ParticipantStatus.Escaped, enemy.Status);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);            // 自分自身対象は再生される
        Assert.Equal(MotionOutcome.SkippedByTargetStatus, result.Outcomes[2]); // 敵対象はスキップ
        Assert.Equal(ParticipantStatus.Active, player.Status);
    }

    // --- ステップ2: accuracy_gate_group ---

    [Fact]
    public void ゲートの先頭が効果適用に到達すればメンバーは独立に抽選される()
    {
        // 「攻撃命中 → 毒30% ＆ 麻痺20% をそれぞれ独立判定」。
        // 先頭が命中してもメンバーが自動成功するわけではない
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0, gate: 1),
            Attack(1, gate: 1, accuracy: Always),
            Attack(2, gate: 1, accuracy: Never));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Applied, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);
        Assert.Equal(MotionOutcome.Missed, result.Outcomes[2]);
    }

    [Fact]
    public void ゲートの先頭が命中を外すとメンバーは抽選せずスキップされる()
    {
        // 外した攻撃に毒を乗せない
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0, gate: 1, accuracy: Never),
            Attack(1, gate: 1, accuracy: Always),
            Attack(2, gate: 1, accuracy: Always));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Missed, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.SkippedByGate, result.Outcomes[1]);
        Assert.Equal(MotionOutcome.SkippedByGate, result.Outcomes[2]);
    }

    [Fact]
    public void ゲートの依存先は常に先頭でありメンバー同士は連鎖しない()
    {
        // メンバー1が外れてもメンバー2は先頭だけを見るため再生される。
        // ショートサーキット方式だと1攻撃から複数の状態変化を付与できなくなる
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0, gate: 1, accuracy: Always),
            Attack(1, gate: 1, accuracy: Never),
            Attack(2, gate: 1, accuracy: Always));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Applied, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.Missed, result.Outcomes[1]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[2]);
    }

    [Fact]
    public void ゲートの先頭はmotion_index最小の行である()
    {
        var skill = SkillOf(Attack(5, gate: 1), Attack(2, gate: 1), Attack(9, gate: 1));

        Assert.Equal((byte)2, skill.GateLeaderOf(1)!.MotionIndex);
    }

    [Fact]
    public void 別グループのゲートは互いに影響しない()
    {
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0, gate: 1, accuracy: Never),
            Attack(1, gate: 1, accuracy: Always),
            Attack(2, gate: 2, accuracy: Always),
            Attack(3, gate: 2, accuracy: Always));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.SkippedByGate, result.Outcomes[1]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[3]);
    }

    [Fact]
    public void ゲート無指定のモーションは単独で判定される()
    {
        var (_, player, enemy) = NewBattle();
        var skill = SkillOf(
            Attack(0, accuracy: Never),
            Attack(1, accuracy: Always));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(MotionOutcome.Missed, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);
    }

    // --- ステップ3: 対象状態 ---

    [Fact]
    public void 先攻が対象を倒すと以降の敵対象モーションはスキップされる()
    {
        // 対象の生存判定であり、行動者の生存判定とは独立
        var (_, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1);
        var skill = SkillOf(Attack(0), Attack(1));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(ParticipantStatus.Defeated, enemy.Status);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[0]);
        Assert.Equal(MotionOutcome.SkippedByTargetStatus, result.Outcomes[1]);
    }

    [Fact]
    public void 対象を倒した後も自分自身対象のモーションは再生される()
    {
        var (_, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1);
        player.Entity.TakeDamage(1000);
        var wounded = player.Entity.CurrentLife;

        var skill = SkillOf(
            Attack(0),
            new HealMotion(1, TargetRule.Myself, Always, AttackType.Physical, GameConstants.PowerScale));

        var result = new SkillPlayer().Play(player, skill, enemy, Deterministic);

        Assert.Equal(ParticipantStatus.Defeated, enemy.Status);
        Assert.Equal(MotionOutcome.Applied, result.Outcomes[1]);
        Assert.True(player.Entity.CurrentLife > wounded);
    }

    // --- ステップ4: 命中判定 ---

    [Fact]
    public void 命中を外したモーションはダメージを与えない()
    {
        // 外れたモーションはパイプラインに入らないため、最低ダメージ1の保証にも到達しない
        var (_, player, enemy) = NewBattle();
        var full = enemy.Entity.CurrentLife;

        var result = new SkillPlayer().Play(player, SkillOf(Attack(0, accuracy: Never)), enemy, Deterministic);

        Assert.Equal(MotionOutcome.Missed, result.Outcomes[0]);
        Assert.Equal(full, enemy.Entity.CurrentLife);
    }

    // --- 再生順 ---

    [Fact]
    public void モーションはmotion_index昇順に再生される()
    {
        var skill = SkillOf(Attack(2), Attack(0), Attack(1));

        Assert.Equal([(byte)0, (byte)1, (byte)2], skill.Motions.Select(m => m.MotionIndex));
    }

    // --- 与ダメージの通知 ---

    [Fact]
    public void 実効ダメージが台帳の接続点へ通知される()
    {
        var (_, player, enemy) = NewBattle();
        var total = BigInteger.Zero;

        new SkillPlayer().Play(
            player, SkillOf(Attack(0), Attack(1)), enemy, Deterministic,
            onDamageDealt: (_, _, damage) => total += damage);

        Assert.Equal(enemy.Entity.MaxLife - enemy.Entity.CurrentLife, total);
    }

    [Fact]
    public void オーバーキル分は台帳に通知されない()
    {
        var (_, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1);
        var total = BigInteger.Zero;

        new SkillPlayer().Play(
            player, SkillOf(Attack(0)), enemy, Deterministic,
            onDamageDealt: (_, target, damage) =>
            {
                total += damage;
                Assert.Equal(BigInteger.Zero, target.Entity.CurrentLife);
            });

        Assert.True(total > BigInteger.Zero);
    }
}
