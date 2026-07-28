using System.Numerics;
using Chido.Core;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Entities;

/// <summary>
/// ステータス算出の検証（戦闘システム 2.3）。
///
/// 「同格」＝ E = L・Shape 1.00・強さ倍率 1・装備なし・状態変化なし。
/// この条件で HP = 12L、攻撃力 = 防御力 = 8L となり、被防御係数がちょうど 0.5 になる。
/// 5.1 の較正表（通常攻撃 4L・最大HP比 33.3%）はここから導かれるため、
/// 以降のフェーズのバランス検証はすべてこの等式に依存する。
/// </summary>
public class StatCalculationTests
{
    private static Player NewPlayer(BigInteger exp) => new(userId: 1, name: "テスト", exp: exp);

    /// <summary>指定レベルのプレイヤー。exp = level² により floor(√exp) = level となる。</summary>
    private static Player PlayerAtLevel(int level) => NewPlayer(new BigInteger(level) * level);

    /// <summary>
    /// rate がそのまま1スロットの補正値になる装備の基点。
    /// 1スロットの補正値 = progression_value × 1.2^rarity × rate であるため、
    /// progression_value = 1・rarity = Common（1.2^0 = 1）とすればスケーリングが恒等になる。
    /// </summary>
    private static readonly EquipmentBonus UnitSlot =
        EquipmentBonus.None with { ProgressionValue = 1, Rarity = Rarity.Common };

    private static Enemy NewEnemy(
        int level,
        StatShape? shape = null,
        Ratio? strengthRate = null,
        int baseSpeed = 500,
        Element elements = Element.None) => new(
            masterKey: "test_enemy",
            name: "テスト敵",
            level: level,
            shape: shape ?? StatShape.Player,
            strengthRate: strengthRate ?? Ratio.Full,
            expRate: Ratio.Full,
            baseSpeed: baseSpeed,
            innateElements: elements);

    // --- 同格の基礎ステータス ---

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(5000)]
    public void 同格のプレイヤーはHPが12L_攻撃防御が8Lになる(int level)
    {
        var player = PlayerAtLevel(level);

        Assert.Equal(new BigInteger(level) * GameConstants.LifeScale, player.MaxLife);
        Assert.Equal(new BigInteger(level) * GameConstants.AttackScale, player.PAtk);
        Assert.Equal(new BigInteger(level) * GameConstants.AttackScale, player.MAtk);
        Assert.Equal(new BigInteger(level) * GameConstants.DefenseScale, player.PDef);
        Assert.Equal(new BigInteger(level) * GameConstants.DefenseScale, player.MDef);
    }

    [Fact]
    public void 同格同士の被防御係数はちょうど0_5になる()
    {
        // 攻撃力と防御力の Scale が等しいことの帰結。5.1 の較正表の起点
        var attacker = PlayerAtLevel(100);
        var defender = PlayerAtLevel(100);

        // 被防御係数 = ATK ÷ (ATK + DEF)
        Assert.Equal(attacker.PAtk, defender.PDef);
        Assert.Equal(attacker.PAtk * 2, attacker.PAtk + defender.PDef);
    }

    [Fact]
    public void プレイヤーと敵は同じ計算式に従う()
    {
        // 差は Shape・強さ倍率・Speed の基本値だけであり、式そのものは共通
        var player = PlayerAtLevel(50);
        var enemy = NewEnemy(50);

        Assert.Equal(player.MaxLife, enemy.MaxLife);
        Assert.Equal(player.PAtk, enemy.PAtk);
        Assert.Equal(player.PDef, enemy.PDef);
    }

    [Fact]
    public void 敵のShapeがステータスに反映される()
    {
        // Shape は 100 = 1.00 のスケール（permyriad ではない）
        var enemy = NewEnemy(100, shape: new StatShape(MaxLife: 200, PAtk: 50, PDef: 100, MAtk: 100, MDef: 100));

        Assert.Equal(new BigInteger(100 * GameConstants.LifeScale * 2), enemy.MaxLife);
        Assert.Equal(new BigInteger(100 * GameConstants.AttackScale / 2), enemy.PAtk);
        Assert.Equal(new BigInteger(100 * GameConstants.DefenseScale), enemy.PDef);
    }

    [Fact]
    public void 敵の強さ倍率がステータスに反映される()
    {
        var normal = NewEnemy(100);
        var boss = NewEnemy(100, strengthRate: Ratio.FromMultiplier(2m));

        Assert.Equal(normal.MaxLife * 2, boss.MaxLife);
        Assert.Equal(normal.PAtk * 2, boss.PAtk);
    }

    // --- レベル導出 ---

    [Fact]
    public void プレイヤーのレベルは経験値から導出される()
    {
        Assert.Equal(BigInteger.One, NewPlayer(1).Level);
        Assert.Equal(new BigInteger(2), NewPlayer(4).Level);
        Assert.Equal(new BigInteger(2), NewPlayer(8).Level);
        Assert.Equal(new BigInteger(3), NewPlayer(9).Level);
    }

    [Fact]
    public void 経験値の加算がレベルとステータスに即座に反映される()
    {
        var player = NewPlayer(GameConstants.InitialExp);
        Assert.Equal(BigInteger.One, player.Level);

        player.AddExp(3); // exp = 4 → level 2
        Assert.Equal(new BigInteger(2), player.Level);
        Assert.Equal(new BigInteger(2 * GameConstants.LifeScale), player.MaxLife);
    }

    // --- レイヤー内は加算、レイヤー間は乗算 ---

    [Fact]
    public void 同一レイヤー内の複数の補正は加算合成される()
    {
        // +10% の状態変化が2つ併存する場合は ×1.2 であり ×1.21 ではない
        var player = PlayerAtLevel(1000);
        var expected = player.PAtk;

        player.AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.FromPercent(10m)));
        player.AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.FromPercent(10m)));

        Assert.Equal(StatCalculator.ApplyLayer(expected, Ratio.FromMultiplier(1.2m)), player.PAtk);
        Assert.NotEqual(StatCalculator.ApplyLayer(expected, Ratio.FromMultiplier(1.21m)), player.PAtk);
    }

    [Fact]
    public void 装備レイヤーと状態変化レイヤーは乗算される()
    {
        // それぞれ +100% なら ×2 × ×2 = ×4。加算合成なら ×3 になるはずで、そうならないことを確認する
        var player = PlayerAtLevel(1000);
        var baseline = player.PAtk;

        player.SetEquipment([UnitSlot with { PAtkRate = Ratio.Full }]);
        player.AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.Full));

        Assert.Equal(baseline * 4, player.PAtk);
    }

    [Fact]
    public void 装備5スロットぶんの補正は加算合成される()
    {
        var player = PlayerAtLevel(1000);
        var baseline = player.PDef;

        // +20% を5スロット → ×2.0（×1.2^5 = ×2.49 ではない）
        var slot = UnitSlot with { PDefRate = Ratio.FromPercent(20m) };
        player.SetEquipment([slot, slot, slot, slot, slot]);

        Assert.Equal(baseline * 2, player.PDef);
    }

    // --- 1スロットの補正値 = progression_value × 1.2^rarity × rate ---

    [Theory]
    // rarity = Common（1.2^0 = 1）: 補正値は rate そのまま
    [InlineData(1, Rarity.Common, 10000, 10000)]
    // progression_value がそのまま倍率として効く（Lv5000 で P(5000) = 60 の想定）
    [InlineData(60, Rarity.Common, 10000, 600000)]
    // rarity = Uncommon（1.2^1）: 10000 × 6 ÷ 5 = 12000
    [InlineData(1, Rarity.Uncommon, 10000, 12000)]
    // rarity = Rare（1.2^2 = 1.44）: 10000 × 36 ÷ 25 = 14400
    [InlineData(1, Rarity.Rare, 10000, 14400)]
    // rarity = Mythic（1.2^3 = 1.728）
    [InlineData(1, Rarity.Mythic, 10000, 17280)]
    // rarity = Hidden（1.2^4 = 2.0736）
    [InlineData(1, Rarity.Hidden, 10000, 20736)]
    // 負の補正値（デメリット装備）でも同じ式が通る
    [InlineData(10, Rarity.Rare, -10000, -144000)]
    public void 装備1スロットの補正値は進行度とレアリティでスケールされる(
        int progressionValue, Rarity rarity, int ratePermyriad, int expected)
    {
        var actual = StatCalculator.EquipmentContribution(
            progressionValue, rarity, Ratio.FromPermyriad(ratePermyriad));

        Assert.Equal(new BigInteger(expected), actual);
    }

    [Fact]
    public void レアリティ補正は浮動小数点を通さず有理数で累乗される()
    {
        // 1.2^4 = 2.0736 は double では正確に表現できない。6^4 ÷ 5^4 = 1296 ÷ 625 で厳密に計算する
        var contribution = StatCalculator.EquipmentContribution(
            BigInteger.Pow(10, 40), Rarity.Hidden, Ratio.Full);

        Assert.Equal(BigInteger.Pow(10, 40) * 1296 * 10000 / 625, contribution);
    }

    [Fact]
    public void 進行度が0の装備は補正値を供給しない()
    {
        // スロット未装着と等価。EquipmentBonus.None がこの形になっている
        Assert.Equal(BigInteger.Zero,
            StatCalculator.EquipmentContribution(BigInteger.Zero, Rarity.Hidden, Ratio.Full));
    }

    [Fact]
    public void 進行度でスケールされた補正値が装備レイヤーに入る()
    {
        // progression_value = 1・rate = +100% で補正値 1.0 → 装備補正 ×2
        var player = PlayerAtLevel(1000);
        var baseline = player.PAtk;

        player.SetEquipment([UnitSlot with { PAtkRate = Ratio.Full }]);
        Assert.Equal(baseline * 2, player.PAtk);

        // progression_value = 3 なら補正値 3.0 → 装備補正 ×4
        player.SetEquipment([UnitSlot with { ProgressionValue = 3, PAtkRate = Ratio.Full }]);
        Assert.Equal(baseline * 4, player.PAtk);
    }

    [Fact]
    public void 装備の補正値はintに収まらない大きさになりうる()
    {
        // progression_value は DECIMAL(65,0) 相当。合計を Ratio（int）で持てないことの確認
        var player = PlayerAtLevel(1);
        var huge = BigInteger.Pow(10, 30);

        player.SetEquipment([UnitSlot with { ProgressionValue = huge, PAtkRate = Ratio.Full }]);

        Assert.True(player.PAtk > int.MaxValue);
        Assert.Equal(GameConstants.AttackScale * (1 + huge), player.PAtk);
    }

    [Fact]
    public void SpeedとLuckは進行度とレアリティのスケーリングを受けない()
    {
        // Speed と Luck は「上記の乗算構造の対象外」であり、装備の生の値をそのまま加算する
        var player = PlayerAtLevel(100);

        player.SetEquipment([
            UnitSlot with { ProgressionValue = 999, Rarity = Rarity.Hidden, SpeedBonus = 50, LuckBonusRate = Ratio.FromPercent(5m) },
        ]);

        Assert.Equal(GameConstants.PlayerBaseSpeed + 50, player.Speed);
        Assert.Equal(Ratio.FromPercent(5m), player.Luck);
    }

    [Fact]
    public void 状態変化はレイヤーを跨いで対象ステータスごとに独立している()
    {
        var player = PlayerAtLevel(1000);
        var basePAtk = player.PAtk;
        var basePDef = player.PDef;

        player.AddStatusModifier(new StatusModifier(TargetStatus.PAtk, Ratio.FromPercent(50m)));

        Assert.Equal(StatCalculator.ApplyLayer(basePAtk, Ratio.FromMultiplier(1.5m)), player.PAtk);
        Assert.Equal(basePDef, player.PDef);
    }

    // --- 加算合成の帰結（負値） ---

    [Fact]
    public void 強力なデバフが2つ重なると防御力が負値になる()
    {
        // -60% が2つで 1 - 1.2 = -0.2。ここではクランプせず、
        // ダメージ計算式の max(0, ...) が受け止める（戦闘システム 2.3・5.1）
        var player = PlayerAtLevel(1000);
        var baseline = player.PDef;

        player.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-60m)));
        player.AddStatusModifier(new StatusModifier(TargetStatus.PDef, Ratio.FromPercent(-60m)));

        Assert.True(player.PDef < BigInteger.Zero, $"負値になるはずが {player.PDef} だった");
        Assert.Equal(StatCalculator.ApplyLayer(baseline, Ratio.FromMultiplier(-0.2m)), player.PDef);
    }

    [Theory]
    // 1 × -0.15 = -0.15 → 床値 -1（0方向切り捨てなら 0）
    [InlineData(1, -1500, -1)]
    // 10 × -0.15 = -1.5 → 床値 -2（0方向切り捨てなら -1）
    [InlineData(10, -1500, -2)]
    // 正値では床値と0方向切り捨てが一致する
    [InlineData(10, 1500, 1)]
    public void 乗算レイヤーは負の無限大方向へ丸められる(int value, int permyriad, int expected)
    {
        // BigInteger の除算は0方向切り捨てのため、明示的に床除算していないと
        // デバフ累積で負に振れた場合に丸めの向きが変わる
        Assert.Equal(new BigInteger(expected), StatCalculator.ApplyLayer(value, Ratio.FromPermyriad(permyriad)));
    }

    // --- Speed / Luck は枠組みの外 ---

    [Fact]
    public void プレイヤーのSpeedは基本値500である()
    {
        Assert.Equal(GameConstants.PlayerBaseSpeed, PlayerAtLevel(1).Speed);
        Assert.Equal(GameConstants.PlayerBaseSpeed, PlayerAtLevel(9999).Speed);
    }

    [Fact]
    public void Speedはレベルと強さ倍率の影響を受けず装備でのみ変動する()
    {
        var enemy = NewEnemy(9999, strengthRate: Ratio.FromMultiplier(10m), baseSpeed: 300);
        Assert.Equal(300, enemy.Speed);

        enemy.SetEquipment([
            UnitSlot with { SpeedBonus = 50 },
            UnitSlot with { SpeedBonus = -30 },
        ]);
        Assert.Equal(320, enemy.Speed);
    }

    [Fact]
    public void Speedは状態変化の影響を受けない()
    {
        // 現時点では状態変化による変動対象外（番号だけ確保してある）
        var player = PlayerAtLevel(100);
        player.AddStatusModifier(new StatusModifier(TargetStatus.Speed, Ratio.FromPercent(100m)));

        Assert.Equal(GameConstants.PlayerBaseSpeed, player.Speed);
    }

    [Fact]
    public void Luckは基本0パーセントで装備の加算のみで動く()
    {
        var player = PlayerAtLevel(100);
        Assert.Equal(Ratio.Zero, player.Luck);

        // 乗算ではなく%ポイントの加算
        player.SetEquipment([
            UnitSlot with { LuckBonusRate = Ratio.FromPercent(5m) },
            UnitSlot with { LuckBonusRate = Ratio.FromPercent(3m) },
        ]);
        Assert.Equal(Ratio.FromPercent(8m), player.Luck);
    }

    [Fact]
    public void Luckは状態変化の影響を受けない()
    {
        var player = PlayerAtLevel(100);
        player.AddStatusModifier(new StatusModifier(TargetStatus.Luck, Ratio.FromPercent(50m)));

        Assert.Equal(Ratio.Zero, player.Luck);
    }

    // --- 属性 ---

    [Fact]
    public void プレイヤーの本体属性は常に属性なしである()
    {
        Assert.Equal(Element.None, PlayerAtLevel(100).Elements);
    }

    [Fact]
    public void 実効属性は本体と装備と一時付与の和集合である()
    {
        var enemy = NewEnemy(100, elements: Element.Fire);
        Assert.Equal(Element.Fire, enemy.Elements);

        enemy.SetEquipment([
            UnitSlot with { Elements = Element.Water },
            UnitSlot with { Elements = Element.Ice },
        ]);
        Assert.Equal(Element.Fire | Element.Water | Element.Ice, enemy.Elements);

        enemy.GrantElements(Element.Thunder);
        Assert.Equal(Element.Fire | Element.Water | Element.Ice | Element.Thunder, enemy.Elements);

        enemy.ClearGrantedElements();
        Assert.Equal(Element.Fire | Element.Water | Element.Ice, enemy.Elements);
    }

    [Fact]
    public void プレイヤーの属性は装備由来のみである()
    {
        var player = PlayerAtLevel(100);
        player.SetEquipment([UnitSlot with { Elements = Element.Light }]);

        Assert.Equal(Element.Light, player.Elements);
    }

    // --- ダメージ軽減率 ---

    [Fact]
    public void ダメージ軽減率は加算合成され乗算レイヤーには入らない()
    {
        var player = PlayerAtLevel(1000);
        var basePDef = player.PDef;

        player.AddStatusModifier(new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));
        player.AddStatusModifier(new StatusModifier(TargetStatus.DamageResistRate, GameConstants.DefendDamageResistRate));

        // Σr = 10000（50% × 2）
        Assert.Equal(Ratio.Full, player.DamageResistRate);

        // ステータスそのものには一切影響しない
        Assert.Equal(basePDef, player.PDef);
    }

    [Fact]
    public void ダメージ軽減率は状態変化が無ければ0である()
    {
        Assert.Equal(Ratio.Zero, PlayerAtLevel(100).DamageResistRate);
    }
}
