using System.Numerics;
using Chido.Core.Battle;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// セッション・参加者・参加中セッションの永続化の検証（戦闘システム 4.3・6.1・7.2）。
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BattleSessionRepositoryTests(DatabaseFixture fixture)
{
    // --- セッション生成レース ---

    [DatabaseFact]
    public async Task 並行するセッション生成でも2つ目は生まれない()
    {
        // MySQL は「ended_at IS NULL の行がチャンネルごとに1つ」という部分ユニークインデックスを
        // 張れないため、DB制約では防げない。チャンネル行②を先に取得することが唯一の担保である
        var ids = BattleLockTests.NewIds();
        await using var seedDb = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(seedDb, ids);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => StartBattleAsync(ids)));

        await using var verifyDb = await fixture.CreateContextAsync();
        var sessions = await verifyDb.BattleSessions
            .Where(x => x.ChannelId == ids.ChannelId)
            .ToListAsync();

        // 生成されたセッションは1つだけで、全員が同じセッションを見ている
        Assert.Single(sessions);
        Assert.All(results, id => Assert.Equal(sessions[0].SessionId, id));
    }

    /// <summary>「セッションが無ければ作る」という戦闘行動の入口を、ロック順序どおりに1回分実行する。</summary>
    private async Task<Guid> StartBattleAsync(BattleLockTests.Ids ids)
    {
        await using var db = await fixture.CreateContextAsync();
        var sessions = new BattleSessionRepository(db);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockPlayerAsync(ids.UserId);
        await scope.LockChannelAsync(ids.ChannelId);

        var session = await sessions.FindActiveAsync(ids.ChannelId)
                      ?? await sessions.CreateAsync(ids.GuildId, ids.ChannelId);

        await scope.CommitAsync();
        return session.SessionId;
    }

    [DatabaseFact]
    public async Task 進行中セッションがあるチャンネルでの生成は拒否される()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        var sessions = new BattleSessionRepository(db);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockChannelAsync(ids.ChannelId);
        await sessions.CreateAsync(ids.GuildId, ids.ChannelId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sessions.CreateAsync(ids.GuildId, ids.ChannelId));
    }

    // --- 単一セッション制約 ---

    [DatabaseFact]
    public async Task 別チャンネルの戦闘へは同時に参加できない()
    {
        // 1プレイヤー1行という構造が単一セッション制約を裏付ける
        var first = BattleLockTests.NewIds();
        var second = BattleLockTests.NewIds() with { UserId = first.UserId };

        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, first);
        await new ChannelStateRepository(db).EnsureAsync(second.ChannelId);

        var sessions = new BattleSessionRepository(db);
        var sessionA = await CreateAndJoinAsync(db, sessions, first);

        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockChannelAsync(second.ChannelId);
        var sessionB = await sessions.CreateAsync(second.GuildId, second.ChannelId);

        // 専用の型で投げる。呼び出し側はこれを正常系の拒否として扱うため、
        // 実装の不具合と同じ型にすると、本当の失敗が無関係な案内に化けて表に出なくなる
        var error = await Assert.ThrowsAsync<SingleSessionViolationException>(
            () => sessions.JoinPlayerAsync(sessionB.SessionId, first.UserId, displayOrder: 0));

        Assert.Equal(first.UserId, error.UserId);
        Assert.Contains(sessionA.ToString(), error.Message);
    }

    [DatabaseFact]
    public async Task 同じセッションへの再参加は新しい参加者行を作らない()
    {
        // 飛び入り参加が前提のため、進行中セッションへの途中参加は常に許可される。
        // 一方で同じプレイヤーが二重に列挙されてはならない
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var sessions = new BattleSessionRepository(db);
        var sessionId = await CreateAndJoinAsync(db, sessions, ids);

        var again = await sessions.JoinPlayerAsync(sessionId, ids.UserId, displayOrder: 5);

        var participants = await sessions.LoadParticipantsAsync(sessionId);
        Assert.Single(participants);
        Assert.Equal(participants[0].EntityId, again.EntityId);
        // 既存行が返るため、後から渡した表示順で上書きされることもない
        Assert.Equal(0, participants[0].DisplayOrder);
    }

    // --- セッション終了 ---

    [DatabaseFact]
    public async Task セッション終了で参加中セッションが一括削除される()
    {
        // 漏れると、チャンネル消失時に Defeated だったプレイヤーが永久に他の戦闘へ参加できなくなる
        var ids = BattleLockTests.NewIds();
        var secondUser = ids.UserId + 1000;

        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);
        await new PlayerRepository(db).EnsureAsync(secondUser, "P2");

        var sessions = new BattleSessionRepository(db);
        var sessionId = await CreateAndJoinAsync(db, sessions, ids);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await sessions.JoinPlayerAsync(sessionId, secondUser, displayOrder: 1);
            await scope.CommitAsync();
        }

        Assert.Equal(2, await db.PlayerInBattleSessions.CountAsync(x => x.SessionId == sessionId));

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await sessions.FinishAsync(sessionId, BattleEndReason.PlayerVictory);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();

        Assert.Empty(await verifyDb.PlayerInBattleSessions.Where(x => x.SessionId == sessionId).ToListAsync());
        // 参加者行は物理削除しない（戦闘終了後も記録として残す）
        Assert.Equal(2, await verifyDb.BattleParticipants.CountAsync(x => x.SessionId == sessionId));

        var channel = await verifyDb.ChannelStates.FirstAsync(x => x.ChannelId == ids.ChannelId);
        Assert.Null(channel.CurrentSessionId);
    }

    [DatabaseFact]
    public async Task 終了理由を問わず拘束は解かれる()
    {
        // ChannelMissing でも同じ。end_reason の値に依存させると、
        // チャンネル消失という最も救済が要る経路で拘束が残る
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var sessions = new BattleSessionRepository(db);
        var sessionId = await CreateAndJoinAsync(db, sessions, ids);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await sessions.FinishAsync(sessionId, BattleEndReason.ChannelMissing);
            await scope.CommitAsync();
        }

        Assert.Empty(await db.PlayerInBattleSessions.Where(x => x.UserId == ids.UserId).ToListAsync());

        // 拘束が解けているため、別チャンネルの戦闘へ参加できる
        var next = BattleLockTests.NewIds() with { UserId = ids.UserId };
        await new ChannelStateRepository(db).EnsureAsync(next.ChannelId);
        await CreateAndJoinAsync(db, sessions, next);

        Assert.Single(await db.PlayerInBattleSessions.Where(x => x.UserId == ids.UserId).ToListAsync());
    }

    [DatabaseFact]
    public async Task 終了後のチャンネルでは新しいセッションを生成できる()
    {
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var sessions = new BattleSessionRepository(db);
        var firstId = await CreateAndJoinAsync(db, sessions, ids);

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockChannelAsync(ids.ChannelId);
            await sessions.FinishAsync(firstId, BattleEndReason.PlayerVictory);
            await scope.CommitAsync();
        }

        // 終了後は「同一セッションの延命」ではなく新規生成になる
        Assert.Null(await sessions.FindActiveAsync(ids.ChannelId));

        var secondId = await StartBattleAsync(ids);

        Assert.NotEqual(firstId, secondId);
    }

    // --- 通貨（BigInteger の読み書き） ---

    [DatabaseFact]
    public async Task 所持金額はSQL側の算術を経ずに加減算される()
    {
        // 10進整数文字列で格納されるため UPDATE ... SET amount = amount + X は使えない。
        // 読み出して BigInteger で計算し書き戻す
        var ids = BattleLockTests.NewIds();
        await using var db = await fixture.CreateContextAsync();
        await BattleLockTests.SeedAsync(db, ids);

        var players = new PlayerRepository(db);
        var huge = BigInteger.Pow(10, 80) + 12345;

        await using (var scope = await BattleLock.BeginAsync(db))
        {
            await scope.LockPlayerAsync(ids.UserId);
            await players.AddCurrencyAsync(ids.UserId, huge);
            await scope.CommitAsync();
        }

        await using var verifyDb = await fixture.CreateContextAsync();

        Assert.Equal(huge, await new PlayerRepository(verifyDb).GetCurrencyAsync(ids.UserId));
    }

    // --- ヘルパ ---

    private static async Task<Guid> CreateAndJoinAsync(
        ChidoDbContext db, BattleSessionRepository sessions, BattleLockTests.Ids ids)
    {
        await using var scope = await BattleLock.BeginAsync(db);
        await scope.LockPlayerAsync(ids.UserId);
        await scope.LockChannelAsync(ids.ChannelId);

        var session = await sessions.FindActiveAsync(ids.ChannelId)
                      ?? await sessions.CreateAsync(ids.GuildId, ids.ChannelId);

        await sessions.JoinPlayerAsync(session.SessionId, ids.UserId, displayOrder: 0);
        await scope.CommitAsync();

        return session.SessionId;
    }
}
