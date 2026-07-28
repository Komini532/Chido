using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Battle.Actions;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle;

/// <summary>
/// セッション終了トリガーと Escape の検証（戦闘システム 4.3・6.1）。
/// </summary>
public class SessionEndTests
{
    // --- 敵側の生存が0 ---

    [Fact]
    public void 全敵が撃破されるとPlayerVictoryになる()
    {
        var (session, _, enemies) = NewBattle(enemyCount: 2);

        enemies[0].MarkDefeated();
        Assert.False(session.CheckEndCondition().ended);

        enemies[1].MarkDefeated();

        Assert.Equal((true, BattleEndReason.PlayerVictory), session.CheckEndCondition());
    }

    [Fact]
    public void 最後の1体が逃走ならEnemyEscapedになる()
    {
        // 参加者の状態分布からは事後的に区別できないため、消失の順番を見る。
        // 終了理由は次に出現する敵の抽選を分岐させるので取り違えられない
        var (session, _, enemies) = NewBattle(enemyCount: 2);

        enemies[0].MarkDefeated();
        enemies[1].MarkEscaped();

        Assert.Equal((true, BattleEndReason.EnemyEscaped), session.CheckEndCondition());
    }

    [Fact]
    public void 逃走が先で撃破が後ならPlayerVictoryになる()
    {
        // 状態の組み合わせは前のテストと全く同じ（Escaped の敵行と Defeated の敵行が同居）。
        // 順番だけが両者を分ける
        var (session, _, enemies) = NewBattle(enemyCount: 2);

        enemies[0].MarkEscaped();
        enemies[1].MarkDefeated();

        Assert.Equal((true, BattleEndReason.PlayerVictory), session.CheckEndCondition());
    }

    [Fact]
    public void 単独の敵が逃走してもEnemyEscapedになる()
    {
        // target_rule = 敵 の離脱モーションで敵を追い払う経路。撃破報酬も累積敵レベルの上昇も無いため、
        // 「勝てない敵を追い払って引き直す」が既存仕様の合成だけで成立する
        var (session, _, enemies) = NewBattle(enemyCount: 1);

        enemies[0].MarkEscaped();

        Assert.Equal((true, BattleEndReason.EnemyEscaped), session.CheckEndCondition());
    }

    // --- プレイヤー側の生存が0 ---

    [Fact]
    public void 全プレイヤーが離脱するとPlayerEscapedになる()
    {
        var (session, players, _) = NewBattle(playerCount: 2);

        players[0].MarkEscaped();
        Assert.False(session.CheckEndCondition().ended);

        players[1].MarkEscaped();

        Assert.Equal((true, BattleEndReason.PlayerEscaped), session.CheckEndCondition());
    }

    [Fact]
    public void 戦闘不能が混ざっている間はセッションが終了しない()
    {
        // 能動的な選択である Escape のみがトリガー。全参加者が戦闘不能でも終了しない
        var (session, players, _) = NewBattle(playerCount: 2);

        players[0].MarkEscaped();
        players[1].MarkDefeated();

        Assert.False(session.CheckEndCondition().ended);
    }

    [Fact]
    public void 全プレイヤーが戦闘不能でもセッションは終了しない()
    {
        // 非同期・飛び入り参加前提では参加者の総数が確定しないため、戦闘不能を含めると
        // 「最初の1人が参加してそのまま戦闘不能になっただけで終了する」ことになる
        var (session, players, _) = NewBattle(playerCount: 2);

        foreach (var player in players) player.MarkDefeated();

        Assert.False(session.CheckEndCondition().ended);
    }

    [Fact]
    public void 戦闘不能のプレイヤーが改めて離脱すればセッションが終了する()
    {
        var (session, players, _) = NewBattle(playerCount: 2);

        players[0].MarkDefeated();
        players[1].MarkEscaped();
        Assert.False(session.CheckEndCondition().ended);

        players[0].MarkEscaped();

        Assert.Equal((true, BattleEndReason.PlayerEscaped), session.CheckEndCondition());
    }

    // --- Escape コマンド ---

    [Fact]
    public async Task 戦闘不能からでも離脱できる()
    {
        // 単一セッション制約の拘束を自力で解く唯一の手段。塞ぐと Defeated のプレイヤーが
        // 誰かが敵を倒すかチャンネルが消えるまで他の戦闘に参加できなくなる
        var (session, players, _) = NewBattle();
        players[0].MarkDefeated();

        var result = await new EscapeAction().ExecuteAsync(
            players[0], session.Participants, session, new Random(1));

        Assert.Equal(ParticipantStatus.Escaped, players[0].Status);
        Assert.True(result.SessionEnded);
        Assert.Equal(BattleEndReason.PlayerEscaped, result.EndReason);
    }

    [Fact]
    public async Task 離脱後は同じ戦闘に再参加できない()
    {
        var (session, players, _) = NewBattle(playerCount: 2);
        players[0].MarkEscaped();

        var result = await new EscapeAction().ExecuteAsync(
            players[0], session.Participants, session, new Random(1));

        Assert.False(result.SessionEnded);
        Assert.Contains("既に戦闘から離脱している", result.LogEntries[0]);
    }

    [Fact]
    public void 離脱は終端であり戦闘不能へ戻らない()
    {
        var (_, players, _) = NewBattle();

        players[0].MarkEscaped();
        players[0].MarkDefeated();

        Assert.Equal(ParticipantStatus.Escaped, players[0].Status);
    }

    [Fact]
    public void 消失順は最初にActiveでなくなった時点で確定する()
    {
        // 既に生存から外れた参加者の位置は、後から状態が変わっても動かない
        var (session, players, enemies) = NewBattle(enemyCount: 2);

        players[0].MarkDefeated();
        var orderAtDefeat = players[0].DeactivationOrder;

        enemies[0].MarkDefeated();
        players[0].MarkEscaped();

        Assert.Equal(orderAtDefeat, players[0].DeactivationOrder);
        Assert.True(enemies[0].DeactivationOrder > orderAtDefeat);
        Assert.Null(enemies[1].DeactivationOrder);
    }

    [Fact]
    public void チャンネル消失は状態に依らず外から与えられる()
    {
        // 戦闘の場そのものが消えたという、参加者の状態には現れない事象。
        // 時間経過による強制終了ではなくチャンネルの存否そのものが終了条件になっている
        var (session, _, _) = NewBattle();

        Assert.False(session.CheckEndCondition().ended);

        session.Finish(BattleEndReason.ChannelMissing);

        Assert.True(session.IsFinished);
        Assert.Equal(BattleEndReason.ChannelMissing, session.EndReason);
    }

    // --- ヘルパ ---

    private static (BattleSession Session, List<BattleParticipant> Players, List<BattleParticipant> Enemies)
        NewBattle(int playerCount = 1, int enemyCount = 1)
    {
        var session = new BattleSession(guildId: 1, channelId: 1);

        var players = Enumerable.Range(0, playerCount).Select(i =>
        {
            var p = new Player(userId: (ulong)(i + 1), name: $"P{i}", exp: 10000);
            p.RestoreToFull();
            return new BattleParticipant(
                p, EntityType.Player, discordUserId: (ulong)(i + 1), displayOrder: (ushort)i);
        }).ToList();

        var enemies = Enumerable.Range(0, enemyCount).Select(i =>
        {
            var e = new Enemy(
                masterKey: "test", name: $"E{i}", level: 100, shape: StatShape.Player,
                strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500);
            e.RestoreToFull();
            return new BattleParticipant(
                e, EntityType.Enemy, enemyId: Guid.NewGuid(), displayOrder: (ushort)i);
        }).ToList();

        foreach (var p in players) session.AddParticipant(p);
        foreach (var e in enemies) session.AddParticipant(e);

        return (session, players, enemies);
    }
}
