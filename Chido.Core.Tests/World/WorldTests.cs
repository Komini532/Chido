using System.Numerics;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Stats;
using Chido.Core.World;
using Xunit;

namespace Chido.Core.Tests.World;

/// <summary>
/// 敵の出現・フィールド遷移・レベル進行の検証（戦闘システム 10.3・10.4・10.5）。
/// </summary>
public class WorldTests
{
    private const string Grassland = GameConstants.GrasslandFieldKey;
    private const string Cave = "cave";

    private static Random Deterministic => new(20260729);

    // --- 組の抽選（DrawGroup） ---

    [Fact]
    public void レアリティは重みに従って抽選される()
    {
        var catalog = new FakeCatalog()
            .WithRarities(Grassland, (Rarity.Common, 7000), (Rarity.Uncommon, 3000))
            .WithGroups(Grassland, Rarity.Common, "common_group")
            .WithGroups(Grassland, Rarity.Uncommon, "uncommon_group");

        var counts = Enumerable.Range(0, 4000)
            .Select(seed => GroupDraw.Draw(catalog, Grassland, new Random(seed)).Rarity)
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.InRange(counts[Rarity.Common] / 4000.0, 0.66, 0.74);
        Assert.InRange(counts[Rarity.Uncommon] / 4000.0, 0.26, 0.34);
    }

    [Fact]
    public void 重みの合計で正規化される()
    {
        // 合計が 10000 に満たなくても、余りの確率だけ「どれにも当たらない」区間が生まれてはならない。
        // 生まれると、その分だけ静かに縮退経路（草原フォールバック）へ落ちる
        var catalog = new FakeCatalog()
            .WithRarities(Grassland, (Rarity.Common, 1), (Rarity.Rare, 1))
            .WithGroups(Grassland, Rarity.Common, "c")
            .WithGroups(Grassland, Rarity.Rare, "r");

        var results = Enumerable.Range(0, 200)
            .Select(seed => GroupDraw.Draw(catalog, Grassland, new Random(seed)))
            .ToList();

        Assert.All(results, r => Assert.False(r.Degraded));
        Assert.Equal(2, results.Select(r => r.GroupKey).Distinct().Count());
    }

    [Fact]
    public void Hiddenは通常抽選に含まれない()
    {
        // イベント専用のレアリティ。重みテーブルに混入していても引かれてはならない
        var catalog = new FakeCatalog()
            .WithRarities(Grassland, (Rarity.Common, 1), (Rarity.Hidden, 9999))
            .WithGroups(Grassland, Rarity.Common, "c")
            .WithGroups(Grassland, Rarity.Hidden, "hidden_group");

        var rarities = Enumerable.Range(0, 100)
            .Select(seed => GroupDraw.Draw(catalog, Grassland, new Random(seed)).Rarity)
            .Distinct()
            .ToList();

        Assert.Equal([Rarity.Common], rarities);
    }

    [Fact]
    public void 候補が空なら草原のCommonへ縮退する()
    {
        // レアリティは保存しない。保存すると「フィールドAの Mythic 定義漏れが
        // 草原の Mythic 報酬を引き出す抜け道」になる
        var catalog = new FakeCatalog()
            .WithRarities(Cave, (Rarity.Mythic, 10000))
            .WithGroups(Grassland, Rarity.Common, "grassland_common");

        var result = GroupDraw.Draw(catalog, Cave, Deterministic);

        Assert.Equal("grassland_common", result.GroupKey);
        Assert.Equal(Rarity.Common, result.Rarity); // Mythic のまま持ち越さない
        Assert.True(result.Degraded);               // 縮退は必ず通知される
    }

    [Fact]
    public void 草原のCommonも空なら例外になる()
    {
        // 起動時検証がこの正常系を塞ぐ。到達するのはマスタが壊れているときだけであり、
        // 例外はチャンネル行ロック下のターン全体をロールバックさせる
        var catalog = new FakeCatalog().WithRarities(Cave, (Rarity.Mythic, 10000));

        Assert.Throws<InvalidOperationException>(() => GroupDraw.Draw(catalog, Cave, Deterministic));
    }

    [Fact]
    public void 固定レアリティ指定は1段目の抽選を飛ばす()
    {
        var catalog = new FakeCatalog()
            .WithRarities(Grassland, (Rarity.Mythic, 10000))
            .WithGroups(Grassland, Rarity.Common, "c")
            .WithGroups(Grassland, Rarity.Mythic, "m");

        var result = GroupDraw.Draw(catalog, Grassland, Deterministic, forcedRarity: Rarity.Common);

        Assert.Equal("c", result.GroupKey);
    }

    // --- フィールド遷移（NextField） ---

    [Fact]
    public void 遷移先は候補から完全ランダムに選ばれる()
    {
        var catalog = new FakeCatalog().WithTransitions(Grassland, Cave, "forest", "desert");

        var picked = Enumerable.Range(0, 100)
            .Select(seed => FieldTransition.Next(catalog, Grassland, new Random(seed)).FieldKey)
            .Distinct()
            .ToList();

        Assert.Equal(3, picked.Count);
    }

    [Fact]
    public void 遷移先候補が0件なら草原へ縮退する()
    {
        // 現在フィールドに留まる案は採らない。次の判定機会も2500レベル後で同じく0件になり、
        // フィールドシステムが恒久的に、しかも無言で停止するため
        var result = FieldTransition.Next(new FakeCatalog(), Cave, Deterministic);

        Assert.Equal(Grassland, result.FieldKey);
        Assert.True(result.Degraded);
    }

    [Fact]
    public void 自己ループは縮退ではなく意図として扱われる()
    {
        // (草原, 草原) の行があれば「そこから動かない」がデータ上の意図として明示され、
        // 行が無い（＝不整合）ケースと区別できる
        var catalog = new FakeCatalog().WithTransitions(Grassland, Grassland);

        var result = FieldTransition.Next(catalog, Grassland, Deterministic);

        Assert.Equal(Grassland, result.FieldKey);
        Assert.False(result.Degraded);
    }

    // --- 終了理由ごとの分岐 ---

    [Fact]
    public void PlayerVictoryは累積敵レベルを1つ進める()
    {
        var catalog = StandardCatalog();

        var plan = SpawnPlanner.PlanNext(
            BattleEndReason.PlayerVictory, Grassland, 100, "prev", Rarity.Common, catalog, Deterministic);

        Assert.Equal(101, plan.CumulativeEnemyLevel);
        Assert.False(plan.FieldChanged);
    }

    [Fact]
    public void フィールド切替は加算後の値で判定され切替後のフィールドから抽選する()
    {
        // 「+1 → 切替判定 → 抽選」の順。順序を誤ると切替の境目のターンで
        // 切替前のフィールドから抽選してしまう
        var catalog = new FakeCatalog()
            .WithTransitions(Grassland, Cave)
            .WithRarities(Cave, (Rarity.Common, 10000))
            .WithGroups(Cave, Rarity.Common, "cave_group")
            .WithGroups(Grassland, Rarity.Common, "grassland_group");

        var plan = SpawnPlanner.PlanNext(
            BattleEndReason.PlayerVictory, Grassland,
            GameConstants.FieldTransitionPeriod - 1, "prev", Rarity.Common, catalog, Deterministic);

        Assert.Equal(GameConstants.FieldTransitionPeriod, plan.CumulativeEnemyLevel);
        Assert.True(plan.FieldChanged);
        Assert.Equal(Cave, plan.FieldKey);
        Assert.Equal("cave_group", plan.GroupKey); // 切替後のフィールドの組
    }

    [Theory]
    [InlineData(BattleEndReason.PlayerEscaped)]
    [InlineData(BattleEndReason.EnemyEscaped)]
    public void 逃走では累積敵レベルもフィールドも変化しない(BattleEndReason reason)
    {
        // 切替判定は累積敵レベルが変化する PlayerVictory でのみ発火しうる
        var catalog = StandardCatalog();

        var plan = SpawnPlanner.PlanNext(
            reason, Grassland, GameConstants.FieldTransitionPeriod - 1, "prev", Rarity.Rare,
            catalog, Deterministic);

        Assert.Equal(GameConstants.FieldTransitionPeriod - 1, plan.CumulativeEnemyLevel);
        Assert.False(plan.FieldChanged);
        Assert.Equal(Grassland, plan.FieldKey);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    public void プレイヤーの逃走は低レアなら同一の組が再出現する(Rarity previous)
    {
        // group_key を直接参照するため DrawGroup を経由せず、
        // フィールドへの紐づけの有無に依存しない（草原フォールバックの対象外）
        var catalog = new FakeCatalog(); // 組を1件も持たないカタログでも成立する

        var plan = SpawnPlanner.PlanNext(
            BattleEndReason.PlayerEscaped, Grassland, 50, "same_group", previous, catalog, Deterministic);

        Assert.Equal("same_group", plan.GroupKey);
        Assert.Equal(previous, plan.Rarity);
        Assert.False(plan.GroupDegraded);
    }

    [Theory]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Mythic)]
    [InlineData(Rarity.Hidden)]
    public void プレイヤーの逃走は高レアならCommonへ落ちる(Rarity previous)
    {
        // レア敵から降りたことへのペナルティ。Uncommon は対象外
        var catalog = StandardCatalog();

        var plan = SpawnPlanner.PlanNext(
            BattleEndReason.PlayerEscaped, Grassland, 50, "rare_group", previous, catalog, Deterministic);

        Assert.Equal(Rarity.Common, plan.Rarity);
        Assert.Equal("grassland_common", plan.GroupKey);
    }

    [Fact]
    public void 敵の逃走はレアリティ分岐なく常にCommonになる()
    {
        // 敵の逃走はプレイヤーの選択の結果ではない。分岐を適用すると Common の敵に逃げられた場合に
        // 同じ敵が戻ってきて、逃走した敵と再戦するという不自然な状況になる
        var catalog = StandardCatalog();

        var plan = SpawnPlanner.PlanNext(
            BattleEndReason.EnemyEscaped, Grassland, 50, "same_group", Rarity.Common,
            catalog, Deterministic);

        Assert.Equal(Rarity.Common, plan.Rarity);
        Assert.NotEqual("same_group", plan.GroupKey);
    }

    [Fact]
    public void チャンネル消失では次の敵を計画しない()
    {
        Assert.Throws<InvalidOperationException>(() => SpawnPlanner.PlanNext(
            BattleEndReason.ChannelMissing, Grassland, 50, "g", Rarity.Common,
            StandardCatalog(), Deterministic));
    }

    [Fact]
    public void 初期化はレベル加算も切替判定も行わない()
    {
        var catalog = StandardCatalog();

        var plan = SpawnPlanner.PlanInitial(catalog, Deterministic);

        Assert.Equal(GameConstants.InitialCumulativeEnemyLevel, plan.CumulativeEnemyLevel);
        Assert.Equal(Grassland, plan.FieldKey);
        Assert.False(plan.FieldChanged);
    }

    // --- 組の生成（SpawnGroup） ---

    [Fact]
    public void 生成された敵はレベルを共有しHPが全快している()
    {
        var catalog = new FakeCatalog().WithGroup("pair", ("slime", 0), ("bat", 1));

        var spawned = new GroupSpawner(catalog).Spawn("pair", level: 300, Deterministic);

        Assert.Equal(2, spawned.Count);
        Assert.All(spawned, s => Assert.Equal(300, s.Enemy.Level));
        Assert.All(spawned, s => Assert.Equal(s.Enemy.MaxLife, s.Enemy.CurrentLife));
        // spawn_index は member_index の恒等複製
        Assert.Equal([0, 1], spawned.Select(s => (int)s.SpawnIndex));
    }

    [Fact]
    public void 同じ組を生成しても毎回新しいインスタンスになる()
    {
        // 「前のインスタンスを引き継ぐ」経路は存在しない
        var catalog = new FakeCatalog().WithGroup("solo", ("slime", 0));
        var spawner = new GroupSpawner(catalog);

        var first = spawner.Spawn("solo", 100, Deterministic).Single().Enemy;
        first.TakeDamage(first.MaxLife / 2);

        var second = spawner.Spawn("solo", 100, Deterministic).Single().Enemy;

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.MaxLife, second.CurrentLife);
    }

    [Fact]
    public void 装備は出現の都度抽選される()
    {
        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithEquipment("slime", Ratio.FromPercent(50m), EquipPart.Weapon);
        var spawner = new GroupSpawner(catalog);

        var equippedCounts = Enumerable.Range(0, 60)
            .Select(seed => spawner.Spawn("solo", 100, new Random(seed)).Single().Equipment.Count)
            .Distinct()
            .ToList();

        // 装備している出現と、していない出現の両方が現れる
        Assert.Equal(2, equippedCounts.Count);
    }

    [Fact]
    public void 装備の補正は最大HPに反映される()
    {
        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithEquipment("slime", Ratio.Full, EquipPart.Weapon, hpRate: Ratio.Full, progressionValue: 1);

        var bare = new GroupSpawner(new FakeCatalog().WithGroup("solo", ("slime", 0)))
            .Spawn("solo", 100, Deterministic).Single().Enemy;
        var armed = new GroupSpawner(catalog).Spawn("solo", 100, Deterministic).Single().Enemy;

        Assert.True(armed.MaxLife > bare.MaxLife);
        // 全快は装備を載せた後に行われる。先に全快させると装備ぶんが乗らない
        Assert.Equal(armed.MaxLife, armed.CurrentLife);
    }

    [Fact]
    public void 同一部位に複数当たっても1つしか装着されない()
    {
        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithEquipment("slime", Ratio.Full, EquipPart.Weapon, equipKey: "sword")
            .WithEquipment("slime", Ratio.Full, EquipPart.Weapon, equipKey: "axe");

        var spawned = new GroupSpawner(catalog).Spawn("solo", 100, Deterministic).Single();

        var equipment = Assert.Single(spawned.Equipment);
        Assert.Equal("sword", equipment.Option.EquipKey); // 先に引かれた候補が部位を取る
    }

    [Fact]
    public void 複数部位に適合する装備は空いている部位へ入る()
    {
        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithEquipment("slime", Ratio.Full, EquipPart.Weapon, equipKey: "sword")
            .WithEquipment("slime", Ratio.Full, EquipPart.Weapon | EquipPart.Head, equipKey: "helm");

        var spawned = new GroupSpawner(catalog).Spawn("solo", 100, Deterministic).Single();

        Assert.Equal(2, spawned.Equipment.Count);
        Assert.Equal(EquipPart.Head, spawned.Equipment[1].Part);
    }

    [Fact]
    public void auto付与の状態変化が出現時に再適用される()
    {
        var poison = new EffectDefinition("poison", "毒", slipDamage: new SlipDamageSpec(20));
        var applier = new EffectApplier(new Dictionary<string, EffectDefinition> { ["poison"] = poison });

        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithAutoEffect("slime", "poison", grantRate: Ratio.Full,
                attackType: AttackType.Physical, durationActions: 6);

        var enemy = new GroupSpawner(catalog, applier).Spawn("solo", 100, Deterministic).Single().Enemy;

        var effect = Assert.Single(enemy.Effects);
        Assert.Equal("poison", effect.EffectKey);
        Assert.Equal(AffectReason.Auto, effect.AffectReason);
        Assert.Null(effect.GrantSourceKey);
        // 付与者は自身。自滅ダメージは自分の与ダメージとして計上される
        Assert.Equal(enemy.Id, effect.GranterEntityId);
        Assert.Equal<ushort?>(6, effect.RemainingActions);
    }

    [Fact]
    public void auto付与はgrant_rateで抽選される()
    {
        var buff = new EffectDefinition("rage", "激昂",
            statusModifiers: [new StatusModifierSpec(TargetStatus.PAtk, Ratio.FromPercent(50m))]);
        var applier = new EffectApplier(new Dictionary<string, EffectDefinition> { ["rage"] = buff });

        var catalog = new FakeCatalog()
            .WithGroup("solo", ("slime", 0))
            .WithAutoEffect("slime", "rage", grantRate: Ratio.FromPercent(50m), durationActions: 3);
        var spawner = new GroupSpawner(catalog, applier);

        var counts = Enumerable.Range(0, 60)
            .Select(seed => spawner.Spawn("solo", 100, new Random(seed)).Single().Enemy.Effects.Count)
            .Distinct()
            .ToList();

        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void メンバーを持たない組の生成は例外になる()
    {
        Assert.Throws<InvalidOperationException>(
            () => new GroupSpawner(new FakeCatalog()).Spawn("empty", 100, Deterministic));
    }

    // --- 起動時検証 ---

    [Fact]
    public void 起動時検証は草原とそのCommonの組を要求する()
    {
        Assert.Equal(2, WorldValidation.Validate(new FakeCatalog()).Count);

        var fieldOnly = new FakeCatalog().WithField(Grassland);
        Assert.Single(WorldValidation.Validate(fieldOnly));

        Assert.Empty(WorldValidation.Validate(StandardCatalog()));
    }

    [Fact]
    public void 起動時検証は不足をまとめて提示する()
    {
        // 1つ直すたびに再起動して次の不足が出る、という運用を避ける
        var problems = WorldValidation.Validate(new FakeCatalog());

        Assert.Contains(problems, p => p.Contains("草原"));
        Assert.Throws<InvalidOperationException>(() => WorldValidation.ThrowIfInvalid(new FakeCatalog()));
        WorldValidation.ThrowIfInvalid(StandardCatalog());
    }

    // --- ヘルパ ---

    private static FakeCatalog StandardCatalog() => new FakeCatalog()
        .WithField(Grassland)
        .WithRarities(Grassland, (Rarity.Common, 10000))
        .WithGroups(Grassland, Rarity.Common, "grassland_common");

    /// <summary>マスタ参照の差し替え。抽選の規則はDBを用意せずに検証できる。</summary>
    private sealed class FakeCatalog : IFieldCatalog, IEnemyCatalog
    {
        private readonly HashSet<string> fields = [];
        private readonly Dictionary<string, List<RarityWeight>> rarities = [];
        private readonly Dictionary<(string, Rarity), List<string>> groups = [];
        private readonly Dictionary<string, List<string>> transitions = [];
        private readonly Dictionary<string, List<EnemyGroupMember>> members = [];
        private readonly Dictionary<string, List<EnemyEquipmentOption>> equipment = [];
        private readonly Dictionary<string, List<EnemyAutoEffect>> autoEffects = [];

        public FakeCatalog WithField(string fieldKey)
        {
            fields.Add(fieldKey);
            return this;
        }

        public FakeCatalog WithRarities(string fieldKey, params (Rarity Rarity, int Permyriad)[] weights)
        {
            fields.Add(fieldKey);
            rarities[fieldKey] = weights
                .Select(w => new RarityWeight(w.Rarity, Ratio.FromPermyriad(w.Permyriad)))
                .ToList();
            return this;
        }

        public FakeCatalog WithGroups(string fieldKey, Rarity rarity, params string[] groupKeys)
        {
            fields.Add(fieldKey);
            groups[(fieldKey, rarity)] = [.. groupKeys];
            return this;
        }

        public FakeCatalog WithTransitions(string fieldKey, params string[] next)
        {
            transitions[fieldKey] = [.. next];
            return this;
        }

        public FakeCatalog WithGroup(string groupKey, params (string EnemyKey, byte MemberIndex)[] entries)
        {
            members[groupKey] = entries.Select(e => new EnemyGroupMember(e.MemberIndex, e.EnemyKey)).ToList();
            return this;
        }

        public FakeCatalog WithEquipment(
            string enemyKey, Ratio equipRate, EquipPart parts,
            string equipKey = "gear", Ratio? hpRate = null, int progressionValue = 0)
        {
            if (!equipment.TryGetValue(enemyKey, out var list)) equipment[enemyKey] = list = [];

            list.Add(new EnemyEquipmentOption(
                equipKey, equipRate, DropRate: Ratio.Zero, parts,
                new EquipmentBonus(
                    progressionValue, Rarity.Common,
                    hpRate ?? Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero, Ratio.Zero,
                    0, Ratio.Zero, Element.None)));
            return this;
        }

        public FakeCatalog WithAutoEffect(
            string enemyKey, string effectKey, Ratio grantRate,
            AttackType? attackType = null, ushort? durationActions = null)
        {
            if (!autoEffects.TryGetValue(enemyKey, out var list)) autoEffects[enemyKey] = list = [];

            list.Add(new EnemyAutoEffect(
                effectKey, grantRate, Ratio.FromPercent(10m), attackType, durationActions));
            return this;
        }

        public bool HasField(string fieldKey) => fields.Contains(fieldKey);

        public IReadOnlyList<RarityWeight> RarityWeightsOf(string fieldKey)
            => rarities.TryGetValue(fieldKey, out var w) ? w : [];

        public IReadOnlyList<string> GroupsOf(string fieldKey, Rarity rarity)
            => groups.TryGetValue((fieldKey, rarity), out var g) ? g : [];

        public IReadOnlyList<string> TransitionsFrom(string fieldKey)
            => transitions.TryGetValue(fieldKey, out var t) ? t : [];

        public IReadOnlyList<EnemyGroupMember> MembersOf(string groupKey)
            => members.TryGetValue(groupKey, out var m) ? m : [];

        public Enemy CreateEnemy(string enemyKey, BigInteger level)
            => new(masterKey: enemyKey, name: enemyKey, level: level, shape: StatShape.Player,
                strengthRate: Ratio.Full, expRate: Ratio.Full, baseSpeed: 500);

        public IReadOnlyList<EnemyEquipmentOption> EquipmentOptionsOf(string enemyKey)
            => equipment.TryGetValue(enemyKey, out var e) ? e : [];

        public IReadOnlyList<EnemyAutoEffect> AutoEffectsOf(string enemyKey)
            => autoEffects.TryGetValue(enemyKey, out var a) ? a : [];
    }
}
