using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Entities;
using Chido.Core.Progression;
using Chido.Core.Rewards;
using Chido.Core.Stats;
using Chido.Data.Entities;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 報酬の適用と、その入力となるマスタの読み出しの検証（戦闘システム 6.2・10.2）。
///
/// <para>按分の数式そのものは Core 側の単体テストで固定してある。</para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RewardRepositoryTests(DatabaseFixture fixture)
{
    [DatabaseFact]
    public async Task 経験値と通貨とアイテムが所持に反映される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var rewards = new RewardRepository(db);
        var reward = new PlayerReward(
            ids.UserId, Exp: 500, Currency: 1234,
            Items: [new ItemDrop("herb", 3)],
            Equipment: ["rusty_sword"]);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockPlayerAsync(ids.UserId);
            await rewards.ApplyAsync([reward]);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var players = new PlayerRepository(verifyDb);

        // 経験値の初期値は 1
        Assert.Equal(501, await players.GetExpAsync(ids.UserId));
        Assert.Equal(1234, await players.GetCurrencyAsync(ids.UserId));

        var item = await verifyDb.PlayerItems.FirstAsync(x => x.UserId == ids.UserId);
        Assert.Equal("herb", item.ItemKey);
        Assert.Equal(3u, item.Quantity);

        var equipment = await verifyDb.PlayerEquipments.FirstAsync(x => x.UserId == ids.UserId);
        Assert.Equal("rusty_sword", equipment.EquipKey);
    }

    [DatabaseFact]
    public async Task 同じアイテムの再取得は数量を加算する()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var rewards = new RewardRepository(db);

        for (var i = 0; i < 2; i++)
        {
            await using var scope = await BattleLock.BeginAsync(db);
            await scope.LockPlayerAsync(ids.UserId);
            await rewards.ApplyAsync([new PlayerReward(
                ids.UserId, 0, 0, [new ItemDrop("herb", 2)], [])]);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var item = await verifyDb.PlayerItems.FirstAsync(x => x.UserId == ids.UserId);

        Assert.Equal(4u, item.Quantity);
    }

    [DatabaseFact]
    public async Task 装備は複製された新しいインスタンスとして発行される()
    {
        // 敵の装備インスタンスをそのまま所有者移転すると、同一の敵から複数プレイヤーが
        // 同じ装備を受け取る場合に破綻する
        var ids = BattleLockTests.NewIds();
        var second = ids.UserId + 1000;

        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        await new PlayerRepository(db).EnsureAsync(second, "P2");

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await new RewardRepository(db).ApplyAsync(
            [
                new PlayerReward(ids.UserId, 0, 0, [], ["rusty_sword"]),
                new PlayerReward(second, 0, 0, [], ["rusty_sword"]),
            ]);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var instances = await verifyDb.PlayerEquipments
            .Where(x => x.UserId == ids.UserId || x.UserId == second)
            .ToListAsync();

        Assert.Equal(2, instances.Count);
        Assert.Equal(2, instances.Select(x => x.InstanceId).Distinct().Count());
    }

    // --- マスタからの入力組み立て ---

    [DatabaseFact]
    public async Task 基礎経験値と通貨は撃破した敵ごとに合算される()
    {
        // 組の基礎経験値はメンバーの合算。同一種族が複数体いればその数だけ加算される
        await using var db = await fixture.CreateContextAsync();
        var enemyKey = await SeedEnemyAsync(db, expRatePercent: 100, dropAmount: 50);

        var enemyIds = await SpawnAsync(db, enemyKey, count: 3, level: 10);

        var context = await new RewardRepository(db).BuildContextAsync(
            players: [new PlayerContribution(1, 100, Ratio.Zero, false)],
            enemyLevel: 10,
            spawnMaxLifeSum: 100,
            defeatedEnemyIds: enemyIds);

        Assert.Equal(Ratio.Full.Permyriad * 3, context.ExpRateSum);
        Assert.Equal(150, context.CurrencyDropTotal);
    }

    [DatabaseFact]
    public async Task ドロップ候補は敵の数だけ判定機会を持つ()
    {
        await using var db = await fixture.CreateContextAsync();
        var enemyKey = await SeedEnemyAsync(db, expRatePercent: 100, dropAmount: 0);

        db.EnemyLootsMasters.Add(new EnemyLootsMasterRecord
        {
            EnemyKey = enemyKey, ItemKey = "herb", Quantity = 1, DropRate = Ratio.Full,
        });
        await db.SaveChangesAsync();

        var enemyIds = await SpawnAsync(db, enemyKey, count: 2, level: 10);

        var context = await new RewardRepository(db).BuildContextAsync(
            [new PlayerContribution(1, 100, Ratio.Zero, false)], 10, 100, enemyIds);

        Assert.Equal(2, context.Loots.Count);
    }

    [DatabaseFact]
    public async Task 装備のドロップ候補は出現時に装着していたものだけになる()
    {
        // 装備マスタの全候補ではない。出現時に身につけていなかった装備は落とさない
        await using var db = await fixture.CreateContextAsync();
        var enemyKey = await SeedEnemyAsync(db, expRatePercent: 100, dropAmount: 0);

        db.EnemyEquipmentMasters.AddRange(
            new EnemyEquipmentMasterRecord
            {
                EnemyKey = enemyKey, EnemyEquipmentIndex = 0, EquipKey = "worn_blade",
                EquipRate = Ratio.Full, DropRate = Ratio.FromPercent(25m),
            },
            new EnemyEquipmentMasterRecord
            {
                EnemyKey = enemyKey, EnemyEquipmentIndex = 1, EquipKey = "unworn_shield",
                EquipRate = Ratio.Zero, DropRate = Ratio.Full,
            });
        await db.SaveChangesAsync();

        var enemyIds = await SpawnAsync(db, enemyKey, count: 1, level: 10);

        // 出現時に装着していたのは worn_blade だけ
        db.BattleEnemyEquipments.Add(new BattleEnemyEquipmentRecord
        {
            InstanceId = Guid.NewGuid(), EnemyId = enemyIds[0], EquipKey = "worn_blade",
        });
        await db.SaveChangesAsync();

        var context = await new RewardRepository(db).BuildContextAsync(
            [new PlayerContribution(1, 100, Ratio.Zero, false)], 10, 100, enemyIds);

        var drop = Assert.Single(context.EquipmentDrops);
        Assert.Equal("worn_blade", drop.EquipKey);
        // ドロップ率は敵マスタ側が持つ
        Assert.Equal(Ratio.FromPercent(25m), drop.DropRate);
    }

    // --- 称号 ---

    [DatabaseFact]
    public async Task 称号は報酬適用後の状態で判定される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        await SeedTitlesAsync(db);

        var rewards = new RewardRepository(db);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockPlayerAsync(ids.UserId);
            await rewards.ApplyAsync(
                [new PlayerReward(ids.UserId, 0, Currency: 1000, [new ItemDrop("herb", 1)], [])]);

            var earned = await rewards.GrantTitlesAsync(
                ids.UserId, level: 100, defeatedEnemyKeys: new HashSet<string> { "slime" });

            // 撃破で得た通貨・アイテムがそのターンの条件を満たす
            Assert.Contains("collector", earned);
            Assert.Contains("rich", earned);
            Assert.Contains("slayer", earned);
            Assert.Contains("veteran", earned);

            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var owned = await verifyDb.PlayerTitles.Where(x => x.UserId == ids.UserId).ToListAsync();

        Assert.Equal(4, owned.Count);
    }

    [DatabaseFact]
    public async Task 獲得済みの称号は重複して付与されない()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        await SeedTitlesAsync(db);

        var rewards = new RewardRepository(db);

        for (var i = 0; i < 2; i++)
        {
            await using var scope = await BattleLock.BeginAsync(db);
            await scope.LockPlayerAsync(ids.UserId);
            await rewards.GrantTitlesAsync(ids.UserId, 100, new HashSet<string> { "slime" });
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var owned = await verifyDb.PlayerTitles.Where(x => x.UserId == ids.UserId).ToListAsync();

        // 2周目は veteran / slayer が既得のため増えない
        Assert.Equal(2, owned.Count);
    }

    // --- ヘルパ ---

    private static async Task<string> SeedEnemyAsync(
        ChidoDbContext db, int expRatePercent, int dropAmount)
    {
        var enemyKey = $"e{Guid.NewGuid():N}"[..20];

        db.EnemyMasters.Add(new EnemyMasterRecord
        {
            EnemyKey = enemyKey,
            Name = enemyKey,
            ImageUrl = null,
            Rarity = Rarity.Common,
            Elements = Chido.Core.Battle.Damage.Element.None,
            HpShape = 100, PAtkShape = 100, PDefShape = 100, MAtkShape = 100, MDefShape = 100,
            StrengthRate = Ratio.Full,
            ExpRate = Ratio.FromPercent(expRatePercent),
            Speed = 500,
            InitialTp = 0,
            ActionPatternType = Chido.Core.Entities.Enemies.ActionPatternType.PureRandom,
            AllyTargetRule = Chido.Core.Entities.Enemies.AllyTargetRule.PureRandom,
        });

        db.EnemyCurrencyMasters.Add(new EnemyCurrencyMasterRecord
        {
            EnemyKey = enemyKey, DropAmount = dropAmount,
        });

        await db.SaveChangesAsync();
        return enemyKey;
    }

    private static async Task<List<Guid>> SpawnAsync(
        ChidoDbContext db, string enemyKey, int count, BigInteger level)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var enemyId = Guid.NewGuid();
            db.BattleEnemies.Add(new BattleEnemyRecord
            {
                EnemyId = enemyId, MasterKey = enemyKey, Level = level,
            });
            ids.Add(enemyId);
        }

        await db.SaveChangesAsync();
        return ids;
    }

    /// <summary>実DBテストはデータベースを共有するため、投入は冪等にしておく。</summary>
    private static async Task SeedTitlesAsync(ChidoDbContext db)
    {
        if (await db.TitleMasters.AnyAsync(x => x.TitleKey == "collector")) return;

        db.TitleMasters.AddRange(
            NewTitle("collector", TitleAcquisitionType.ItemObtained, conditionKey: "herb"),
            NewTitle("slayer", TitleAcquisitionType.EnemyDefeated, conditionKey: "slime"),
            NewTitle("veteran", TitleAcquisitionType.LevelReached, conditionValue: 100),
            NewTitle("rich", TitleAcquisitionType.CurrencyReached, conditionValue: 1000));

        await db.SaveChangesAsync();
    }

    private static TitleMasterRecord NewTitle(
        string key, TitleAcquisitionType type,
        string? conditionKey = null, BigInteger? conditionValue = null)
        => new()
        {
            TitleKey = key,
            Name = key,
            Emoji = "⭐",
            AcquisitionType = type,
            ConditionKey = conditionKey,
            ConditionValue = conditionValue,
        };
}
