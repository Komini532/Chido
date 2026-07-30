using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Progression;
using Chido.Core.Rewards;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Rewards;

/// <summary>
/// 撃破報酬の検証（戦闘システム 6.2・10.2）。
/// </summary>
public class RewardTests
{
    /// <summary>基礎経験値を 10000 に揃えるための組（レベル 10000・exp_rate 100%）。</summary>
    private const int BaseExp = 10000;

    private static Random Deterministic => new(20260730);

    // --- 按分率の較正 ---

    // 設計 6.2 の較正表は小数第1位までの表示であり、1% の行の「0.2%」は 0.25% を、
    // 5% の行の「6.2%」は 6.25% を切り捨てて表示したもの。ここでは厳密値で固定する
    [Theory]
    [InlineData(1, 25)]     // 貢献率 1%   → 0.25%（表示は 0.2%）
    [InlineData(4, 400)]    // 貢献率 4%   → 4.0%（損益分岐点）
    [InlineData(5, 625)]    // 貢献率 5%   → 6.25%（表示は 6.2%）
    [InlineData(10, 2500)]  // 貢献率 10%  → 25.0%（10人均等）
    [InlineData(20, 10000)] // 貢献率 20%  → 100%（頭打ち）
    [InlineData(50, 10000)] // 20%超も満額
    public void 按分率が設計の較正表と一致する(int contributionPercent, int expected)
    {
        // 分母 10000・基礎経験値 10000 に揃えることで、獲得値がそのまま按分率(permyriad)になる
        var own = new BigInteger(contributionPercent) * 100;

        var gain = Apportionment.Apportion(
            fullNumerator: BaseExp, fullDenominator: 1, own: own, denominator: 10000);

        Assert.Equal(expected, gain);
    }

    [Fact]
    public void 貢献率4パーセントがソロと同じ効率になる()
    {
        // 損益分岐点。s/c = 25c = 1 となる点であり、ここを境に集団戦がソロより有利／不利になる
        var solo = Apportionment.Apportion(BaseExp, 1, own: 10000, denominator: 10000);
        var quarter = Apportionment.Apportion(BaseExp, 1, own: 400, denominator: 10000);

        // 4% の貢献で 4% の取り分＝ダメージあたりの効率がソロと等しい
        Assert.Equal(solo * 4 / 100, quarter);
    }

    [Fact]
    public void 参加者5人までは全員が満額になる()
    {
        // 各自20%で頭打ちに到達する。6人以上から希釈が始まる、という明快な線引き
        var five = Apportionment.Apportion(BaseExp, 1, own: 2000, denominator: 10000);
        var six = Apportionment.Apportion(BaseExp, 1, own: 10000 / 6, denominator: 10000);

        Assert.Equal(BaseExp, five);
        Assert.True(six < BaseExp);
    }

    [Fact]
    public void 丸めは最後に1回だけ行われる()
    {
        // 満額の式で先に丸めてから按分すると、丸め誤差が持ち越されて按分率がずれる。
        // 基礎経験値が permyriad で割り切れない値でも、単一の floor で計算される
        var gain = Apportionment.Apportion(
            fullNumerator: new BigInteger(3) * 9999, // E × ΣexpRate
            fullDenominator: Ratio.Full.Permyriad,
            own: 1000,
            denominator: 10000);

        // 3 × 9999 × 25 × 1000² ÷ (10000 × 10000²) = 749925000000 ÷ 1000000000000 = 0
        Assert.Equal(BigInteger.Zero, gain);
    }

    // --- 分母 ---

    [Fact]
    public void 分母は総ダメージと出現時最大HPの大きい方になる()
    {
        // 通常戦闘では sumDmg ≥ sumHp が常に成立し、下限は発火しない
        Assert.Equal(1200, Apportionment.Denominator(sumDamage: 1200, spawnMaxLifeSum: 1200));
        Assert.Equal(1500, Apportionment.Denominator(sumDamage: 1500, spawnMaxLifeSum: 1200));

        // プレイヤー以外の要因でHPが減った場合にのみ下限が働く
        Assert.Equal(1200, Apportionment.Denominator(sumDamage: 600, spawnMaxLifeSum: 1200));
    }

    [Fact]
    public void 自滅した敵では実際にやった仕事の割合が反映される()
    {
        // 「6行動で自滅する敵」に1ダメージだけ入れて放置しても100%にならない。
        // t² の抑止曲線は c を入力にしているためこの経路には無力であり、分母の下限が塞ぐ
        var denominator = Apportionment.Denominator(sumDamage: 1, spawnMaxLifeSum: 1200);

        Assert.Equal(BigInteger.Zero, Apportionment.Apportion(BaseExp, 1, own: 1, denominator: denominator));

        // 半分削ったなら取り分は 0.5 相当（25 × 0.5² = 6.25倍… ではなく頭打ちで満額）
        Assert.Equal(BaseExp, Apportionment.Apportion(BaseExp, 1, own: 600, denominator: denominator));
    }

    [Fact]
    public void ゼロ除算は構造的に発生しない()
    {
        // sumHp > 0 が常に成立するため、分母の定義が例外を弾く分岐ではなく定義として吸収する
        Assert.Equal(BigInteger.Zero, Apportionment.Apportion(BaseExp, 1, own: 0, denominator: 0));
    }

    // --- オーバーキル ---

    [Fact]
    public void オーバーキルが他人の取り分を壊さない()
    {
        // 台帳は実効与ダメージ（実際に減った現在HP）を積む。生ダメージで積むと、
        // 残りHP1の敵に10000を叩き込んだ側が分母を独占し、99%の仕事をした側の取り分が消える
        var context = NewContext(
            spawnMaxLifeSum: 100,
            players:
            [
                new PlayerContribution(1, TotalDamageDealt: 99, Ratio.Zero, Escaped: false),
                new PlayerContribution(2, TotalDamageDealt: 1, Ratio.Zero, Escaped: false),
            ]);

        var rewards = RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic);

        var worker = rewards.Single(r => r.UserId == 1);
        var finisher = rewards.Single(r => r.UserId == 2);

        // 99% の仕事をした側が満額、1% の側は 0.25%
        Assert.Equal(BaseExp, worker.Exp);
        Assert.Equal(BaseExp * 25 / 10000, finisher.Exp);
    }

    // --- 報酬ゲート ---

    [Fact]
    public void 撃破以外の終了理由では誰も報酬を得ない()
    {
        // 敵に十分なダメージを与えていた場合であっても EnemyEscaped では報酬は発生しない
        var context = NewContext(100, [new PlayerContribution(1, 100, Ratio.Zero, false)]);

        Assert.Empty(RewardCalculator.Calculate(BattleEndReason.EnemyEscaped, context, Deterministic));
        Assert.Empty(RewardCalculator.Calculate(BattleEndReason.PlayerEscaped, context, Deterministic));
        Assert.Empty(RewardCalculator.Calculate(BattleEndReason.ChannelMissing, context, Deterministic));
    }

    [Fact]
    public void 与ダメージが0のプレイヤーは対象外になる()
    {
        // 行動実績ではなく結果（台帳に1以上積まれたか）を基準にする
        var context = NewContext(100,
        [
            new PlayerContribution(1, 100, Ratio.Zero, false),
            new PlayerContribution(2, 0, Ratio.Zero, false),
        ]);

        var rewards = RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic);

        Assert.Equal(1UL, Assert.Single(rewards).UserId);
    }

    [Fact]
    public void 離脱者は対象外だが与ダメージは分母に残る()
    {
        // 除くと「主力が敵を削りきってから /escape すれば残った参加者の貢献率が跳ね上がる」
        // という悪用経路が生まれる。分母は「この敵を倒すために費やされた仕事の総量」であるべき
        var context = NewContext(100,
        [
            new PlayerContribution(1, 90, Ratio.Zero, Escaped: true),
            new PlayerContribution(2, 10, Ratio.Zero, Escaped: false),
        ]);

        var rewards = RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic);
        var remaining = Assert.Single(rewards);

        Assert.Equal(2UL, remaining.UserId);
        // 分母は 100 のまま。離脱者を除いて 10/10 = 満額にはならない
        Assert.Equal(BaseExp * 25 * 10 * 10 / (100 * 100), remaining.Exp);
    }

    [Fact]
    public void 戦闘不能はそれ自体では対象外の理由にならない()
    {
        var context = NewContext(100, [new PlayerContribution(1, 50, Ratio.Zero, Escaped: false)]);

        Assert.Single(RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic));
    }

    // --- 通貨 ---

    [Fact]
    public void 通貨は経験値と同じ按分率で配られる()
    {
        var context = NewContext(100,
        [
            new PlayerContribution(1, 100, Ratio.Zero, false),
            new PlayerContribution(2, 10, Ratio.Zero, false),
        ],
        currencyDropTotal: 1000);

        var rewards = RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic);

        // 分母は sumDmg = 110。100/110 は 20% を超えるため満額
        Assert.Equal(1000, rewards.Single(r => r.UserId == 1).Currency);
        // 10/110 ≈ 9.09% → 25 × (10/110)² ≈ 20.66% → floor(1000 × 25 × 100 ÷ 12100) = 206
        Assert.Equal(1000 * 25 * 10 * 10 / (110 * 110), rewards.Single(r => r.UserId == 2).Currency);
    }

    // --- ドロップと Luck ---

    [Fact]
    public void ドロップ率100パーセントは常に落ちる()
    {
        var context = NewContext(100, [new PlayerContribution(1, 100, Ratio.Zero, false)],
            loots: [new LootOption("herb", 2, Ratio.Full)]);

        var reward = Assert.Single(RewardCalculator.Calculate(
            BattleEndReason.PlayerVictory, context, Deterministic));

        var drop = Assert.Single(reward.Items);
        Assert.Equal("herb", drop.ItemKey);
        Assert.Equal(2, drop.Quantity);
    }

    [Fact]
    public void Luckが0なら再抽選は起こらない()
    {
        // 装備なしの初期状態。通常の drop_rate のみでドロップが決まる
        var hits = Enumerable.Range(0, 2000)
            .Count(seed => DropRoll.Roll(Ratio.FromPercent(10m), Ratio.Zero, new Random(seed)));

        Assert.InRange(hits / 2000.0, 0.08, 0.12);
    }

    [Fact]
    public void Luckは外れた場合にのみ再抽選の権利を与える()
    {
        // drop_rate 10% / Luck 100% なら 10% + 90% × 10% = 19%
        var hits = Enumerable.Range(0, 4000)
            .Count(seed => DropRoll.Roll(Ratio.FromPercent(10m), Ratio.Full, new Random(seed)));

        Assert.InRange(hits / 4000.0, 0.17, 0.21);
    }

    [Fact]
    public void 再抽選は1度だけ行われる()
    {
        // 2度以上引けるなら 10% + 90%×10% + ... と積み上がる。19% を明確に超えないことで見る
        var hits = Enumerable.Range(0, 4000)
            .Count(seed => DropRoll.Roll(Ratio.FromPercent(10m), Ratio.Full, new Random(seed)));

        Assert.True(hits / 4000.0 < 0.25);
    }

    [Theory]
    [InlineData(-50, 0)]   // 負値は0%へ
    [InlineData(0, 0)]
    [InlineData(50, 5000)]
    [InlineData(150, 10000)] // 100%超は100%へ
    public void Luckは使用時点でクランプされる(int percent, int expectedPermyriad)
    {
        // ステータスとしての Luck はクランプせず、定義域を強制するのは使用時点のみ
        Assert.Equal(expectedPermyriad, DropRoll.ClampLuck(Ratio.FromPercent(percent)).Permyriad);
    }

    [Fact]
    public void 負のLuckは1段目のドロップ率に干渉しない()
    {
        // 干渉させると Luck が実質的に2つの意味を持ち、装備の説明が破綻する
        var withNegative = Enumerable.Range(0, 2000)
            .Count(seed => DropRoll.Roll(Ratio.FromPercent(30m), Ratio.FromPercent(-80m), new Random(seed)));
        var withZero = Enumerable.Range(0, 2000)
            .Count(seed => DropRoll.Roll(Ratio.FromPercent(30m), Ratio.Zero, new Random(seed)));

        Assert.Equal(withZero, withNegative);
    }

    [Fact]
    public void ドロップ判定はプレイヤーごとに独立して行われる()
    {
        // 同一の敵から複数プレイヤーが同じアイテムを受け取りうる
        var context = NewContext(100,
        [
            new PlayerContribution(1, 50, Ratio.Zero, false),
            new PlayerContribution(2, 50, Ratio.Zero, false),
        ],
        loots: [new LootOption("herb", 1, Ratio.Full)]);

        var rewards = RewardCalculator.Calculate(BattleEndReason.PlayerVictory, context, Deterministic);

        Assert.All(rewards, r => Assert.Single(r.Items));
    }

    [Fact]
    public void 装備のドロップは出現時に確定した装備のみを対象にする()
    {
        var context = NewContext(100, [new PlayerContribution(1, 100, Ratio.Zero, false)],
            equipmentDrops: [new EquipmentDropOption("rusty_sword", Ratio.Full)]);

        var reward = Assert.Single(RewardCalculator.Calculate(
            BattleEndReason.PlayerVictory, context, Deterministic));

        Assert.Equal("rusty_sword", Assert.Single(reward.Equipment));
    }

    // --- 台帳 ---

    [Fact]
    public void 台帳はプレイヤーから敵への実効ダメージのみを積む()
    {
        var (session, player, enemy) = NewBattle();

        session.RecordDamageDealt(player.Entity.Id, enemy, 100);

        Assert.Equal(100, player.TotalDamageDealt);
    }

    [Fact]
    public void 味方への誤爆は台帳に積まれない()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var attacker = NewPlayerParticipant("A", 1);
        var ally = NewPlayerParticipant("B", 2);
        session.AddParticipant(attacker);
        session.AddParticipant(ally);

        session.RecordDamageDealt(attacker.Entity.Id, ally, 100);

        Assert.Equal(BigInteger.Zero, attacker.TotalDamageDealt);
    }

    [Fact]
    public void 敵の自滅ダメージは台帳に積まれない()
    {
        // プレイヤーの仕事量ではないため分母に入れてはならない
        var (session, _, enemy) = NewBattle();

        session.RecordDamageDealt(enemy.Entity.Id, enemy, 100);

        Assert.All(session.Participants, p => Assert.Equal(BigInteger.Zero, p.TotalDamageDealt));
    }

    [Fact]
    public void 実効0は台帳に寄与しない()
    {
        var (session, player, enemy) = NewBattle();

        session.RecordDamageDealt(player.Entity.Id, enemy, 0);

        Assert.Equal(BigInteger.Zero, player.TotalDamageDealt);
    }

    // --- 称号 ---

    [Fact]
    public void 称号は条件を満たした時点で獲得される()
    {
        var titles = new[]
        {
            new TitleCondition("collector", TitleAcquisitionType.ItemObtained, "herb", null),
            new TitleCondition("slayer", TitleAcquisitionType.EnemyDefeated, "slime", null),
            new TitleCondition("veteran", TitleAcquisitionType.LevelReached, null, 100),
            new TitleCondition("rich", TitleAcquisitionType.CurrencyReached, null, 1000),
        };

        var progress = new TitleProgress(
            AcquiredItemKeys: new HashSet<string> { "herb" },
            DefeatedEnemyKeys: new HashSet<string> { "slime" },
            Level: 100,
            Currency: 999);

        var earned = TitleEvaluator.Evaluate(titles, new HashSet<string>(), progress);

        Assert.Equal(["collector", "slayer", "veteran"], earned);
    }

    [Fact]
    public void 獲得済みの称号は再判定されない()
    {
        var titles = new[]
        {
            new TitleCondition("veteran", TitleAcquisitionType.LevelReached, null, 100),
        };

        var progress = new TitleProgress(
            new HashSet<string>(), new HashSet<string>(), Level: 200, Currency: 0);

        Assert.Empty(TitleEvaluator.Evaluate(titles, new HashSet<string> { "veteran" }, progress));
    }

    [Fact]
    public void 閾値は到達で満たされる()
    {
        var titles = new[]
        {
            new TitleCondition("rich", TitleAcquisitionType.CurrencyReached, null, 1000),
        };

        var empty = new HashSet<string>();

        Assert.Empty(TitleEvaluator.Evaluate(
            titles, empty, new TitleProgress(empty, empty, 0, 999)));
        Assert.Single(TitleEvaluator.Evaluate(
            titles, empty, new TitleProgress(empty, empty, 0, 1000)));
    }

    // --- ヘルパ ---

    private static RewardContext NewContext(
        BigInteger spawnMaxLifeSum,
        IReadOnlyList<PlayerContribution> players,
        BigInteger? currencyDropTotal = null,
        IReadOnlyList<LootOption>? loots = null,
        IReadOnlyList<EquipmentDropOption>? equipmentDrops = null)
        => new(
            players,
            EnemyLevel: BaseExp,
            ExpRateSum: Ratio.Full.Permyriad,
            SpawnMaxLifeSum: spawnMaxLifeSum,
            CurrencyDropTotal: currencyDropTotal ?? BigInteger.Zero,
            Loots: loots ?? [],
            EquipmentDrops: equipmentDrops ?? []);

    private static BattleParticipant NewPlayerParticipant(string name, ulong userId)
    {
        var player = new Player(userId, name, exp: 10000);
        player.RestoreToFull();
        return new BattleParticipant(player, EntityType.Player, discordUserId: userId);
    }

    private static (BattleSession Session, BattleParticipant Player, BattleParticipant Enemy) NewBattle()
    {
        var session = new BattleSession(guildId: 1, channelId: 1);
        var player = NewPlayerParticipant("プレイヤー", 1);

        var enemyEntity = new Enemy(
            masterKey: "test", name: "敵", level: 100, shape: StatShape.Player,
            strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500);
        enemyEntity.RestoreToFull();
        var enemy = new BattleParticipant(enemyEntity, EntityType.Enemy, enemyId: Guid.NewGuid());

        session.AddParticipant(player);
        session.AddParticipant(enemy);
        return (session, player, enemy);
    }
}
