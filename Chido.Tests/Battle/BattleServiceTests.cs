using System.Numerics;
using Chido.Battle;
using Chido.Core.Battle;
using Chido.Core.Progression;
using Chido.Data;
using Chido.Data.Entities;
using Chido.Data.Repositories;
using Chido.Data.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Tests.Battle;

/// <summary>
/// 戦闘オーケストレーションの検証（戦闘システム 7.2・7.3）。
///
/// <para>
/// 見るのは「1回のコマンドが1つのトランザクションとして正しく閉じるか」。
/// ダメージ計算・按分・抽選そのものは Core 側の単体テストで固定済みであり、
/// ここでは<b>コマンドをまたいで状態が繋がること</b>を確かめる。
/// </para>
/// </summary>
[Collection(BattleDatabaseCollection.Name)]
public sealed class BattleServiceTests(BattleDatabaseFixture fixture)
{
    [DatabaseFact]
    public async Task 初期化から撃破までが一続きに通る()
    {
        // 初期化 → 攻撃 → 撃破 → 報酬 → 次の組の出現。1戦闘の最短経路
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Attack, guildId, channelId, userId, "prime"));

        Assert.True(outcome.Accepted, string.Join(" / ", outcome.Message.Trailing));
        Assert.Equal(BattleEndReason.PlayerVictory, outcome.EndReason);

        await using var db = await fixture.CreateContextAsync();

        // 報酬が入っている
        var status = await db.BattleStatuses.FirstAsync(x => x.UserId == userId);
        Assert.True(status.Exp > BigInteger.One, $"経験値が増えていない（{status.Exp}）");

        var currency = await db.PlayerCurrencies.FirstAsync(x => x.UserId == userId);
        Assert.True(currency.Amount > BigInteger.Zero);

        // 拘束が解かれている
        Assert.False(await db.PlayerInBattleSessions.AnyAsync(x => x.UserId == userId));

        // 次の組が出現している
        Assert.True(await db.ChannelCurrentEnemies.AnyAsync(x => x.ChannelId == channelId));

        // 累積敵レベルが +1 され、組とレアリティが記録されている
        var channel = await db.ChannelStates.FirstAsync(x => x.ChannelId == channelId);
        Assert.Equal(new BigInteger(2), channel.CumulativeEnemyLevel);
        Assert.Equal(BattleWorld.GroupKey, channel.CurrentGroupKey);
        Assert.NotNull(channel.CurrentRarity);
        Assert.Null(channel.CurrentSessionId);
    }

    [DatabaseFact]
    public async Task 初期化されていないチャンネルでは行動できない()
    {
        // チャンネル行はロックアンカーでもあるため、無い状態で先へ進ませてはならない
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Attack, guildId, channelId, userId, "prime"));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("battle-init"));
    }

    [DatabaseFact]
    public async Task 初期化は冪等ではなく再実行が失敗する()
    {
        // 「既にあるものを消去して作り直す」機能を伴わない（戦闘システム 10.5）
        var world = await NewWorldAsync();
        var (_, channelId, _) = NewIds();

        Assert.True(await world.InitializeAsync(channelId));
        Assert.False(await world.InitializeAsync(channelId));
    }

    [DatabaseFact]
    public async Task ターンが開かない行動ではセッションが生成されない()
    {
        // /target はセッションを生成しない（B-1）。生成の契機はターンが開く行動に限る
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Target, guildId, channelId, userId, "prime", TargetInput: "スライム"));

        Assert.False(outcome.Accepted);

        await using var db = await fixture.CreateContextAsync();
        var channel = await db.ChannelStates.FirstAsync(x => x.ChannelId == channelId);

        Assert.Null(channel.CurrentSessionId);
    }

    [DatabaseFact]
    public async Task 離脱でセッションの拘束が解ける()
    {
        // 単一セッション制約の拘束は /escape でしか自力では解けない（戦闘システム 4.3）
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);
        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        await using (var db = await fixture.CreateContextAsync())
        {
            Assert.True(await db.PlayerInBattleSessions.AnyAsync(x => x.UserId == userId));
        }

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Escape, guildId, channelId, userId, "prime"));

        Assert.True(outcome.Accepted, string.Join(" / ", outcome.Message.Trailing));
        Assert.Equal(BattleEndReason.PlayerEscaped, outcome.EndReason);

        await using var verify = await fixture.CreateContextAsync();
        Assert.False(await verify.PlayerInBattleSessions.AnyAsync(x => x.UserId == userId));
    }

    [DatabaseFact]
    public async Task 離脱した戦闘には再参加できない()
    {
        // B-13。参加者行は残るため、参加中セッションの記録が外れていても再参加は成立しない
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();
        var other = userId + 1;

        await world.InitializeAsync(channelId);

        // 他プレイヤーがセッションを開いておく。自分が離脱してもセッションは終了しない
        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, other, "other"));
        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));
        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Escape, guildId, channelId, userId, "prime"));

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("離脱"));
    }

    [DatabaseFact]
    public async Task 別チャンネルの戦闘には同時に参加できない()
    {
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();
        var otherChannel = channelId + 1;

        await world.InitializeAsync(channelId);
        await world.InitializeAsync(otherChannel);

        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, otherChannel, userId, "prime"));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("別の戦闘"));
    }

    [DatabaseFact]
    public async Task 未習得のスキルは使えない()
    {
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Skill, guildId, channelId, userId, "prime",
                SkillKey: BattleWorld.HealSkillKey));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("習得"));
    }

    [DatabaseFact]
    public async Task 通常攻撃と防御はスキルコマンドから使えない()
    {
        // 習得管理の対象外であり、専用のコマンドから使う
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Skill, guildId, channelId, userId, "prime",
                SkillKey: Core.GameConstants.AttackSkillKey));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("専用のコマンド"));
    }

    [DatabaseFact]
    public async Task 所持していないアイテムは使えない()
    {
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Use, guildId, channelId, userId, "prime",
                ItemKey: BattleWorld.ItemKey));

        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Message.Trailing, t => t.Contains("所持"));
    }

    [DatabaseFact]
    public async Task 使い切りアイテムは成立したターンでのみ減る()
    {
        // 不成立の行動が資源だけを奪ってはならない
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);
        await world.GiveItemAsync(userId, BattleWorld.ItemKey, 2);

        // 存在しない対象を指すと解決不能で弾かれる。ターンが開かないためアイテムも減らない
        var rejected = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Use, guildId, channelId, userId, "prime",
                ItemKey: BattleWorld.ItemKey, TargetInput: "存在しない敵"));

        Assert.False(rejected.Accepted);
        Assert.Equal(2u, await world.QuantityOfAsync(userId, BattleWorld.ItemKey));

        var accepted = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Use, guildId, channelId, userId, "prime",
                ItemKey: BattleWorld.ItemKey));

        Assert.True(accepted.Accepted, string.Join(" / ", accepted.Message.Trailing));
        Assert.Equal(1u, await world.QuantityOfAsync(userId, BattleWorld.ItemKey));
    }

    [DatabaseFact]
    public async Task 解決できない対象はターンを開かない()
    {
        // 受理ゲートで拒否された行動は不成立であり、ターン・TP・反撃・減衰のいずれも発生しない
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Attack, guildId, channelId, userId, "prime",
                TargetInput: "ドラゴン"));

        Assert.False(outcome.Accepted);

        await using var db = await fixture.CreateContextAsync();
        var channel = await db.ChannelStates.FirstAsync(x => x.ChannelId == channelId);

        // セッションの生成ごと巻き戻っている
        Assert.Null(channel.CurrentSessionId);
    }

    [DatabaseFact]
    public async Task 防御で得たダメージ軽減が次のコマンドまで残る()
    {
        // 戦闘内スコープの状態変化がコマンドをまたいで生き残ることの確認。
        // ここが欠けると、マスタ上は定義されている挙動が実行時にだけ消える
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        await using var db = await fixture.CreateContextAsync();

        var session = await new BattleSessionRepository(db).FindActiveAsync(channelId);
        Assert.NotNull(session);

        var participant = await db.BattleParticipants
            .FirstAsync(x => x.SessionId == session.SessionId && x.UserId == userId);

        var effect = await db.BattleEffects.FirstOrDefaultAsync(x => x.EntityId == participant.EntityId);

        Assert.NotNull(effect);
        Assert.Equal(Core.GameConstants.DefendSkillKey, effect.EffectKey);
    }

    [DatabaseFact]
    public async Task TPはコマンドをまたいで積み上がる()
    {
        // 参加者行の書き戻しが漏れると、毎回0から始まって蓄積が成立しない
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);

        // 撃破してしまわないよう防御を選ぶ。反撃は受けるため被攻撃TPも入る
        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        var first = await world.CurrentTpAsync(channelId, userId);

        await world.Battles.ExecuteAsync(
            new BattleActionRequest(BattleActionKind.Defend, guildId, channelId, userId, "prime"));

        var second = await world.CurrentTpAsync(channelId, userId);

        // 防御モーションの再生でそれぞれ +100。被攻撃ぶんが上乗せされるため下限として見る
        Assert.True(first >= Core.GameConstants.TpGainOnDefendMotion, $"1ターン目のTPが {first}");
        Assert.True(second >= first + Core.GameConstants.TpGainOnDefendMotion,
            $"2ターン目に蓄積されていない（{first} → {second}）");
    }

    [DatabaseFact]
    public async Task 習得済みのスキルで味方を回復できる()
    {
        var world = await NewWorldAsync();
        var (guildId, channelId, userId) = NewIds();

        await world.InitializeAsync(channelId);
        await world.LearnAsync(userId, BattleWorld.HealSkillKey);

        var outcome = await world.Battles.ExecuteAsync(
            new BattleActionRequest(
                BattleActionKind.Skill, guildId, channelId, userId, "prime",
                SkillKey: BattleWorld.HealSkillKey));

        Assert.True(outcome.Accepted, string.Join(" / ", outcome.Message.Trailing));
    }

    // --- 足場 ---

    private static (ulong GuildId, ulong ChannelId, ulong UserId) NewIds()
    {
        // 実DBはコレクション内で共有されるため、テストごとに衝突しないIDを引く
        var seed = (ulong)Random.Shared.NextInt64(1_000_000, long.MaxValue / 4);

        return (seed, seed * 2, seed * 4);
    }

    private async Task<TestWorld> NewWorldAsync()
    {
        await using (var db = await fixture.CreateContextAsync())
        {
            await BattleWorld.SeedAsync(db);
        }

        var factory = new FixtureDbContextFactory(fixture);
        var catalogs = new GameCatalogs(factory);
        await catalogs.ReloadAsync();

        return new TestWorld(fixture, factory, catalogs, new BattleService(factory, catalogs));
    }

    private sealed record TestWorld(
        BattleDatabaseFixture Fixture,
        FixtureDbContextFactory Factory,
        GameCatalogs Catalogs,
        BattleService Battles)
    {
        /// <summary>
        /// 戦闘チャンネルの初期化。<c>/battle-init</c> が呼ぶものと同一の経路
        /// （Discord のインタラクションだけを剥がしてある）。
        /// </summary>
        public async Task<bool> InitializeAsync(ulong channelId)
            => (await Battles.InitializeChannelAsync(channelId)).Accepted;

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

        public async Task<ushort> CurrentTpAsync(ulong channelId, ulong userId)
        {
            await using var db = await Fixture.CreateContextAsync();

            var session = await new BattleSessionRepository(db).FindActiveAsync(channelId);
            if (session is null) return 0;

            var participant = await db.BattleParticipants
                .FirstOrDefaultAsync(x => x.SessionId == session.SessionId && x.UserId == userId);

            return participant?.CurrentTp ?? 0;
        }

        public async Task<uint> QuantityOfAsync(ulong userId, string itemKey)
        {
            await using var db = await Fixture.CreateContextAsync();

            return (await db.PlayerItems
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ItemKey == itemKey))?.Quantity ?? 0;
        }
    }

    /// <summary>
    /// フィクスチャが用意したDBへ繋ぐ <see cref="IDbContextFactory{TContext}"/>。
    /// 本番では DI が供給する口を、テストでは同じスキーマ準備の経路に繋ぎ替える。
    /// </summary>
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
