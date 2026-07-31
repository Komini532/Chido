using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Chido.Targeting;
using Xunit;

namespace Chido.Tests.Targeting;

/// <summary>
/// <c>[対象]</c> の解決規則の検証（戦闘システム 9.2・B-12）。
/// </summary>
public class TargetResolutionTests
{
    [Fact]
    public void 未指定は指定なしとして扱われる()
    {
        var candidates = new[] { NewEnemy("スライム", 0) };

        Assert.Equal(TargetResolutionStatus.NotSpecified,
            TargetResolution.Resolve(null, candidates).Status);
        Assert.Equal(TargetResolutionStatus.NotSpecified,
            TargetResolution.Resolve("   ", candidates).Status);
    }

    [Fact]
    public void オートコンプリートのentity_idは一意に解決される()
    {
        // 候補から選んだ場合はGuidがそのまま届くため、同名の敵がいても一意に決まる
        var first = NewEnemy("スライム", 0);
        var second = NewEnemy("スライム", 1);

        var result = TargetResolution.Resolve(second.Entity.Id.ToString(), [first, second]);

        Assert.Equal(TargetResolutionStatus.Resolved, result.Status);
        Assert.Same(second, result.Participant);
    }

    [Fact]
    public void 候補に無いentity_idは解決不能になる()
    {
        var result = TargetResolution.Resolve(
            Guid.NewGuid().ToString(), [NewEnemy("スライム", 0)]);

        Assert.Equal(TargetResolutionStatus.Unresolved, result.Status);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void 表示名の完全一致で解決される()
    {
        var slime = NewEnemy("スライム", 0);
        var bat = NewEnemy("コウモリ", 1);

        var result = TargetResolution.Resolve("コウモリ", [slime, bat]);

        Assert.Same(bat, result.Participant);
    }

    [Fact]
    public void 完全一致は大文字小文字を区別しない()
    {
        var goblin = NewEnemy("Goblin", 0);

        Assert.Same(goblin, TargetResolution.Resolve("goblin", [goblin]).Participant);
    }

    [Fact]
    public void 前方一致で解決される()
    {
        var slime = NewEnemy("スライムキング", 0);
        var bat = NewEnemy("コウモリ", 1);

        Assert.Same(slime, TargetResolution.Resolve("スライム", [slime, bat]).Participant);
    }

    [Fact]
    public void 完全一致は前方一致より優先される()
    {
        var exact = NewEnemy("スライム", 0);
        var longer = NewEnemy("スライムキング", 1);

        Assert.Same(exact, TargetResolution.Resolve("スライム", [exact, longer]).Participant);
    }

    [Fact]
    public void 同名が複数あれば解決不能として通知する()
    {
        // 曖昧なまま片方を選ぶと、意図と違う相手を攻撃したことに後から気づく形になる
        var first = NewEnemy("スライム", 0);
        var second = NewEnemy("スライム", 1);

        var result = TargetResolution.Resolve("スライム", [first, second]);

        Assert.Equal(TargetResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.Participant);
        Assert.Contains("複数", result.Message);
    }

    [Fact]
    public void 前方一致が複数あれば解決不能になる()
    {
        var a = NewEnemy("スライムA", 0);
        var b = NewEnemy("スライムB", 1);

        Assert.Equal(TargetResolutionStatus.Ambiguous,
            TargetResolution.Resolve("スライム", [a, b]).Status);
    }

    [Fact]
    public void 一致しない文字列は解決不能になる()
    {
        var result = TargetResolution.Resolve("ドラゴン", [NewEnemy("スライム", 0)]);

        Assert.Equal(TargetResolutionStatus.Unresolved, result.Status);
        Assert.Contains("見つかりません", result.Message);
    }

    [Fact]
    public void 解決できた場合と未指定の場合は通知しない()
    {
        var slime = NewEnemy("スライム", 0);

        Assert.Null(TargetResolution.Resolve("スライム", [slime]).Message);
        Assert.Null(TargetResolution.Resolve(null, [slime]).Message);
    }

    [Fact]
    public void 前後の空白は無視される()
    {
        var slime = NewEnemy("スライム", 0);

        Assert.Same(slime, TargetResolution.Resolve("  スライム  ", [slime]).Participant);
    }

    // --- オートコンプリートのラベル ---

    [Fact]
    public void 同名がいなければ表示名だけを出す()
    {
        var slime = NewEnemy("スライム", 0);
        var bat = NewEnemy("コウモリ", 1);

        Assert.Equal("スライム", TargetResolution.LabelOf(slime, [slime, bat]));
    }

    [Fact]
    public void 同名がいれば表示順を添えて区別する()
    {
        // 組に同じ種族が複数いるのは通常の構成であり、名前だけでは選択肢が見分けられない
        var first = NewEnemy("スライム", 0);
        var second = NewEnemy("スライム", 1);

        Assert.Equal("スライム #1", TargetResolution.LabelOf(first, [first, second]));
        Assert.Equal("スライム #2", TargetResolution.LabelOf(second, [first, second]));
    }

    [Fact]
    public void プレイヤーも同じ規則で解決される()
    {
        // [対象] は敵・味方の双方を受理する単一の引数であり、役割は entity_type で決まる
        var player = NewPlayer("プレイヤー");
        var enemy = NewEnemy("スライム", 0);

        Assert.Same(player, TargetResolution.Resolve("プレイヤー", [player, enemy]).Participant);
    }

    private static BattleParticipant NewEnemy(string name, ushort displayOrder)
    {
        var enemy = new Enemy(
            masterKey: "test", name: name, level: 100, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500);
        enemy.RestoreToFull();

        return new BattleParticipant(
            enemy, EntityType.Enemy, enemyId: Guid.NewGuid(), displayOrder: displayOrder);
    }

    private static BattleParticipant NewPlayer(string name)
    {
        var player = new Player(userId: 1, name: name, exp: new BigInteger(10000));
        player.RestoreToFull();

        return new BattleParticipant(player, EntityType.Player, discordUserId: 1);
    }
}
