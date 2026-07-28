using Chido.Core;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests;

/// <summary>
/// GameConstants の検証。
/// ここで守りたいのは個々の数値そのものではなく、設計ドキュメントが要求している
/// 「複数の参照点が同じ1箇所を指す」という関係と、定数間に成り立つべき等式である。
/// </summary>
public class GameConstantsTests
{
    [Fact]
    public void 攻撃と防御のスキルキーは互いに異なる()
    {
        // TP+100の契機判定・習得管理除外・priority 既定値という3つの参照点が
        // この2つの定数を共有する（戦闘システム 4.1・4.4）
        Assert.NotEqual(GameConstants.AttackSkillKey, GameConstants.DefendSkillKey);
        Assert.NotEmpty(GameConstants.AttackSkillKey);
        Assert.NotEmpty(GameConstants.DefendSkillKey);
    }

    [Fact]
    public void 攻撃力と防御力のScaleが等しい()
    {
        // 同格同士（Shape 1・強さ倍率 1・装備なし）の被防御係数 ATK ÷ (ATK + DEF) を
        // ちょうど 0.5 にしているのはこの等式。「等価な回復威力 = 攻撃威力 × 被防御係数」
        // という較正の起点になっている（戦闘システム 5.1）
        Assert.Equal(GameConstants.AttackScale, GameConstants.DefenseScale);
    }

    [Fact]
    public void HPのScaleは攻撃防御の1_5倍である()
    {
        // 威力100%の通常攻撃が 4L ダメージ・最大HPが 12L となり、
        // 最大HP比 33.3% という較正表（戦闘システム 5.1）が成立する関係
        Assert.Equal(GameConstants.LifeScale * 2, GameConstants.AttackScale * 3);
    }

    [Fact]
    public void TPの蓄積量は上限を超えない()
    {
        Assert.True(GameConstants.TpGainOnAttackMotion < GameConstants.TpMax);
        Assert.True(GameConstants.TpGainOnDefendMotion < GameConstants.TpMax);
        Assert.True(GameConstants.TpGainOnDamagedNumerator < GameConstants.TpMax);
    }

    [Fact]
    public void 通常攻撃と防御のTP蓄積量は等しい()
    {
        // Defend は「TPが尽きたときの安全な選択肢」であり、通常攻撃と同じ蓄積を得る（戦闘システム 4.4）
        Assert.Equal(GameConstants.TpGainOnAttackMotion, GameConstants.TpGainOnDefendMotion);
    }

    [Fact]
    public void 同格の被反撃1回あたりのTPが回復スキルの下限根拠と一致する()
    {
        // 同格では反撃 4L・最大HP 12L のため、被反撃1回のTPは 500 × 4L ÷ 12L ≒ 166。
        // require_tp ≤ 166 では回復を毎ターン撃ててしまい実用帯が消滅するため、
        // 回復モーションを含むスキルの require_tp は 200 以上と定められている（戦闘システム 4.4・5.1）
        var damagePerCounter = GameConstants.AttackScale / 2; // 4L（被防御係数 0.5）
        var maxLife          = GameConstants.LifeScale;       // 12L

        var tpPerCounter = GameConstants.TpGainOnDamagedNumerator * damagePerCounter / maxLife;

        Assert.Equal(166, tpPerCounter);
    }

    [Fact]
    public void 初期値とレベル下限は同一の値を共有する()
    {
        // 一方だけ変えて不整合が出ることを防ぐため、独立に持たない（戦闘システム 2.3・10.5）
        Assert.Equal(GameConstants.MinLevel, GameConstants.InitialExp);
        Assert.Equal(GameConstants.MinLevel, GameConstants.InitialCumulativeEnemyLevel);
    }

    [Fact]
    public void 防御のDRRは半減を表す()
    {
        Assert.Equal(5000, GameConstants.DefendDamageResistRate.Permyriad);

        // ダメージパイプラインへ供給される係数 (10000 - Σr) ÷ 10000 は 0.5 になる
        var coefficient = Ratio.Full - GameConstants.DefendDamageResistRate;
        Assert.Equal(5000, coefficient.Permyriad);
    }

    [Fact]
    public void クリティカルの発生率と倍率が設計値である()
    {
        Assert.Equal(400,   GameConstants.CriticalRate.Permyriad);       // 4%
        Assert.Equal(15000, GameConstants.CriticalMultiplier.Permyriad); // ×1.5
    }

    [Fact]
    public void 属性補正の分子分母が1_3倍を表す()
    {
        Assert.Equal(13, GameConstants.ElementAffinityNumerator);
        Assert.Equal(10, GameConstants.ElementAffinityDenominator);
    }

    [Fact]
    public void 草原のフィールドキーが定義されている()
    {
        // 起動時検証・DrawGroup のフォールバック・NextField のフォールバック・初期フィールド固定の
        // 4者がこの1箇所を参照する（戦闘システム 10.5）
        Assert.NotEmpty(GameConstants.GrasslandFieldKey);
    }

    [Fact]
    public void プレイヤーのShapeは等倍である()
    {
        // _shape 列は permyriad ではなく 100 = 1.00 のスケール（DB設計の命名規約）
        Assert.Equal(100, GameConstants.PlayerShape);
    }

    [Fact]
    public void プレイヤーのLuckの基本値は0である()
    {
        Assert.Equal(Ratio.Zero, GameConstants.BaseLuck);
    }
}
