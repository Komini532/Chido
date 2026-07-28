using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle.Skills;

/// <summary>
/// 敵の味方対象モーションの対象解決の検証（ally_target_rule。戦闘システム 4.2）。
/// </summary>
public class AllyTargetResolverTests
{
    [Fact]
    public void 完全ランダムは自分を含む候補から選ぶ()
    {
        // 「味方」が自分を含むため、値0・24 では候補が空にならない
        var (session, actor, others) = NewGroup(AllyTargetRule.PureRandom, memberCount: 3);

        var picked = Enumerable.Range(0, 60)
            .Select(seed => new AllyTargetResolver(session, new Random(seed)).Resolve(actor))
            .Distinct()
            .ToList();

        Assert.Equal(3, picked.Count);
        Assert.Contains(actor, picked);
        Assert.All(others, o => Assert.Contains(o, picked));
    }

    [Fact]
    public void 単独なら自分が選ばれる()
    {
        var (session, actor, _) = NewGroup(AllyTargetRule.PureRandom, memberCount: 1);

        Assert.Same(actor, new AllyTargetResolver(session, new Random(1)).Resolve(actor));
    }

    [Fact]
    public void 自分以外からランダムは自分を選ばない()
    {
        var (session, actor, others) = NewGroup(AllyTargetRule.RandomExceptSelf, memberCount: 3);

        var picked = Enumerable.Range(0, 60)
            .Select(seed => new AllyTargetResolver(session, new Random(seed)).Resolve(actor))
            .Distinct()
            .ToList();

        Assert.Equal(2, picked.Count);
        Assert.DoesNotContain(actor, picked);
        Assert.All(others, o => Assert.Contains(o, picked));
    }

    [Fact]
    public void 自分以外からランダムは単独なら自分へフォールバックする()
    {
        // 明示的なフォールバックが要るのは、候補を意図的に狭める本値（および将来の固定対象系）のみ
        var (session, actor, _) = NewGroup(AllyTargetRule.RandomExceptSelf, memberCount: 1);

        Assert.Same(actor, new AllyTargetResolver(session, new Random(1)).Resolve(actor));
    }

    // --- 最小HP割合（値24） ---

    [Fact]
    public void 最小HP割合は割合が最小の1体を選ぶ()
    {
        var (session, actor, others) = NewGroup(AllyTargetRule.LowestLifeRatio, memberCount: 3);

        // actor は無傷、others[0] は半分、others[1] は 1/4
        others[0].Entity.TakeDamage(others[0].Entity.MaxLife / 2);
        others[1].Entity.TakeDamage(others[1].Entity.MaxLife * 3 / 4);

        var resolver = new AllyTargetResolver(session, new Random(1));

        Assert.Same(others[1], resolver.Resolve(actor));
    }

    [Fact]
    public void 最小HP割合は最大HPが異なっても割合で比較する()
    {
        // 実数の残量ではなく割合。除算せず交差乗算で比較するため丸めで順位がずれない
        var session = new BattleSession(guildId: 1, channelId: 1);
        // 最大HP は tough 3600・frail 1200
        var tough = NewParticipant(NewEnemy(AllyTargetRule.LowestLifeRatio, level: 300), 0);
        var frail = NewParticipant(NewEnemy(AllyTargetRule.LowestLifeRatio, level: 100), 1);
        session.AddParticipant(tough);
        session.AddParticipant(frail);

        // 残量は tough(1800) のほうが多いが、割合は tough 50% < frail 100%
        tough.Entity.TakeDamage(tough.Entity.MaxLife / 2);

        Assert.True(tough.Entity.CurrentLife > frail.Entity.CurrentLife);
        Assert.Same(tough, new AllyTargetResolver(session, new Random(1)).Resolve(frail));
    }

    [Fact]
    public void 最小HP割合はオーバーヒールを自然に扱う()
    {
        // 割合が1を超える参加者があっても、交差乗算の比較はそのまま成立する
        var (session, actor, others) = NewGroup(AllyTargetRule.LowestLifeRatio, memberCount: 2);
        actor.Entity.Heal(actor.Entity.MaxLife);

        Assert.Same(others[0], new AllyTargetResolver(session, new Random(1)).Resolve(actor));
    }

    [Fact]
    public void 最小HP割合は同値なら候補からランダムに選ぶ()
    {
        var (session, actor, others) = NewGroup(AllyTargetRule.LowestLifeRatio, memberCount: 3);

        // 全員無傷＝全員100%で同順位
        var picked = Enumerable.Range(0, 60)
            .Select(seed => new AllyTargetResolver(session, new Random(seed)).Resolve(actor))
            .Distinct()
            .ToList();

        Assert.Equal(3, picked.Count);
    }

    [Fact]
    public void 最小HP割合はHP0のActiveをフェイルセーフで除外する()
    {
        // 「HP0 だが Active」が生じた場合に、割合0の瀕死者へ回復対象が固定される退化を防ぐ。
        // 正常系を状態で・異常系を値で塞ぐ二重防御
        var (session, actor, others) = NewGroup(AllyTargetRule.LowestLifeRatio, memberCount: 3);

        others[0].Entity.TakeDamage(others[0].Entity.MaxLife); // HP0 だが Active のまま
        others[1].Entity.TakeDamage(others[1].Entity.MaxLife / 2);

        Assert.Equal(BigInteger.Zero, others[0].Entity.CurrentLife);
        Assert.True(others[0].IsActive);

        Assert.Same(others[1], new AllyTargetResolver(session, new Random(1)).Resolve(actor));
    }

    // --- 候補集合 ---

    [Fact]
    public void 候補集合はActiveな自軍のみで構成される()
    {
        var (session, actor, others) = NewGroup(AllyTargetRule.PureRandom, memberCount: 3);
        others[0].MarkDefeated();
        others[1].MarkEscaped();

        // プレイヤーは別陣営であり候補にならない
        session.AddParticipant(NewPlayer());

        var picked = Enumerable.Range(0, 40)
            .Select(seed => new AllyTargetResolver(session, new Random(seed)).Resolve(actor))
            .Distinct()
            .ToList();

        Assert.Same(actor, Assert.Single(picked));
    }

    [Fact]
    public void 未実装の予約値は完全ランダムへフォールバックする()
    {
        // マスタに予約値が紛れ込んでも戦闘を止めない。判定は IsImplemented の1箇所に集約されている
        Assert.False(AllyTargetRule.HighestPAtk.IsImplemented());

        var (session, actor, _) = NewGroup(AllyTargetRule.HighestPAtk, memberCount: 3);

        var picked = Enumerable.Range(0, 60)
            .Select(seed => new AllyTargetResolver(session, new Random(seed)).Resolve(actor))
            .Distinct()
            .ToList();

        Assert.Equal(3, picked.Count);
    }

    [Fact]
    public void 味方対象モーションが規則を通して解決される()
    {
        // TargetResolver への差し込み口が実際に繋がっていることを見る
        var (session, actor, others) = NewGroup(AllyTargetRule.LowestLifeRatio, memberCount: 2);
        others[0].Entity.TakeDamage(others[0].Entity.MaxLife / 2);

        var motion = new HealMotion(0, TargetRule.Ally, Ratio.Full, AttackType.Physical, 100);
        var selector = new AllyTargetResolver(session, new Random(1)).AsSelector();

        var resolved = TargetResolver.Resolve(motion, actor, NewPlayer(), enemyAllySelector: selector);

        Assert.Same(others[0], resolved);
    }

    // --- ヘルパ ---

    private static Enemy NewEnemy(AllyTargetRule rule, int level = 100)
    {
        var e = new Enemy(
            masterKey: "test", name: $"敵{Guid.NewGuid():N}", level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500, allyTargetRule: rule);
        e.RestoreToFull();
        return e;
    }

    private static BattleParticipant NewParticipant(Enemy enemy, ushort displayOrder) =>
        new(enemy, EntityType.Enemy, enemyId: Guid.NewGuid(), displayOrder: displayOrder);

    private static BattleParticipant NewPlayer()
    {
        var p = new Player(userId: 1, name: "プレイヤー", exp: 10000);
        p.RestoreToFull();
        return new BattleParticipant(p, EntityType.Player, discordUserId: 1);
    }

    private static (BattleSession Session, BattleParticipant Actor, List<BattleParticipant> Others) NewGroup(
        AllyTargetRule rule, int memberCount)
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var members = Enumerable.Range(0, memberCount)
            .Select(i => NewParticipant(NewEnemy(rule), (ushort)i))
            .ToList();

        foreach (var member in members) session.AddParticipant(member);

        return (session, members[0], members.Skip(1).ToList());
    }
}
