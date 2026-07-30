using System.Numerics;
using Chido.Core;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Equipment;
using Chido.Core.Stats;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Chido.Data.Loaders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// マスタからスキル・状態変化・プレイヤー実体を組み立てる経路の検証。
///
/// <para>
/// ここで見るのは「マスタの行が Core のドメインモデルへ正しく橋渡しされるか」。
/// 戦闘ロジックそのものは Core 側の単体テストで固定してある。
/// </para>
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CatalogTests(DatabaseFixture fixture)
{
    // --- スキルカタログ ---

    [DatabaseFact]
    public async Task スキルマスタからモーション列が組み立てられる()
    {
        await using var db = await fixture.CreateContextAsync();
        var key = await SeedSkillAsync(db);

        var catalog = await SkillCatalog.LoadAsync(db);
        var skill = catalog.Get(key);

        Assert.Equal("複合スキル", skill.Name);
        Assert.Equal(5, skill.Priority);
        Assert.Equal(300, skill.RequireTp);

        // motion_index 昇順に並ぶ
        Assert.Collection(skill.Motions,
            m =>
            {
                var attack = Assert.IsType<AttackMotion>(m);
                Assert.Equal(AttackType.Physical, attack.AttackType);
                Assert.Equal(150, attack.Power);
                Assert.Equal(Element.Fire, attack.Elements);
                Assert.Equal(TargetRule.Enemy, attack.TargetRule);
            },
            m =>
            {
                var heal = Assert.IsType<HealMotion>(m);
                Assert.Equal(80, heal.Power);
                Assert.Equal(TargetRule.Ally, heal.TargetRule);
            },
            m =>
            {
                var grant = Assert.IsType<GrantEffectMotion>(m);
                Assert.Equal("catalog_poison", grant.EffectKey);
                Assert.Equal(AttackType.Physical, grant.AttackType);
                Assert.Equal<ushort?>(4, grant.DurationActions);
                // ゲートは先頭1件に依存する。攻撃が当たったときだけ毒を独立抽選する構成
                Assert.Equal<ushort?>(1, grant.AccuracyGateGroup);
            },
            m => Assert.IsType<DispelEffectMotion>(m),
            m => Assert.IsType<FleeMotion>(m));
    }

    [DatabaseFact]
    public async Task サブタイプの行が欠けたモーションは例外になる()
    {
        // 親に行があるのにサブタイプが無い状態はマスタの不整合であり、黙って短いスキルとして
        // 成立させると「なぜか効果が出ないスキル」として運用に紛れ込む
        await using var db = await fixture.CreateContextAsync();

        var key = $"broken_{Guid.NewGuid():N}"[..20];
        db.SkillMasters.Add(NewSkillMaster(key));
        db.SkillMotionMasters.Add(new SkillMotionMasterRecord
        {
            SkillKey = key, MotionIndex = 0, MotionType = MotionType.Attack,
            TargetRule = TargetRule.Enemy, AccuracyRate = Ratio.Full,
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SkillCatalog.LoadAsync(db));

        Assert.Contains("サブタイプ", error.Message);
    }

    [DatabaseFact]
    public async Task 敵の保有スキルが重み付きで読み出される()
    {
        await using var db = await fixture.CreateContextAsync();
        var skillKey = await SeedSkillAsync(db);
        var enemyKey = $"e{Guid.NewGuid():N}"[..20];

        db.EnemySkillsMasters.Add(new EnemySkillsMasterRecord
        {
            EnemyKey = enemyKey, SkillKey = skillKey, Weight = 7,
        });
        await db.SaveChangesAsync();

        var catalog = await SkillCatalog.LoadAsync(db);

        var entry = Assert.Single(catalog.SkillsOf(enemyKey));
        Assert.Equal(skillKey, entry.Skill.SkillKey);
        Assert.Equal(7, entry.Weight);
    }

    [DatabaseFact]
    public async Task マスタに無いスキルの取得は例外になる()
    {
        await using var db = await fixture.CreateContextAsync();
        var catalog = await SkillCatalog.LoadAsync(db);

        Assert.Null(catalog.Find("nonexistent"));
        Assert.Throws<InvalidOperationException>(() => catalog.Get("nonexistent"));
    }

    // --- 状態変化カタログ ---

    [DatabaseFact]
    public async Task 複数の効果種別を兼ねる状態変化が合成される()
    {
        // 1つの effect_key が複数の成分を持ちうる（マルチネイチャー）。
        // effect_types は非正規化キャッシュであり、真実の情報源は各サブテーブルの実体
        await using var db = await fixture.CreateContextAsync();
        var key = $"multi_{Guid.NewGuid():N}"[..20];

        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = key, Name = "複合効果", ClearOnBattleEnd = true,
            EffectTypes = EffectType.None, // 実体と食い違わせても、成分から導出される
        });
        db.EffectStatusModifierMasters.Add(new EffectStatusModifierMasterRecord
        {
            EffectKey = key, TargetStatus = TargetStatus.PAtk, FixedRate = Ratio.FromPercent(-30m),
        });
        db.EffectSlipDamageMasters.Add(new EffectSlipDamageMasterRecord
        {
            EffectKey = key, Power = 25, Elements = Element.Dark,
        });
        db.EffectDisableMoveMasters.Add(new EffectDisableMoveMasterRecord
        {
            EffectKey = key, DisableRate = Ratio.FromPercent(20m),
        });
        db.EffectElementGrantMasters.Add(new EffectElementGrantMasterRecord
        {
            EffectKey = key, Elements = Element.Ice,
        });
        await db.SaveChangesAsync();

        var catalog = await EffectCatalog.LoadAsync(db);
        var definition = catalog.Find(key);

        Assert.NotNull(definition);
        Assert.Equal("複合効果", definition.Name);
        Assert.Equal(Ratio.FromPercent(-30m), Assert.Single(definition.StatusModifiers).FixedRate);
        Assert.Equal(25, definition.SlipDamage!.Value.Power);
        Assert.Equal(Element.Dark, definition.SlipDamage.Value.Elements);
        Assert.Equal(Ratio.FromPercent(20m), definition.DisableRate);
        Assert.Equal(Element.Ice, definition.GrantedElements);

        // 保有効果種別は成分の実体から導出される
        Assert.Equal(
            EffectType.StatusModifier | EffectType.SlipDamage
                | EffectType.DisableMove | EffectType.ElementGrant,
            definition.EffectTypes);
    }

    [DatabaseFact]
    public async Task マスタに無い効果の表示名はキーをそのまま返す()
    {
        // 描画が落ちないようにするためのフォールバック
        await using var db = await fixture.CreateContextAsync();
        var catalog = await EffectCatalog.LoadAsync(db);

        Assert.Equal("unknown_effect", catalog.NameOf("unknown_effect"));
    }

    // --- プレイヤーの復元 ---

    [DatabaseFact]
    public async Task 経験値からレベルが導出される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        await using (var scope = await Locking.BattleLock.BeginAsync(db))
        {
            await scope.LockPlayerAsync(ids.UserId);
            // 初期値 1 に 9999 を足して 10000（level = 100）
            await new Repositories.PlayerRepository(db).AddExpAsync(ids.UserId, 9999);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var loader = new PlayerLoader(verifyDb, await EffectCatalog.LoadAsync(verifyDb));

        var player = await loader.LoadAsync(ids.UserId);

        Assert.Equal(10000, player.Exp);
        Assert.Equal(100, player.Level);
        // 同格の基準値。装備なしなら HP = 12L
        Assert.Equal(GameConstants.LifeScale * 100, player.MaxLife);
        Assert.Equal(player.MaxLife, player.CurrentLife);
    }

    [DatabaseFact]
    public async Task 装着中の装備だけがステータスに反映される()
    {
        // スロットに入っているものだけが対象であり、所持しているだけの装備は影響しない
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var equipKey = await SeedEquipmentAsync(db);

        var wornId = Guid.NewGuid();
        var storedId = Guid.NewGuid();

        db.PlayerEquipments.AddRange(
            new PlayerEquipmentRecord { InstanceId = wornId, UserId = ids.UserId, EquipKey = equipKey },
            new PlayerEquipmentRecord { InstanceId = storedId, UserId = ids.UserId, EquipKey = equipKey });
        db.PlayerEquipmentSlots.Add(new PlayerEquipmentSlotRecord
        {
            UserId = ids.UserId, WeaponInstanceId = wornId,
        });
        await db.SaveChangesAsync();

        await using var verifyDb = await fixture.CreateContextAsync();
        var loader = new PlayerLoader(verifyDb, await EffectCatalog.LoadAsync(verifyDb));

        var player = await loader.LoadAsync(ids.UserId);

        // 装着は1つだけ。2つ分の補正は乗らない
        Assert.Single(player.Equipment);
        Assert.True(player.PAtk > GameConstants.AttackScale * player.Level);
    }

    [DatabaseFact]
    public async Task 永続スコープの状態変化が復元される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var effectKey = $"curse_{Guid.NewGuid():N}"[..20];
        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = effectKey, Name = "呪い", ClearOnBattleEnd = false,
            EffectTypes = EffectType.StatusModifier,
        });
        db.EffectStatusModifierMasters.Add(new EffectStatusModifierMasterRecord
        {
            EffectKey = effectKey, TargetStatus = TargetStatus.PDef, FixedRate = Ratio.FromPercent(-50m),
        });
        await db.SaveChangesAsync();

        var effects = await EffectCatalog.LoadAsync(db);

        await using (var scope = await Locking.BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            new Repositories.PlayerEffectRepository(db).Add(ids.UserId, new EffectInstance(
                effects.Find(effectKey)!, AffectReason.Skill, Guid.NewGuid(),
                EffectScope.Player, "curse_touch", remainingActions: 7));
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var loader = new PlayerLoader(verifyDb, await EffectCatalog.LoadAsync(verifyDb));

        var player = await loader.LoadAsync(ids.UserId);

        var restored = Assert.Single(player.Effects);
        Assert.Equal(effectKey, restored.EffectKey);
        Assert.Equal(EffectScope.Player, restored.Scope);
        Assert.Equal<ushort?>(7, restored.RemainingActions);
        // 固定変動がステータスへ効いている
        Assert.Equal(GameConstants.DefenseScale * player.Level / 2, player.PDef);
    }

    [DatabaseFact]
    public async Task 参加者の識別子を与えて復元できる()
    {
        // 戦闘中は CurrentTarget と台帳の帰属がこのIDで解決されるため、
        // 参加者行の entity_id と実体の Id を一致させる必要がある
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var entityId = Guid.NewGuid();
        var loader = new PlayerLoader(db, await EffectCatalog.LoadAsync(db));

        var player = await loader.LoadAsync(ids.UserId, entityId);

        Assert.Equal(entityId, player.Id);
    }

    [DatabaseFact]
    public async Task 初期行が無いプレイヤーの復元は例外になる()
    {
        await using var db = await fixture.CreateContextAsync();
        var loader = new PlayerLoader(db, await EffectCatalog.LoadAsync(db));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loader.LoadAsync(BattleLockTests.NewIds().UserId));
    }

    // --- ヘルパ ---

    /// <summary>攻撃・回復・付与・解除・離脱を1本に持つスキル。</summary>
    private static async Task<string> SeedSkillAsync(ChidoDbContext db)
    {
        var key = $"s{Guid.NewGuid():N}"[..20];

        db.SkillMasters.Add(NewSkillMaster(key));

        db.SkillMotionMasters.AddRange(
            NewMotion(key, 0, MotionType.Attack, TargetRule.Enemy),
            NewMotion(key, 1, MotionType.Heal, TargetRule.Ally),
            NewMotion(key, 2, MotionType.GrantEffect, TargetRule.Enemy, gateGroup: 1),
            NewMotion(key, 3, MotionType.DispelEffect, TargetRule.Myself),
            NewMotion(key, 4, MotionType.Flee, TargetRule.Myself));

        db.SkillMotionAttackMasters.Add(new SkillMotionAttackMasterRecord
        {
            SkillKey = key, MotionIndex = 0, AttackType = AttackType.Physical,
            Power = 150, Elements = Element.Fire,
        });
        db.SkillMotionHealMasters.Add(new SkillMotionHealMasterRecord
        {
            SkillKey = key, MotionIndex = 1, AttackType = AttackType.Magical, Power = 80,
        });
        db.SkillMotionEffectMasters.Add(new SkillMotionEffectMasterRecord
        {
            SkillKey = key, MotionIndex = 2, EffectKey = "catalog_poison",
            EffectRate = null, AttackType = AttackType.Physical, DurationActions = 4,
        });
        db.SkillMotionDispelMasters.Add(new SkillMotionDispelMasterRecord
        {
            SkillKey = key, MotionIndex = 3, EffectKey = "catalog_poison",
        });

        await db.SaveChangesAsync();
        return key;
    }

    private static SkillMasterRecord NewSkillMaster(string key) => new()
    {
        SkillKey = key,
        Name = "複合スキル",
        Description = null,
        Elements = Element.Fire,
        RequireTp = 300,
        LearnableLevel = new BigInteger(10),
        Priority = 5,
        SpecialProcessKey = null,
    };

    private static SkillMotionMasterRecord NewMotion(
        string key, byte index, MotionType type, TargetRule rule, ushort? gateGroup = null) => new()
    {
        SkillKey = key,
        MotionIndex = index,
        MotionType = type,
        TargetRule = rule,
        AccuracyRate = Ratio.Full,
        AccuracyGateGroup = gateGroup,
    };

    private static async Task<string> SeedEquipmentAsync(ChidoDbContext db)
    {
        var key = $"eq{Guid.NewGuid():N}"[..20];

        db.EquipmentMasters.Add(new EquipmentMasterRecord
        {
            EquipKey = key,
            Name = "試験用の剣",
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

        await db.SaveChangesAsync();
        return key;
    }
}
