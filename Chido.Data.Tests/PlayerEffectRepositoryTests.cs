using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Stats;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// 戦闘を跨ぐ状態変化の永続化の検証（戦闘システム 5.4）。
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlayerEffectRepositoryTests(DatabaseFixture fixture)
{
    private static EffectDefinition Curse => new(
        "curse", "呪い", clearOnBattleEnd: false,
        statusModifiers: [new StatusModifierSpec(TargetStatus.PDef, Ratio.FromPercent(-20m))]);

    private static EffectDefinition Blessing => new(
        "blessing", "祝福", clearOnBattleEnd: false,
        statusModifiers: [new StatusModifierSpec(TargetStatus.PAtk, Ratio.FromPercent(10m))]);

    [DatabaseFact]
    public async Task 減衰はその場で行われ使い切った行は同一トランザクションで消える()
    {
        // 作業コピー方式（戦闘開始時に複製し終了時に書き戻す）は採らない。
        // セッションは長期間開きっぱなしになりうるため書き戻しの契機が来る保証がなく、
        // 永続効果の真値が無期限に戦闘テーブルに人質に取られる
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var effects = new PlayerEffectRepository(db);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            effects.Add(ids.UserId, NewInstance(Curse, remainingActions: 2));
            effects.Add(ids.UserId, NewInstance(Blessing, remainingActions: 1));
            await scope.CommitAsync();
        }

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            var expired = await effects.DecayAsync(ids.UserId);
            Assert.Equal(1, expired);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();
        var remaining = await new PlayerEffectRepository(verifyDb).LoadAsync(ids.UserId);

        // 使い切った祝福は消え、呪いだけが残り有効行動数1で残る
        var curse = Assert.Single(remaining);
        Assert.Equal("curse", curse.EffectKey);
        Assert.Equal(1, curse.RemainingActions);
    }

    [DatabaseFact]
    public async Task 解除はeffect_keyのみで一致する行をすべて落とす()
    {
        // 「解毒」は毒の出所を問わない。付与の重複判定キーとは意図的に非対称
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var effects = new PlayerEffectRepository(db);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);

            // 付与者も付与元も異なる2件と、無関係な1件
            effects.Add(ids.UserId, NewInstance(Curse, remainingActions: 5, grantSourceKey: "curse_touch"));
            effects.Add(ids.UserId, NewInstance(Curse, remainingActions: 5, grantSourceKey: "curse_bolt"));
            effects.Add(ids.UserId, NewInstance(Blessing, remainingActions: 5));
            await scope.CommitAsync();
        }

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            Assert.Equal(2, await effects.DispelAsync(ids.UserId, "curse"));
            await scope.CommitAsync();
        }

        var remaining = await effects.LoadAsync(ids.UserId);

        Assert.Equal("blessing", Assert.Single(remaining).EffectKey);
    }

    [DatabaseFact]
    public async Task 他プレイヤーの行はチャンネル行のみを保持して書き込める()
    {
        // target_rule = 味方 の付与・解除は他プレイヤーの行を書くが、行動者は他プレイヤーの
        // 行①を取得しない。チャンネル行②が全戦闘行動の直列化点であるため、
        // これを保持している間はセッション参加者全員の chido_player_effect への書き込みが包摂される
        var ids = BattleLockTests.NewIds();
        var ally = ids.UserId + 1000;

        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        await new PlayerRepository(db).EnsureAsync(ally, "味方");

        var effects = new PlayerEffectRepository(db);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockPlayerAsync(ids.UserId);
            await scope.LockChannelAsync(ids.ChannelId);
            effects.Add(ally, NewInstance(Blessing, remainingActions: 3));
            await scope.CommitAsync();
        }

        Assert.Single(await effects.LoadAsync(ally));
        Assert.Empty(await effects.LoadAsync(ids.UserId));
    }

    [DatabaseFact]
    public async Task 戦闘内スコープの効果は書き込めない()
    {
        await using var db = await fixture.CreateContextAsync();
        var effects = new PlayerEffectRepository(db);

        var battleScoped = new EffectInstance(
            new EffectDefinition("poison", "毒", slipDamage: new SlipDamageSpec(20)),
            AffectReason.Skill, Guid.NewGuid(), EffectScope.Battle,
            grantSourceKey: "poison_touch", remainingActions: 3);

        Assert.Throws<InvalidOperationException>(() => effects.Add(1, battleScoped));
    }

    [DatabaseFact]
    public async Task 持続を持たない効果は書き込めない()
    {
        // 戦闘を跨ぐ効果の終わりを保証するものは残り有効行動数しかない。
        // 真に永久な効果を許すと、加算合成される永続デバフが単調増加して上限なくステータスを蝕む
        await using var db = await fixture.CreateContextAsync();
        var effects = new PlayerEffectRepository(db);

        var endless = new EffectInstance(
            Curse, AffectReason.Skill, Guid.NewGuid(), EffectScope.Player,
            grantSourceKey: "curse_touch", remainingActions: null);

        Assert.Throws<InvalidOperationException>(() => effects.Add(1, endless));
    }

    private static EffectInstance NewInstance(
        EffectDefinition definition, ushort remainingActions, string grantSourceKey = "skill")
        => new(definition, AffectReason.Skill, Guid.NewGuid(), EffectScope.Player,
            grantSourceKey, remainingActions);
}
