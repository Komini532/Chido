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
/// 敵のスキル選択の検証（戦闘システム 4.2）。
/// </summary>
public class EnemySkillSelectorTests
{
    private static Skill Attack =>
        new(GameConstants.AttackSkillKey, "通常攻撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, GameConstants.PowerScale)]);

    private static Skill Named(string key, ushort requireTp = 0) =>
        new(key, key,
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, 120)],
            requireTp: requireTp);

    private static EnemySkillSelector Selector => new(Attack);

    // --- フォールバック ---

    [Fact]
    public void スキルを1つも保有しない敵は通常攻撃を行う()
    {
        var enemy = NewEnemy(ActionPatternType.PureRandom);

        Assert.Equal(GameConstants.AttackSkillKey, Selector.Select(enemy, new Random(1)).SkillKey);
    }

    // --- 完全ランダム ---

    [Fact]
    public void 完全ランダムは払えるスキルのみでプールを構成する()
    {
        // 「払えないスキルも抽選対象に含め、当たってから通常攻撃に落とす」方式にすると、
        // 存在するはずのないエントリが抽選される不自然さが生じる
        var enemy = NewEnemy(ActionPatternType.PureRandom,
            new EnemySkillEntry(Named("cheap")),
            new EnemySkillEntry(Named("expensive", requireTp: 500)));

        var chosen = Enumerable.Range(0, 50)
            .Select(seed => Selector.Select(enemy, new Random(seed)).SkillKey)
            .Distinct()
            .ToList();

        Assert.Equal(["cheap"], chosen);
    }

    [Fact]
    public void 完全ランダムはプールが空なら通常攻撃へフォールバックする()
    {
        var enemy = NewEnemy(ActionPatternType.PureRandom,
            new EnemySkillEntry(Named("expensive", requireTp: 500)));

        Assert.Equal(GameConstants.AttackSkillKey, Selector.Select(enemy, new Random(1)).SkillKey);
    }

    [Fact]
    public void 完全ランダムはweightを無視する()
    {
        // weight を参照するのは重み付きランダムだけ（意図的な非対称）
        var enemy = NewEnemy(ActionPatternType.PureRandom,
            new EnemySkillEntry(Named("zero_weight"), Weight: 0));

        Assert.Equal("zero_weight", Selector.Select(enemy, new Random(1)).SkillKey);
    }

    // --- 重み付きランダム ---

    [Fact]
    public void 重み付きランダムはweight0を抽選対象から外す()
    {
        var enemy = NewEnemy(ActionPatternType.WeightedRandom,
            new EnemySkillEntry(Named("excluded"), Weight: 0),
            new EnemySkillEntry(Named("included"), Weight: 1));

        var chosen = Enumerable.Range(0, 50)
            .Select(seed => Selector.Select(enemy, new Random(seed)).SkillKey)
            .Distinct()
            .ToList();

        Assert.Equal(["included"], chosen);
    }

    [Fact]
    public void 重み付きランダムは全weightが0なら通常攻撃へフォールバックする()
    {
        var enemy = NewEnemy(ActionPatternType.WeightedRandom,
            new EnemySkillEntry(Named("a"), Weight: 0),
            new EnemySkillEntry(Named("b"), Weight: 0));

        Assert.Equal(GameConstants.AttackSkillKey, Selector.Select(enemy, new Random(1)).SkillKey);
    }

    [Fact]
    public void 重み付きランダムは残存エントリのweightをそのまま用いて正規化する()
    {
        // 払えないスキルを除いた後、残った weight を「その合計で」正規化する。
        // 除外前の合計で割ると、残存分の確率が目減りして通常攻撃に化ける確率が生まれてしまう
        var enemy = NewEnemy(ActionPatternType.WeightedRandom,
            new EnemySkillEntry(Named("rare"), Weight: 1),
            new EnemySkillEntry(Named("common"), Weight: 3),
            new EnemySkillEntry(Named("unaffordable", requireTp: 500), Weight: 96));

        var counts = Enumerable.Range(0, 4000)
            .Select(seed => Selector.Select(enemy, new Random(seed)).SkillKey)
            .GroupBy(k => k)
            .ToDictionary(g => g.Key, g => g.Count());

        // 払えない96は完全に除外され、残る合計は4。通常攻撃には落ちない
        Assert.False(counts.ContainsKey(GameConstants.AttackSkillKey));
        Assert.Equal(4000, counts["rare"] + counts["common"]);

        // 1:3 に概ね一致する（決定的なシード列に対する分布）
        Assert.InRange(counts["common"] / (double)counts["rare"], 2.5, 3.5);
    }

    // --- ローテーション ---

    [Fact]
    public void ローテーションは登録順に選択され位置が前進する()
    {
        var enemy = NewEnemy(ActionPatternType.Rotation,
            new EnemySkillEntry(Named("a")),
            new EnemySkillEntry(Named("b")),
            new EnemySkillEntry(Named("c")));

        var order = Enumerable.Range(0, 7)
            .Select(_ => Selector.Select(enemy, new Random(1)).SkillKey)
            .ToList();

        Assert.Equal(["a", "b", "c", "a", "b", "c", "a"], order);
    }

    [Fact]
    public void ローテーションの位置はターン数からの導出と一致する()
    {
        // (turn - 1) % total は観測される従属式であって決定規則ではない。
        // 真実の情報源は rotation_index 列であり、複数の敵が同時に行動しても独立に進む
        var enemy = NewEnemy(ActionPatternType.Rotation,
            new EnemySkillEntry(Named("a")),
            new EnemySkillEntry(Named("b")),
            new EnemySkillEntry(Named("c")));

        for (var turn = 1; turn <= 10; turn++)
        {
            Assert.Equal((turn - 1) % 3, enemy.RotationIndex);
            Selector.Select(enemy, new Random(1));
        }
    }

    [Fact]
    public void ローテーションは払えなくても順番を飛ばさない()
    {
        // 順序そのものに意味があるため、「飛ばして次へ」進めると順序が崩れる。
        // 選択自体はローテ順を維持し、その回の出力だけが通常攻撃に差し替わる
        var enemy = NewEnemy(ActionPatternType.Rotation,
            new EnemySkillEntry(Named("a")),
            new EnemySkillEntry(Named("expensive", requireTp: 500)),
            new EnemySkillEntry(Named("c")));

        var order = Enumerable.Range(0, 3)
            .Select(_ => Selector.Select(enemy, new Random(1)).SkillKey)
            .ToList();

        Assert.Equal(["a", GameConstants.AttackSkillKey, "c"], order);

        // 差し替えられた回もローテ枠は消費されており、位置は一周して先頭へ戻っている
        Assert.Equal(0, enemy.RotationIndex);
    }

    [Fact]
    public void 登録された通常攻撃はローテ枠を占める()
    {
        // フォールバックの通常攻撃は枠を持たない（total に数えない）が、
        // 登録された通常攻撃は1枠を占め、抽選候補にもなる
        var enemy = NewEnemy(ActionPatternType.Rotation,
            new EnemySkillEntry(Attack),
            new EnemySkillEntry(Named("b")));

        var order = Enumerable.Range(0, 4)
            .Select(_ => Selector.Select(enemy, new Random(1)).SkillKey)
            .ToList();

        Assert.Equal([GameConstants.AttackSkillKey, "b", GameConstants.AttackSkillKey, "b"], order);
    }

    [Fact]
    public void TPが貯まればローテーションの高コストスキルが撃てるようになる()
    {
        var enemy = NewEnemy(ActionPatternType.Rotation,
            new EnemySkillEntry(Named("expensive", requireTp: 500)));

        Assert.Equal(GameConstants.AttackSkillKey, Selector.Select(enemy, new Random(1)).SkillKey);

        enemy.GainTp(500);

        Assert.Equal("expensive", Selector.Select(enemy, new Random(1)).SkillKey);
    }

    // --- ヘルパ ---

    private static BattleParticipant NewEnemy(
        ActionPatternType pattern, params EnemySkillEntry[] skills)
    {
        var enemy = new Enemy(
            masterKey: "test", name: "敵", level: 100, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500,
            actionPatternType: pattern, skills: skills);
        enemy.RestoreToFull();

        return new BattleParticipant(enemy, EntityType.Enemy, enemyId: Guid.NewGuid());
    }
}
