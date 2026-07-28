using System.Numerics;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle;

/// <summary>
/// 行動順とターン骨格の検証（戦闘システム 4.1・4.2・3.3）。
/// </summary>
public class TurnResolverTests
{
    private static Player NewPlayer(string name, int level = 100)
    {
        var p = new Player(userId: 1, name: name, exp: new BigInteger(level) * level);
        p.RestoreToFull();
        return p;
    }

    private static Enemy NewEnemy(string name, int level = 100, int speed = 500)
    {
        var e = new Enemy(
            masterKey: "test", name: name, level: level, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: speed);
        e.RestoreToFull();
        return e;
    }

    private static Skill AttackSkill(int priority = 0, int power = GameConstants.PowerScale) =>
        new(GameConstants.AttackSkillKey, "通常攻撃",
            [new AttackMotion(0, TargetRule.Enemy, Ratio.Full, AttackType.Physical, power)],
            priority: priority);

    /// <summary>反撃モーションを持たない、自分自身のみに作用するスキル。</summary>
    private static Skill SelfOnlySkill(int priority = 0) =>
        new("self_only", "自己強化",
            [new HealMotion(0, TargetRule.Myself, Ratio.Full, AttackType.Physical, 1)],
            priority: priority);

    /// <summary>テスト間で状態を持ち越さないよう、呼び出しごとに新しい乱数源を作る。</summary>
    private static Random Deterministic => new(20260728);

    // --- 行動順 ---

    [Fact]
    public void Priorityが高い側が先攻する()
    {
        var slow = new TurnSide(NewParticipant(NewEnemy("鈍足", speed: 1), EntityType.Enemy), AttackSkill(priority: 5));
        var fast = new TurnSide(NewParticipant(NewPlayer("俊足"), EntityType.Player), AttackSkill(priority: 0));

        var (first, _) = TurnOrder.Decide(fast, slow, Deterministic);

        // Speed で劣っていても Priority が上なら先攻。Defend が鈍足でも被弾前に構えを取れる根拠
        Assert.Same(slow, first);
    }

    [Fact]
    public void Priorityが同値ならSpeedの高い側が先攻する()
    {
        var fast = new TurnSide(NewParticipant(NewEnemy("俊足", speed: 900), EntityType.Enemy), AttackSkill());
        var slow = new TurnSide(NewParticipant(NewPlayer("鈍足"), EntityType.Player), AttackSkill());

        var (first, second) = TurnOrder.Decide(slow, fast, Deterministic);

        Assert.Same(fast, first);
        Assert.Same(slow, second);
    }

    [Fact]
    public void PriorityもSpeedも同値なら乱数で決まる()
    {
        var a = new TurnSide(NewParticipant(NewPlayer("A"), EntityType.Player), AttackSkill());
        var b = new TurnSide(NewParticipant(NewEnemy("B"), EntityType.Enemy), AttackSkill());

        // 同じシードなら常に同じ結果になる（再現性）
        var first1 = TurnOrder.Decide(a, b, new Random(1)).First;
        var first2 = TurnOrder.Decide(a, b, new Random(1)).First;
        Assert.Same(first1, first2);

        // 両方の側が先攻になりうる
        var winners = Enumerable.Range(0, 50)
            .Select(seed => TurnOrder.Decide(a, b, new Random(seed)).First)
            .Distinct()
            .Count();
        Assert.Equal(2, winners);
    }

    [Fact]
    public void PriorityかSpeedで決着すれば乱数を消費しない()
    {
        // 「そのターンにつき1回だけ引く」乱数は、タイブレークが必要なときにのみ引かれる。
        // 不要な消費があると、同一シードでの再現性が呼び出し経路に依存してしまう
        var fast = new TurnSide(NewParticipant(NewEnemy("俊足", speed: 900), EntityType.Enemy), AttackSkill());
        var slow = new TurnSide(NewParticipant(NewPlayer("鈍足"), EntityType.Player), AttackSkill());

        var rng = new Random(42);
        TurnOrder.Decide(slow, fast, rng);
        var afterDecide = rng.Next();

        Assert.Equal(new Random(42).Next(), afterDecide);
    }

    // --- 先攻撃破による後攻キャンセル ---

    [Fact]
    public void 先攻が後攻を倒すと後攻の行動はキャンセルされる()
    {
        // 乱数タイブレークに落ちないよう Speed で先攻を確定させる
        var (session, player, enemy) = NewBattle(playerLevel: 10000, enemyLevel: 1, enemySpeed: 1);
        var fullLife = player.Entity.CurrentLife;

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.Equal(ParticipantStatus.Defeated, enemy.Status);
        Assert.True(result.SecondCancelled);
        // ダメージ計算自体が行われないため、行動者は無傷のまま
        Assert.Equal(fullLife, player.Entity.CurrentLife);
    }

    [Fact]
    public void 倒しきれなければ反撃を受ける()
    {
        var (session, player, enemy) = NewBattle();
        var fullLife = player.Entity.CurrentLife;

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.False(result.SecondCancelled);
        Assert.True(player.Entity.CurrentLife < fullLife);
        Assert.True(enemy.Entity.CurrentLife < enemy.Entity.MaxLife);
    }

    [Fact]
    public void ログは実行順と一致する()
    {
        // 行動順を決める抽選とログの並び順を別々に引くとログが処理順とずれるため、
        // 同一の決定を両方に使う
        var (session, player, _) = NewBattle();

        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, AttackSkill(), session, Deterministic, _ => AttackSkill());

        Assert.StartsWith(result.First.Participant.Entity.Name, result.Logs[0]);
    }

    // --- CurrentTarget の更新タイミング ---

    [Fact]
    public void 敵の指定は反撃者確定より前に反映される()
    {
        // 更新を反撃者確定より後に置くと、指定した敵は次ターンからしか反撃者にならず
        // 「どの敵から反撃を受けるかを決める」という用法が失われる
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player, displayOrder: 0);
        var front = NewParticipant(NewEnemy("前列"), EntityType.Enemy, displayOrder: 0);
        var back = NewParticipant(NewEnemy("後列"), EntityType.Enemy, displayOrder: 1);
        session.AddParticipant(player);
        session.AddParticipant(front);
        session.AddParticipant(back);

        // 自分自身にしか作用しないスキルでも、敵の [対象] 指定は反撃者の決定として意味を持つ
        var result = new TurnResolver(new SkillPlayer()).Resolve(
            player, SelfOnlySkill(), session, Deterministic, _ => AttackSkill(), commandTarget: back);

        Assert.Equal(back.Entity.Id, player.CurrentTargetId);
        Assert.True(back.Entity.CurrentLife == back.Entity.MaxLife); // 攻撃はしていない
        Assert.True(player.Entity.CurrentLife < player.Entity.MaxLife); // 後列から反撃を受けた
        Assert.Same(back, result.First.Participant == player ? result.Second.Participant : result.First.Participant);
    }

    // --- 対象解決（ResolveTarget） ---

    [Fact]
    public void 格納値が使えなければ表示順が最小の敵へ落ちて書き戻される()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player);
        var front = NewParticipant(NewEnemy("前列"), EntityType.Enemy, displayOrder: 0);
        var back = NewParticipant(NewEnemy("後列"), EntityType.Enemy, displayOrder: 1);
        // 追加順は表示順と一致させない。順序の根拠が DisplayOrder だけであることを見る
        session.AddParticipant(player);
        session.AddParticipant(back);
        session.AddParticipant(front);

        Assert.Null(player.CurrentTargetId);

        var resolved = session.ResolveTarget(player);

        Assert.Same(front, resolved);
        Assert.Equal(front.Entity.Id, player.CurrentTargetId); // 書き戻される
    }

    [Fact]
    public void 対象が戦闘不能になると次点へ自動再選定される()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player);
        var front = NewParticipant(NewEnemy("前列"), EntityType.Enemy, displayOrder: 0);
        var back = NewParticipant(NewEnemy("後列"), EntityType.Enemy, displayOrder: 1);
        session.AddParticipant(player);
        session.AddParticipant(front);
        session.AddParticipant(back);

        session.ResolveTarget(player);
        front.MarkDefeated();

        Assert.Same(back, session.ResolveTarget(player));
        Assert.Equal(back.Entity.Id, player.CurrentTargetId);
    }

    [Fact]
    public void 明示指定した対象は変更するまで保持される()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー"), EntityType.Player);
        var front = NewParticipant(NewEnemy("前列"), EntityType.Enemy, displayOrder: 0);
        var back = NewParticipant(NewEnemy("後列"), EntityType.Enemy, displayOrder: 1);
        session.AddParticipant(player);
        session.AddParticipant(front);
        session.AddParticipant(back);

        player.SetTarget(back.Entity.Id);

        Assert.Same(back, session.ResolveTarget(player));
        Assert.Same(back, session.ResolveTarget(player));
    }

    [Fact]
    public void Activeな敵がいない状態での対象解決は例外になる()
    {
        // セッション終了トリガーにより構造的に起こらない。到達したらフォールバックせず
        // 実装の不具合として投げる（無言で握り潰すと終了処理の漏れが検出できなくなる）
        var (session, player, enemy) = NewBattle();
        enemy.MarkDefeated();

        Assert.Throws<InvalidOperationException>(() => session.ResolveTarget(player));
    }

    // --- [対象] の受理ゲートと空振り通知 ---

    [Fact]
    public void 戦闘不能の対象を指定すると行動が不成立になる()
    {
        var (_, _, enemy) = NewBattle();
        enemy.MarkDefeated();

        var message = TurnResolver.ValidateCommandTarget(enemy);

        Assert.NotNull(message);
        Assert.Contains("既にやられています", message);
    }

    [Fact]
    public void 蘇生モーションを含むスキルなら戦闘不能の対象も受理される()
    {
        // 可否の切り替え点を1箇所に集約してある（現行はその条件が常に偽）
        var (_, _, enemy) = NewBattle();
        enemy.MarkDefeated();

        Assert.Null(TurnResolver.ValidateCommandTarget(enemy, allowsDefeatedTarget: true));
    }

    [Fact]
    public void 離脱済みの対象を指定すると解決不能として拒否される()
    {
        var (_, _, enemy) = NewBattle();
        enemy.MarkEscaped();

        var message = TurnResolver.ValidateCommandTarget(enemy);

        Assert.NotNull(message);
        Assert.Contains("離脱しています", message);
    }

    [Fact]
    public void 味方対象モーションを持たないスキルへの味方指定は空振りを通知する()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player, displayOrder: 0);
        var ally = NewParticipant(NewPlayer("味方"), EntityType.Player, displayOrder: 1);
        session.AddParticipant(actor);
        session.AddParticipant(ally);

        var message = TurnResolver.DetectAllyTargetMiss(actor, AttackSkill(), ally);

        Assert.NotNull(message);
        Assert.Contains("影響はありませんでした", message);
    }

    [Fact]
    public void 味方対象モーションを持つスキルなら空振りにならない()
    {
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player, displayOrder: 0);
        var ally = NewParticipant(NewPlayer("味方"), EntityType.Player, displayOrder: 1);

        var healOthers = new Skill("heal", "回復",
            [new HealMotion(0, TargetRule.Ally, Ratio.Full, AttackType.Physical, GameConstants.PowerScale)]);

        Assert.Null(TurnResolver.DetectAllyTargetMiss(actor, healOthers, ally));
    }

    [Fact]
    public void 自分自身への指定は空振りとして通知しない()
    {
        // 自分自身対象のモーションが実際に作用しているため、事実に反する通知を避ける
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);

        Assert.Null(TurnResolver.DetectAllyTargetMiss(actor, SelfOnlySkill(), actor));
    }

    [Fact]
    public void 敵の指定はスキル構成を問わず空振りにならない()
    {
        // 敵の指定は反撃者の決定という全スキル共通の経路を通るため、常に意味を持つ
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        Assert.Null(TurnResolver.DetectAllyTargetMiss(actor, SelfOnlySkill(), enemy));
    }

    // --- 対象解決規則 ---

    [Fact]
    public void 自分自身の規則は対象指定を無視する()
    {
        // 「自分を強化」のように味方に向けられては困る効果のための、味方よりも強い規則
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var ally = NewParticipant(NewPlayer("味方"), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        var motion = new HealMotion(0, TargetRule.Myself, Ratio.Full, AttackType.Physical, 100);

        Assert.Same(actor, TargetResolver.Resolve(motion, actor, enemy, commandTarget: ally));
    }

    [Fact]
    public void 味方の規則は対象省略時に行動者自身へ解決する()
    {
        // 「味方」は自分自身を含む。同一のスキルエントリが自己回復と味方回復の双方として機能する
        var actor = NewParticipant(NewPlayer("行動者"), EntityType.Player);
        var ally = NewParticipant(NewPlayer("味方"), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵"), EntityType.Enemy);

        var motion = new HealMotion(0, TargetRule.Ally, Ratio.Full, AttackType.Physical, 100);

        Assert.Same(actor, TargetResolver.Resolve(motion, actor, enemy));
        Assert.Same(ally, TargetResolver.Resolve(motion, actor, enemy, commandTarget: ally));
    }

    // --- ヘルパ ---

    private static BattleParticipant NewParticipant(
        EntityBase entity, EntityType type, ushort displayOrder = 0) =>
        new(entity, type,
            discordUserId: type == EntityType.Player ? 1UL : null,
            enemyId: type == EntityType.Enemy ? Guid.NewGuid() : null,
            displayOrder: displayOrder);

    private static (BattleSession Session, BattleParticipant Player, BattleParticipant Enemy) NewBattle(
        int playerLevel = 100, int enemyLevel = 100, int enemySpeed = 500)
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewParticipant(NewPlayer("プレイヤー", playerLevel), EntityType.Player);
        var enemy = NewParticipant(NewEnemy("敵", enemyLevel, enemySpeed), EntityType.Enemy);
        session.AddParticipant(player);
        session.AddParticipant(enemy);
        return (session, player, enemy);
    }
}
