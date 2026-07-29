using System.Numerics;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Stats;
using Chido.Core.World;
using Chido.Data.Entities;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Chido.Data.World;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// マスタからの抽選・生成・永続化の検証（戦闘システム 10.3・10.5）。
///
/// <para>
/// 抽選の規則そのものは Core 側の単体テストで固定してある。ここで見るのは、
/// マスタテーブルから Core の入力へ正しく橋渡しできているかと、
/// 生成された組がチャンネルに正しく記録されるか。
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class WorldCatalogTests(DatabaseFixture fixture)
{
    private const string Grassland = GameConstants.GrasslandFieldKey;

    [DatabaseFact]
    public async Task 起動時検証は草原とそのCommonの組を要求する()
    {
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);

        WorldValidation.ThrowIfInvalid(catalog);
        Assert.Empty(WorldValidation.Validate(catalog));
    }

    [DatabaseFact]
    public async Task マスタから読んだ重みで抽選できる()
    {
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);
        var result = GroupDraw.Draw(catalog, Grassland, new Random(1));

        Assert.Equal("slime_group", result.GroupKey);
        Assert.Equal(Rarity.Common, result.Rarity);
        Assert.False(result.Degraded);
    }

    [DatabaseFact]
    public async Task 敵マスタのShapeと強さ倍率がステータスに反映される()
    {
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);

        // slime は Shape 100（等倍）・強さ倍率 1 のため、同格の基準値そのものになる
        var slime = catalog.CreateEnemy("slime", level: 100);
        Assert.Equal(GameConstants.LifeScale * 100, slime.MaxLife);
        Assert.Equal(GameConstants.AttackScale * 100, slime.PAtk);

        // bat は HP Shape 50（半分）
        var bat = catalog.CreateEnemy("bat", level: 100);
        Assert.Equal(GameConstants.LifeScale * 100 / 2, bat.MaxLife);
        Assert.Equal(Element.Sky, bat.Elements);
        Assert.Equal(ActionPatternType.Rotation, bat.ActionPatternType);
    }

    [DatabaseFact]
    public async Task 敵マスタに無いキーの生成は例外になる()
    {
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);

        Assert.Throws<InvalidOperationException>(() => catalog.CreateEnemy("unknown", 1));
    }

    [DatabaseFact]
    public async Task 生成された組がチャンネルに記録される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);
        await BattleLockTests.SeedAsync(db, ids);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);
        var enemies = new ChannelEnemyRepository(db);

        var plan = SpawnPlanner.PlanInitial(catalog, new Random(1));
        var spawned = new GroupSpawner(catalog).Spawn(plan.GroupKey, plan.CumulativeEnemyLevel, new Random(1));

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await enemies.ReplaceAsync(ids.ChannelId, spawned);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var current = await new ChannelEnemyRepository(verifyDb).LoadAsync(ids.ChannelId);

        Assert.Equal(2, current.Count);
        // spawn_index は組の member_index の恒等複製
        Assert.Equal([0, 1], current.Select(x => (int)x.SpawnIndex));

        var records = await verifyDb.BattleEnemies
            .Where(x => current.Select(c => c.EnemyId).Contains(x.EnemyId))
            .ToListAsync();

        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(
            GameConstants.InitialCumulativeEnemyLevel, r.Level));
        Assert.Contains(records, r => r.MasterKey == "slime");
        Assert.Contains(records, r => r.MasterKey == "bat");
    }

    [DatabaseFact]
    public async Task 再出現は前の組の記録を置き換える()
    {
        // 差し替えであって追加ではない。ただし chido_battle_enemy の行そのものは物理削除しない
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);
        await BattleLockTests.SeedAsync(db, ids);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);
        var enemies = new ChannelEnemyRepository(db);
        var spawner = new GroupSpawner(catalog);

        var first = spawner.Spawn("slime_group", 1, new Random(1));
        await ReplaceAsync(db, enemies, ids, first);

        var second = spawner.Spawn("slime_group", 2, new Random(2));
        await ReplaceAsync(db, enemies, ids, second);

        await using var verifyDb = await fixture.CreateContextAsync();
        var current = await new ChannelEnemyRepository(verifyDb).LoadAsync(ids.ChannelId);

        // 出現中は新しい組だけ
        Assert.Equal(2, current.Count);
        Assert.All(current, c => Assert.Contains(c.EnemyId, second.Select(s => s.Enemy.Id)));

        // 前の組の敵インスタンスは記録として残る
        var previous = await verifyDb.BattleEnemies
            .Where(x => first.Select(s => s.Enemy.Id).Contains(x.EnemyId))
            .ToListAsync();
        Assert.Equal(2, previous.Count);
    }

    [DatabaseFact]
    public async Task 抽選された装備が部位ごとに記録される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);
        await BattleLockTests.SeedAsync(db, ids);

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);
        var enemies = new ChannelEnemyRepository(db);

        // slime は equip_rate = 10000 の武器を1つ持つ
        var spawned = new GroupSpawner(catalog).Spawn("slime_group", 100, new Random(1));
        var slime = spawned.Single(s => s.Enemy.MasterKey == "slime");

        Assert.Equal(EquipPart.Weapon, Assert.Single(slime.Equipment).Part);
        // 装備の補正が実際にステータスへ乗っている
        Assert.True(slime.Enemy.PAtk > catalog.CreateEnemy("slime", 100).PAtk);

        await ReplaceAsync(db, enemies, ids, spawned);

        await using var verifyDb = await fixture.CreateContextAsync();

        var slot = await verifyDb.BattleEnemyEquipmentSlots
            .FirstAsync(x => x.EnemyId == slime.Enemy.Id);
        Assert.NotNull(slot.WeaponInstanceId);
        Assert.Null(slot.HeadInstanceId);

        var equipment = await verifyDb.BattleEnemyEquipments
            .FirstAsync(x => x.InstanceId == slot.WeaponInstanceId);
        Assert.Equal("rusty_sword", equipment.EquipKey);
    }

    [DatabaseFact]
    public async Task 縮退した抽選は通知の対象として報告される()
    {
        // 洞窟には Mythic の重みしか無く組が0件。草原の Common へ落ちる
        await using var db = await fixture.CreateContextAsync();
        await SeedMastersAsync(db);

        // 実DBテストはコレクション内で同じデータベースを共有するため、投入は冪等にしておく
        if (!await db.FieldMasters.AnyAsync(x => x.FieldKey == "cave"))
        {
            db.FieldMasters.Add(new FieldMasterRecord { FieldKey = "cave", Name = "洞窟" });
            db.FieldRarityRateMasters.Add(new FieldRarityRateMasterRecord
            {
                FieldKey = "cave", Rarity = Rarity.Mythic, RarityRate = Ratio.Full,
            });
            await db.SaveChangesAsync();
        }

        var catalog = await DatabaseWorldCatalog.LoadAsync(db);
        var result = GroupDraw.Draw(catalog, "cave", new Random(1));

        Assert.True(result.Degraded);
        Assert.Equal(Rarity.Common, result.Rarity);
        Assert.Equal("slime_group", result.GroupKey);
    }

    // --- ヘルパ ---

    private static async Task ReplaceAsync(
        ChidoDbContext db, ChannelEnemyRepository enemies,
        BattleLockTests.Ids ids, IReadOnlyList<SpawnedEnemy> spawned)
    {
        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockChannelAsync(ids.ChannelId);
        await enemies.ReplaceAsync(ids.ChannelId, spawned);
        await scope.CommitAsync();
    }

    /// <summary>
    /// 検証に必要な最小のマスタ。既に投入済みなら何もしない
    /// （実DBテストはコレクション内で直列に走り、同じデータベースを共有するため）。
    /// </summary>
    private static async Task SeedMastersAsync(ChidoDbContext db)
    {
        if (await db.FieldMasters.AnyAsync(x => x.FieldKey == Grassland)) return;

        db.FieldMasters.Add(new FieldMasterRecord { FieldKey = Grassland, Name = "草原" });

        db.FieldRarityRateMasters.Add(new FieldRarityRateMasterRecord
        {
            FieldKey = Grassland, Rarity = Rarity.Common, RarityRate = Ratio.Full,
        });

        db.FieldTransitionMasters.Add(new FieldTransitionMasterRecord
        {
            // 自己ループ。「そこから動かない」がデータ上の意図として明示され、縮退経路と区別できる
            FieldKey = Grassland, NextFieldKey = Grassland,
        });

        db.EnemyGroupMasters.Add(new EnemyGroupMasterRecord
        {
            GroupKey = "slime_group", Rarity = Rarity.Common,
        });

        db.FieldEnemyGroupMasters.Add(new FieldEnemyGroupMasterRecord
        {
            FieldKey = Grassland, GroupKey = "slime_group", Rarity = Rarity.Common,
        });

        db.EnemyGroupMemberMasters.AddRange(
            new EnemyGroupMemberMasterRecord { GroupKey = "slime_group", MemberIndex = 0, EnemyKey = "slime" },
            new EnemyGroupMemberMasterRecord { GroupKey = "slime_group", MemberIndex = 1, EnemyKey = "bat" });

        db.EnemyMasters.AddRange(
            NewEnemyMaster("slime", "スライム"),
            NewEnemyMaster("bat", "コウモリ",
                hpShape: 50, elements: Element.Sky, pattern: ActionPatternType.Rotation));

        db.EquipmentMasters.Add(new EquipmentMasterRecord
        {
            EquipKey = "rusty_sword",
            Name = "錆びた剣",
            EquipParts = EquipPart.Weapon,
            Rarity = Rarity.Common,
            Elements = Element.None,
            ProgressionValue = 10,
            HpRate = Ratio.Zero,
            PAtkRate = Ratio.Full,
            PDefRate = Ratio.Zero,
            MAtkRate = Ratio.Zero,
            MDefRate = Ratio.Zero,
            SpeedBonus = 0,
            LuckBonusRate = Ratio.Zero,
        });

        db.EnemyEquipmentMasters.Add(new EnemyEquipmentMasterRecord
        {
            EnemyKey = "slime",
            EnemyEquipmentIndex = 0,
            EquipKey = "rusty_sword",
            EquipRate = Ratio.Full,
            DropRate = Ratio.FromPercent(10m),
        });

        await db.SaveChangesAsync();
    }

    private static EnemyMasterRecord NewEnemyMaster(
        string key, string name,
        ushort hpShape = 100, Element elements = Element.None,
        ActionPatternType pattern = ActionPatternType.PureRandom)
        => new()
        {
            EnemyKey = key,
            Name = name,
            ImageUrl = null,
            Rarity = Rarity.Common,
            Elements = elements,
            HpShape = hpShape,
            PAtkShape = 100,
            PDefShape = 100,
            MAtkShape = 100,
            MDefShape = 100,
            StrengthRate = Ratio.Full,
            ExpRate = Ratio.Full,
            Speed = 500,
            InitialTp = 0,
            ActionPatternType = pattern,
            AllyTargetRule = AllyTargetRule.PureRandom,
        };
}
