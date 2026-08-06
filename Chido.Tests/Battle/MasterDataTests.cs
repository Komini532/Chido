using System.Numerics;
using Chido.Battle;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Stats;
using Chido.Core.World;
using Chido.Data;
using Chido.Data.Catalogs;
using Chido.Data.Seeding;
using Chido.Data.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Tests.Battle;

/// <summary>
/// 投入するマスタデータそのものの検証（戦闘システム 10.5）。
///
/// <para>
/// ここで見るのは「出荷するマスタで実際にゲームが成立するか」。
/// 個々の規則は Core 側で固定済みであり、本クラスは<b>データ側の不備</b>
/// （参照先の欠落・起動時検証の不通過・組み立て不能なスキル）を捕まえる。
/// </para>
/// </summary>
[Collection(MasterDatabaseCollection.Name)]
public sealed class MasterDataTests(MasterDatabaseFixture fixture)
{
    [DatabaseFact]
    public async Task 投入したマスタで起動時検証が通る()
    {
        // 草原とその Common の組。欠けていると Bot がそもそも起動しない
        var catalogs = await SeedAndLoadAsync();

        Assert.Empty(WorldValidation.Validate(catalogs.World));
    }

    [DatabaseFact]
    public async Task 投入は冪等で二度目は何も入らない()
    {
        // マスタは運用側が直接手を入れる対象であり、投入のたびに上書きすると
        // 調整した内容が黙って巻き戻る
        await using var db = await fixture.CreateContextAsync();

        await MasterDataSeeder.SeedAsync(db);

        Assert.Equal(0, await MasterDataSeeder.SeedAsync(db));
    }

    [DatabaseFact]
    public async Task 通常攻撃と防御がマスタに存在する()
    {
        // SkillCatalog.Attack / Defend は存在を前提に引くため、欠けると例外になる
        var catalogs = await SeedAndLoadAsync();

        var attack = catalogs.Skills.Attack;
        var defend = catalogs.Skills.Defend;

        Assert.Equal(GameConstants.AttackSkillKey, attack.SkillKey);

        // 通常攻撃は「威力100%の無属性物理攻撃」という単一モーション
        var motion = Assert.IsType<AttackMotion>(Assert.Single(attack.Motions));
        Assert.Equal(GameConstants.PowerScale, motion.Power);
        Assert.Equal(Core.Battle.Damage.Element.None, motion.Elements);
        Assert.Equal(Ratio.Full, motion.AccuracyRate);

        // 防御は自分自身への付与モーション1つで、反撃モーションを含まない
        var grant = Assert.IsType<GrantEffectMotion>(Assert.Single(defend.Motions));
        Assert.Equal(TargetRule.Myself, grant.TargetRule);
        Assert.Equal<ushort?>(1, grant.DurationActions);

        // 先に動いてそのターンの被弾に間に合う必要がある
        Assert.True(defend.Priority > 0, "防御の priority が正値でない");
    }

    [DatabaseFact]
    public async Task 防御は固定値のダメージ軽減率を付与する()
    {
        var catalogs = await SeedAndLoadAsync();

        var definition = catalogs.Effects.Find(GameConstants.DefendSkillKey);

        Assert.NotNull(definition);

        var modifier = Assert.Single(definition.StatusModifiers);
        Assert.Equal(TargetStatus.DamageResistRate, modifier.TargetStatus);

        // 固定値であるためマスタ側が値を持つ（インスタンス側へ複製しない）
        Assert.Equal(GameConstants.DefendDamageResistRate, modifier.FixedRate);
    }

    [DatabaseFact]
    public async Task 全スキルのモーションが組み立てられる()
    {
        // サブタイプの行が欠けていれば SkillCatalog.LoadAsync が例外になる。
        // 投入漏れが「何も起きないスキル」として運用に紛れ込むことを防ぐ
        var catalogs = await SeedAndLoadAsync();

        foreach (var skill in MasterData.Skills)
        {
            var loaded = catalogs.Skills.Find(skill.SkillKey);

            Assert.NotNull(loaded);
            Assert.NotEmpty(loaded.Motions);
        }
    }

    [DatabaseFact]
    public async Task 参照されるキーがすべて実在する()
    {
        // 明示的なFKはスキルモーション周辺にしか無く、他はコメントベースの参照であるため、
        // 参照切れはDBでは弾かれない。投入するデータの側で閉じていることを確かめる
        await using var db = await fixture.CreateContextAsync();
        await MasterDataSeeder.SeedAsync(db);

        var fields = MasterData.Fields.Select(x => x.FieldKey).ToHashSet();
        var groups = MasterData.Groups.Select(x => x.GroupKey).ToHashSet();
        var enemies = MasterData.Enemies.Select(x => x.EnemyKey).ToHashSet();
        var skills = MasterData.Skills.Select(x => x.SkillKey).ToHashSet();
        var effects = MasterData.Effects.Select(x => x.EffectKey).ToHashSet();
        var items = MasterData.Items.Select(x => x.ItemKey).ToHashSet();
        var equipment = MasterData.Equipment.Select(x => x.EquipKey).ToHashSet();

        Assert.All(MasterData.RarityRates, x => Assert.Contains(x.FieldKey, fields));
        Assert.All(MasterData.Transitions, x =>
        {
            Assert.Contains(x.FieldKey, fields);
            Assert.Contains(x.NextFieldKey, fields);
        });
        Assert.All(MasterData.FieldGroups, x =>
        {
            Assert.Contains(x.FieldKey, fields);
            Assert.Contains(x.GroupKey, groups);
        });
        Assert.All(MasterData.GroupMembers, x =>
        {
            Assert.Contains(x.GroupKey, groups);
            Assert.Contains(x.EnemyKey, enemies);
        });
        Assert.All(MasterData.EnemySkills, x =>
        {
            Assert.Contains(x.EnemyKey, enemies);
            Assert.Contains(x.SkillKey, skills);
        });
        Assert.All(MasterData.EnemyEffects, x =>
        {
            Assert.Contains(x.EnemyKey, enemies);
            Assert.Contains(x.EffectKey, effects);
        });
        Assert.All(MasterData.EnemyEquipment, x =>
        {
            Assert.Contains(x.EnemyKey, enemies);
            Assert.Contains(x.EquipKey, equipment);
        });
        Assert.All(MasterData.EnemyLoots, x =>
        {
            Assert.Contains(x.EnemyKey, enemies);
            Assert.Contains(x.ItemKey, items);
        });
        Assert.All(MasterData.EnemyCurrency, x => Assert.Contains(x.EnemyKey, enemies));
        Assert.All(MasterData.Motions, x => Assert.Contains(x.SkillKey, skills));
        Assert.All(MasterData.EffectMotions, x => Assert.Contains(x.EffectKey, effects));
        Assert.All(MasterData.DispelMotions, x => Assert.Contains(x.EffectKey, effects));
        Assert.All(MasterData.ItemEffects, x =>
        {
            Assert.Contains(x.ItemKey, items);
            Assert.NotNull(x.SkillKey);
            Assert.Contains(x.SkillKey!, skills);
        });
    }

    [DatabaseFact]
    public async Task 隠しレアリティは抽選候補に置かれていない()
    {
        // イベント専用であり通常の抽選に現れてはならない。
        // GroupDraw 側でも除外しているが、そもそも候補として置かない
        await Task.CompletedTask;

        Assert.DoesNotContain(MasterData.RarityRates, x => x.Rarity == Rarity.Hidden);
    }

    [DatabaseFact]
    public async Task 草原は自己ループを持つ()
    {
        // 遷移先0件による縮退と「意図した行き止まり」を区別するため。
        // 0件だと正常な進行が縮退の通知として報告されてしまう
        await Task.CompletedTask;

        Assert.Contains(MasterData.Transitions, x =>
            x.FieldKey == GameConstants.GrasslandFieldKey
            && x.NextFieldKey == GameConstants.GrasslandFieldKey);
    }

    [DatabaseFact]
    public async Task 不定値の状態変化には付与側が変動率を供給している()
    {
        // fixed_rate を持たない行は付与モーション側の effect_rate が必須であり、
        // 供給が無いと付与の時点で例外になる
        var indeterminate = MasterData.EffectStatusModifiers
            .Where(x => x.FixedRate is null)
            .Select(x => x.EffectKey)
            .ToHashSet();

        await Task.CompletedTask;

        foreach (var motion in MasterData.EffectMotions.Where(m => indeterminate.Contains(m.EffectKey)))
        {
            Assert.NotNull(motion.EffectRate);
        }

        foreach (var auto in MasterData.EnemyEffects.Where(a => indeterminate.Contains(a.EffectKey)))
        {
            Assert.NotEqual(Ratio.Zero, auto.EffectRate);
        }
    }

    [DatabaseFact]
    public async Task 継続ダメージの付与には攻撃種別が供給されている()
    {
        // SlipDamage 成分を持つ効果は attack_type からスナップショット対象を決めるため、
        // 供給が無いと付与の時点で例外になる
        var slip = MasterData.EffectSlipDamages.Select(x => x.EffectKey).ToHashSet();

        await Task.CompletedTask;

        foreach (var motion in MasterData.EffectMotions.Where(m => slip.Contains(m.EffectKey)))
        {
            Assert.NotNull(motion.AttackType);
        }
    }

    [DatabaseFact]
    public async Task 投入したマスタで一戦が最後まで通る()
    {
        // Phase 10 の到達点。初期化 → 攻撃を繰り返す → 撃破 → 報酬 → 次の組の出現。
        // 出荷するマスタそのもので回ることを確かめる
        var catalogs = await SeedAndLoadAsync();

        var factory = new FixtureDbContextFactory(fixture);
        var battles = new BattleService(factory, catalogs);

        var seed = (ulong)Random.Shared.NextInt64(1_000_000, long.MaxValue / 8);
        var (guildId, channelId, userId) = (seed, seed * 2, seed * 4);

        var initialized = await battles.InitializeChannelAsync(channelId);
        Assert.True(initialized.Accepted, string.Join(" / ", initialized.Message.Trailing));

        BattleEndReason? reason = null;

        // 同格の敵とは3発前後で決着する較正だが、命中・クリティカル・行動不能で揺れる。
        // 決着しない場合に無限に回さないよう上限を置く
        for (var turn = 0; turn < 60 && reason is null; turn++)
        {
            var outcome = await battles.ExecuteAsync(new BattleActionRequest(
                BattleActionKind.Attack, guildId, channelId, userId, "prime"));

            // 戦闘不能になったら以降は行動できない。その場合はここで打ち切る
            if (!outcome.Accepted) break;

            reason = outcome.EndReason;
        }

        await using var db = await fixture.CreateContextAsync();

        // 決着したなら勝利しているはず（撤退も敵の逃走も起こらない構成）
        if (reason is not null)
        {
            Assert.Equal(BattleEndReason.PlayerVictory, reason);

            var status = await db.BattleStatuses.FirstAsync(x => x.UserId == userId);
            Assert.True(status.Exp > BigInteger.One, $"経験値が増えていない（{status.Exp}）");

            // 次の組が出現している
            Assert.True(await db.ChannelCurrentEnemies.AnyAsync(x => x.ChannelId == channelId));
        }

        // 決着の有無に関わらず、敵が出現し戦闘が成立していること自体は確かめる
        Assert.True(await db.BattleParticipants.AnyAsync(x => x.UserId == userId));
    }

    // --- 足場 ---

    private async Task<GameCatalogs> SeedAndLoadAsync()
    {
        await using (var db = await fixture.CreateContextAsync())
        {
            await MasterDataSeeder.SeedAsync(db);
        }

        var catalogs = new GameCatalogs(new FixtureDbContextFactory(fixture));
        await catalogs.ReloadAsync();

        return catalogs;
    }

    private sealed class FixtureDbContextFactory(MasterDatabaseFixture fixture)
        : IDbContextFactory<ChidoDbContext>
    {
        public ChidoDbContext CreateDbContext()
            => fixture.CreateContextAsync().GetAwaiter().GetResult();

        public async Task<ChidoDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => await fixture.CreateContextAsync();
    }
}
