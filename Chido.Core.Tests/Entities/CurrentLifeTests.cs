using System.Numerics;
using Chido.Core;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Entities;

/// <summary>
/// 現在HPの検証（戦闘システム 3.4）。
/// クランプを一切行わないことが決定事項であり、その帰結（オーバーヒールの保持、
/// 最大HP減少時の非切り詰め）をここで固定する。
/// </summary>
public class CurrentLifeTests
{
    /// <summary>
    /// rate がそのまま1スロットの補正値になる装備の基点
    /// （progression_value = 1・rarity = Common により 1.2^0 = 1 でスケーリングが恒等になる）。
    /// </summary>
    private static readonly EquipmentBonus UnitSlot =
        EquipmentBonus.None with { ProgressionValue = 1, Rarity = Rarity.Common };

    private static Player PlayerAtLevel(int level)
    {
        var player = new Player(userId: 1, name: "テスト", exp: new BigInteger(level) * level);
        player.RestoreToFull();
        return player;
    }

    [Fact]
    public void 参加時は最大HPで初期化される()
    {
        // 戦闘ごとに全快するのは意図した仕様（戦闘外の回復手段は設計上存在しない）
        var player = PlayerAtLevel(100);
        Assert.Equal(player.MaxLife, player.CurrentLife);
    }

    [Fact]
    public void 生成直後の現在HPは0であり全快処理を要する()
    {
        // 全快は参加・出現という契機に紐づく操作であり、生成の副作用ではない
        var player = new Player(userId: 1, name: "テスト", exp: 10000);
        Assert.Equal(BigInteger.Zero, player.CurrentLife);
        Assert.False(player.IsAlive);
    }

    // --- ダメージ ---

    [Fact]
    public void 実効ダメージは実際に減少した現在HP分である()
    {
        var player = PlayerAtLevel(10); // MaxLife = 120
        var effective = player.TakeDamage(50);

        Assert.Equal(new BigInteger(50), effective);
        Assert.Equal(player.MaxLife - 50, player.CurrentLife);
    }

    [Fact]
    public void オーバーキルは実効ダメージに計上されない()
    {
        // 生ダメージで台帳に積むとオーバーキルが他人の取り分を破壊するため、
        // min(最終ダメージ, 適用直前の現在HP) を返す（戦闘システム 6.2）
        var player = PlayerAtLevel(10); // MaxLife = 120
        var effective = player.TakeDamage(10000);

        Assert.Equal(new BigInteger(120), effective);
        Assert.Equal(BigInteger.Zero, player.CurrentLife);
        Assert.False(player.IsAlive);
    }

    [Fact]
    public void HPが0の対象への追撃は実効0になる()
    {
        // とどめ以降のインスタンス・モーションは台帳に0が積まれ、報酬ゲートを通らない
        var player = PlayerAtLevel(10);
        player.TakeDamage(10000);

        Assert.Equal(BigInteger.Zero, player.TakeDamage(500));
    }

    [Fact]
    public void 最低1ダメージの保証はエンティティ側では行わない()
    {
        // 保証はダメージパイプラインの責務。ここで下限を敷くと「実効0であるべき経路」を表現できない
        var player = PlayerAtLevel(10);
        var before = player.CurrentLife;

        Assert.Equal(BigInteger.Zero, player.TakeDamage(0));
        Assert.Equal(before, player.CurrentLife);
    }

    // --- 回復 ---

    [Fact]
    public void 回復は最大HPでクランプされない()
    {
        var player = PlayerAtLevel(10); // MaxLife = 120
        var healed = player.Heal(500);

        Assert.Equal(new BigInteger(500), healed);
        Assert.Equal(new BigInteger(620), player.CurrentLife);
        Assert.True(player.CurrentLife > player.MaxLife);
    }

    [Fact]
    public void オーバーヒールは次の参加時に解消される()
    {
        var player = PlayerAtLevel(10);
        player.Heal(500);
        Assert.True(player.CurrentLife > player.MaxLife);

        player.RestoreToFull();
        Assert.Equal(player.MaxLife, player.CurrentLife);
    }

    // --- 最大HPの変動 ---

    [Fact]
    public void 装備で最大HPが減っても現在HPは切り詰められない()
    {
        // 切り詰めると「装備を一時的に外して戻すとHPが減る」という不可逆な副作用が生まれる
        var player = PlayerAtLevel(100);
        var fullLife = player.CurrentLife;

        player.SetEquipment([UnitSlot with { MaxLifeRate = Ratio.FromPercent(-50m) }]);

        Assert.Equal(fullLife, player.CurrentLife);
        Assert.True(player.CurrentLife > player.MaxLife);
    }

    [Fact]
    public void 装備を外して戻しても現在HPは変わらない()
    {
        var player = PlayerAtLevel(100);
        var fullLife = player.CurrentLife;
        var equipment = new[] { UnitSlot with { MaxLifeRate = Ratio.FromPercent(-50m) } };

        player.SetEquipment(equipment);
        player.SetEquipment([]);

        Assert.Equal(fullLife, player.CurrentLife);
        Assert.Equal(player.MaxLife, player.CurrentLife);
    }

    [Fact]
    public void 状態変化で最大HPが減っても現在HPは切り詰められない()
    {
        var player = PlayerAtLevel(100);
        var fullLife = player.CurrentLife;

        player.AddStatusModifier(new StatusModifier(TargetStatus.MaxLife, Ratio.FromPercent(-90m)));

        Assert.Equal(fullLife, player.CurrentLife);
    }

    // --- 表示用の割合 ---

    [Fact]
    public void HP割合はオーバーヒールでも例外にならない()
    {
        var player = PlayerAtLevel(10);
        player.Heal(player.MaxLife); // 200%

        Assert.Equal(Ratio.FromPercent(200m), player.LifeRatio);
    }

    [Fact]
    public void 最大HPが0以下ならHP割合は0を返す()
    {
        // デバフの累積で MaxLife が0以下になった場合のゼロ除算回避
        var player = PlayerAtLevel(100);
        player.AddStatusModifier(new StatusModifier(TargetStatus.MaxLife, Ratio.FromPercent(-100m)));

        Assert.Equal(BigInteger.Zero, player.MaxLife);
        Assert.Equal(Ratio.Zero, player.LifeRatio);
    }

    [Fact]
    public void HP割合はint範囲を超えても飽和して例外にならない()
    {
        // 極端なオーバーヒールで permyriad が int を超えうる
        var player = PlayerAtLevel(1);
        player.Heal(BigInteger.Pow(10, 30));

        Assert.Equal(Ratio.FromPermyriad(int.MaxValue), player.LifeRatio);
    }
}
