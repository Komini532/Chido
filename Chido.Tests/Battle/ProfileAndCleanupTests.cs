using System.Numerics;
using Chido.Battle;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Equipment;
using Chido.Core.Progression;
using Chido.Core.Stats;
using Chido.Data;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Chido.Data.Repositories;
using Chido.Data.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Chido.Tests.Battle;

/// <summary>
/// 非戦闘コマンドとチャンネル消失の後始末の検証（戦闘システム 6.3・9.1・2.5）。
/// </summary>
[Collection(BattleDatabaseCollection.Name)]
public sealed class ProfileAndCleanupTests(BattleDatabaseFixture fixture)
{
    private const string SwordKey = "test_sword";
    private const string RingKey = "test_ring";

    // --- /status ---

    [DatabaseFact]
    public async Task 初回のプレイヤーでもステータスを表示できる()
    {
        // /status が「まず戦ってこい」と突き放す理由はない
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var profile = await world.Profiles.LoadAsync(userId, "prime");

        // 経験値の初期値は 1。0 だと level = √exp = 0 となり全ステータスが 0 になって成立しない
        Assert.Equal(BigInteger.One, profile.Exp);
        Assert.Equal(BigInteger.One, profile.Level);
        Assert.Empty(profile.Equipment);
        Assert.Empty(profile.Titles);
        Assert.Empty(profile.Effects);
    }

    [DatabaseFact]
    public async Task ステータスは永続スコープの状態変化のみを含む()
    {
        // 分離の基準は「セッションに属するか否か」であって「状態変化か否か」ではない（9.1）。
        // 戦闘中でないプレイヤーが、自分に何が残り何行動効いているかを知る手段が他に無い
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);
        await world.GrantCurseAsync(userId, channelId, remaining: 7);

        // 戦闘スコープの効果を持たせる。これは /status には現れてはならない
        await world.LearnAsync(userId, BattleWorld.BuffSkillKey);
        await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Skill, guildId, channelId, userId, "prime",
            SkillKey: BattleWorld.BuffSkillKey));

        var profile = await world.Profiles.LoadAsync(userId, "prime");

        var effect = Assert.Single(profile.Effects);
        Assert.Equal(CurseKey, effect.EffectKey);
        Assert.Equal(EffectScope.Player, effect.Scope);

        // 行動者は関与者集合に入るため、そのターンで1つ消費される。
        // 永続スコープの減衰が戦闘行動を通して効いていることの確認でもある
        Assert.Equal<ushort?>(6, effect.RemainingActions);
    }

    [DatabaseFact]
    public async Task 獲得済みの称号が表示される()
    {
        var world = await NewWorldAsync();
        var userId = NewUserId();

        await world.GrantTitleAsync(userId);

        var profile = await world.Profiles.LoadAsync(userId, "prime");

        var title = Assert.Single(profile.Titles);
        Assert.Equal("称号", title.Name);
    }

    // --- /inventory ---

    [DatabaseFact]
    public async Task 所持数0のアイテムは並ばない()
    {
        var world = await NewWorldAsync();
        var userId = NewUserId();

        await world.GiveItemAsync(userId, BattleWorld.ItemKey, 0);

        Assert.Empty(await world.Profiles.InventoryAsync(userId, "prime"));
    }

    // --- /equip ---

    [DatabaseFact]
    public async Task 装着した装備がステータスに反映される()
    {
        // ステータスは動的算出であるため、装着を書き換えれば次の参照から即座に反映される
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var before = await world.Profiles.LoadAsync(userId, "prime");

        var sword = await world.GiveEquipmentAsync(userId, SwordKey);
        var outcome = await world.Equipment.EquipAsync(userId, "prime", sword.ToString());

        Assert.True(outcome.Accepted, outcome.Refusal);
        Assert.Equal(EquipPart.Weapon, outcome.Part);
        Assert.Null(outcome.Displaced);

        var after = await world.Profiles.LoadAsync(userId, "prime");

        Assert.True(after.PAtk > before.PAtk, $"物理攻撃が伸びていない（{before.PAtk} → {after.PAtk}）");
        Assert.Equal(EquipPart.Weapon, Assert.Single(after.Equipment).Part);
    }

    [DatabaseFact]
    public async Task 同じ部位の装備は押し出される()
    {
        // 1部位につき1つまで。押し出された装備は所持に残る
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var first = await world.GiveEquipmentAsync(userId, SwordKey);
        var second = await world.GiveEquipmentAsync(userId, SwordKey);

        await world.Equipment.EquipAsync(userId, "prime", first.ToString());
        var outcome = await world.Equipment.EquipAsync(userId, "prime", second.ToString());

        Assert.True(outcome.Accepted, outcome.Refusal);
        Assert.NotNull(outcome.Displaced);

        var profile = await world.Profiles.LoadAsync(userId, "prime");

        // 装着は1つだけ。2つ分の補正は乗らない
        Assert.Equal(second, Assert.Single(profile.Equipment).Equipment.InstanceId);

        // 外した装備は所持に残っている
        await using var db = await fixture.CreateContextAsync();
        Assert.Equal(2, await db.PlayerEquipments.CountAsync(x => x.UserId == userId));
    }

    [DatabaseFact]
    public async Task 複数部位に適合する装備は空いている最小の部位へ入る()
    {
        // 装備可能部位は「いずれかを選んで装着できる」という択一の候補提示であり、
        // どこへ入るかはアプリ側が決めるほかない
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var ring = await world.GiveEquipmentAsync(userId, RingKey);
        var another = await world.GiveEquipmentAsync(userId, RingKey);

        var head = await world.Equipment.EquipAsync(userId, "prime", ring.ToString());
        var accessory = await world.Equipment.EquipAsync(userId, "prime", another.ToString());

        Assert.Equal(EquipPart.Head, head.Part);
        Assert.Equal(EquipPart.Accessory1, accessory.Part);
        Assert.Null(accessory.Displaced);
    }

    [DatabaseFact]
    public async Task 所持していない装備は装着できない()
    {
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var outcome = await world.Equipment.EquipAsync(userId, "prime", Guid.NewGuid().ToString());

        Assert.False(outcome.Accepted);
        Assert.Contains("所持", outcome.Refusal);
    }

    [DatabaseFact]
    public async Task 候補から選ばれていない入力は拒否される()
    {
        var world = await NewWorldAsync();
        var userId = NewUserId();

        var outcome = await world.Equipment.EquipAsync(userId, "prime", "つよいけん");

        Assert.False(outcome.Accepted);
        Assert.Contains("候補", outcome.Refusal);
    }

    [DatabaseFact]
    public async Task 戦闘中でも装備を変更できる()
    {
        // ロックは ① → ③ で②を飛ばす。戦闘行動と競合しないことの確認でもある
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);
        await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        var sword = await world.GiveEquipmentAsync(userId, SwordKey);
        var outcome = await world.Equipment.EquipAsync(userId, "prime", sword.ToString());

        Assert.True(outcome.Accepted, outcome.Refusal);
    }

    // --- ChannelMissing ---

    [DatabaseFact]
    public async Task チャンネル消失でセッションと永続状態が消える()
    {
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);
        await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        Assert.True(await world.Cleanup.CleanupAsync(channelId));

        await using var db = await fixture.CreateContextAsync();

        // チャンネルの永続状態と出現中の敵の記録が消えている
        Assert.False(await db.ChannelStates.AnyAsync(x => x.ChannelId == channelId));
        Assert.False(await db.ChannelCurrentEnemies.AnyAsync(x => x.ChannelId == channelId));

        // 拘束が解けている。ここが漏れると、消えたチャンネルのセッションに参加していた
        // プレイヤーが永久に他の戦闘へ参加できなくなる
        Assert.False(await db.PlayerInBattleSessions.AnyAsync(x => x.UserId == userId));
    }

    [DatabaseFact]
    public async Task チャンネル消失では次の敵を出さない()
    {
        // チャンネルごと消えているため出現先が存在しない（戦闘システム 6.3）
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);
        await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        await world.Cleanup.CleanupAsync(channelId);

        await using var db = await fixture.CreateContextAsync();

        Assert.False(await db.ChannelCurrentEnemies.AnyAsync(x => x.ChannelId == channelId));
    }

    [DatabaseFact]
    public async Task 消失の後始末は冪等である()
    {
        // 能動検知と定期検証の二層で拾うため、同じチャンネルに二度到達しうる
        var world = await NewWorldAsync();
        var (_, channelId, _) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);

        Assert.True(await world.Cleanup.CleanupAsync(channelId));
        Assert.False(await world.Cleanup.CleanupAsync(channelId));
    }

    [DatabaseFact]
    public async Task 戦闘チャンネルでなければ何もしない()
    {
        var world = await NewWorldAsync();
        var (_, channelId, _) = NewIds();

        Assert.False(await world.Cleanup.CleanupAsync(channelId));
    }

    [DatabaseFact]
    public async Task 消失後は他の戦闘に参加できる()
    {
        // 拘束が解けたことを、実際に別チャンネルで行動できることで確かめる
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();
        var other = channelId + 1;

        await world.Battles.InitializeChannelAsync(channelId);
        await world.Battles.InitializeChannelAsync(other);

        await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        await world.Cleanup.CleanupAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(new BattleActionRequest(
            BattleActionKind.Defend, guildId, other, userId, "prime"));

        Assert.True(outcome.Accepted, string.Join(" / ", outcome.Message.Trailing));
    }

    [DatabaseFact]
    public async Task 追跡対象は戦闘チャンネルに限られる()
    {
        // 定期検証が突き合わせる対象。行の存在自体が「戦闘チャンネルである」ことを意味する
        var world = await NewWorldAsync();
        var (_, channelId, _) = NewIds();

        await world.Battles.InitializeChannelAsync(channelId);

        Assert.Contains(channelId, await world.Cleanup.TrackedChannelsAsync());
    }

    // --- 足場 ---

    private const string CurseKey = "test_curse";

    private static ulong NewUserId()
        => (ulong)Random.Shared.NextInt64(1_000_000, long.MaxValue / 8) * 8;

    private static (ulong GuildId, ulong ChannelId, ulong UserId) NewIds()
    {
        var seed = (ulong)Random.Shared.NextInt64(1_000_000, long.MaxValue / 8);

        return (seed, seed * 2, seed * 4);
    }

    private async Task<TestWorld> NewWorldAsync()
    {
        await using (var db = await fixture.CreateContextAsync())
        {
            await BattleWorld.SeedAsync(db);
            await SeedProfileMastersAsync(db);
        }

        var factory = new FixtureDbContextFactory(fixture);
        var catalogs = new GameCatalogs(factory);
        await catalogs.ReloadAsync();

        return new TestWorld(
            fixture,
            catalogs,
            new BattleService(factory, catalogs),
            new PlayerProfileService(factory, catalogs),
            new EquipmentService(factory),
            new ChannelCleanupService(factory, NullLogger<ChannelCleanupService>.Instance));
    }

    /// <summary>装備・称号・永続効果のマスタ。本クラスの検証だけが使う。</summary>
    private static async Task SeedProfileMastersAsync(ChidoDbContext db)
    {
        if (await db.EquipmentMasters.AnyAsync(x => x.EquipKey == SwordKey)) return;

        // 武器のみに適合する
        db.EquipmentMasters.Add(NewEquipment(SwordKey, "つるぎ", EquipPart.Weapon));

        // 頭とアクセサリの双方に適合する。択一の候補提示の検証用
        db.EquipmentMasters.Add(NewEquipment(RingKey, "わっか", EquipPart.Head | EquipPart.Accessory1));

        db.TitleMasters.Add(new TitleMasterRecord
        {
            TitleKey = "test_title", Name = "称号", Emoji = "🏅",
            AcquisitionType = TitleAcquisitionType.LevelReached,
            ConditionKey = null, ConditionValue = BigInteger.One,
        });

        // 戦闘を跨ぐ状態変化。持続は必ず有限でなければならない
        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = CurseKey, Name = "呪い",
            ClearOnBattleEnd = false, EffectTypes = EffectType.StatusModifier,
        });
        db.EffectStatusModifierMasters.Add(new EffectStatusModifierMasterRecord
        {
            EffectKey = CurseKey, TargetStatus = TargetStatus.PDef,
            FixedRate = Ratio.FromPercent(-20m),
        });

        await db.SaveChangesAsync();
    }

    private static EquipmentMasterRecord NewEquipment(string key, string name, EquipPart parts) => new()
    {
        EquipKey = key,
        Name = name,
        EquipParts = parts,
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
    };

    private sealed record TestWorld(
        BattleDatabaseFixture Fixture,
        GameCatalogs Catalogs,
        BattleService Battles,
        PlayerProfileService Profiles,
        EquipmentService Equipment,
        ChannelCleanupService Cleanup)
    {
        public async Task<Guid> GiveEquipmentAsync(ulong userId, string equipKey)
        {
            await using var db = await Fixture.CreateContextAsync();

            await new PlayerRepository(db).EnsureAsync(userId, "prime");

            var instanceId = Guid.NewGuid();

            db.PlayerEquipments.Add(new PlayerEquipmentRecord
            {
                InstanceId = instanceId, UserId = userId, EquipKey = equipKey,
            });

            await db.SaveChangesAsync();
            return instanceId;
        }

        public async Task GiveItemAsync(ulong userId, string itemKey, uint quantity)
        {
            await using var db = await Fixture.CreateContextAsync();

            await new PlayerRepository(db).EnsureAsync(userId, "prime");

            db.PlayerItems.Add(new PlayerItemRecord
            {
                UserId = userId, ItemKey = itemKey, Quantity = quantity,
            });

            await db.SaveChangesAsync();
        }

        public async Task GrantTitleAsync(ulong userId)
        {
            await using var db = await Fixture.CreateContextAsync();

            await new PlayerRepository(db).EnsureAsync(userId, "prime");

            db.PlayerTitles.Add(new PlayerTitleRecord { UserId = userId, TitleKey = "test_title" });

            await db.SaveChangesAsync();
        }

        public async Task LearnAsync(ulong userId, string skillKey)
        {
            await using var db = await Fixture.CreateContextAsync();

            await new PlayerRepository(db).EnsureAsync(userId, "prime");

            db.PlayerSkills.Add(new PlayerSkillRecord
            {
                UserId = userId, SkillKey = skillKey, LearnedReason = LearnedReason.Level,
            });

            await db.SaveChangesAsync();
        }

        /// <summary>永続スコープの効果を1つ持たせる。書き込みはチャンネル行②の配下で行う。</summary>
        public async Task GrantCurseAsync(ulong userId, ulong channelId, ushort remaining)
        {
            await using var db = await Fixture.CreateContextAsync();

            await new PlayerRepository(db).EnsureAsync(userId, "prime");

            var effects = await EffectCatalog.LoadAsync(db);

            await using var scope = await Data.Locking.BattleLock.BeginAsync(db);
            await scope.LockChannelAsync(channelId);

            new PlayerEffectRepository(db).Add(userId, new EffectInstance(
                effects.Find(CurseKey)!, AffectReason.Skill, Guid.NewGuid(),
                EffectScope.Player, "curse_touch", remaining));

            await scope.CommitAsync();
        }
    }

    private sealed class FixtureDbContextFactory(BattleDatabaseFixture fixture)
        : IDbContextFactory<ChidoDbContext>
    {
        public ChidoDbContext CreateDbContext()
            => fixture.CreateContextAsync().GetAwaiter().GetResult();

        public async Task<ChidoDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => await fixture.CreateContextAsync();
    }
}
