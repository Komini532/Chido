using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Stats;
using Chido.Rendering;
using Xunit;

namespace Chido.Tests.Rendering;

/// <summary>
/// 状態変化の表示の検証（戦闘システム 3.1）。
/// </summary>
public class EffectDisplayTests
{
    private static string NameOf(string effectKey) => effectKey switch
    {
        "poison" => "毒",
        "curse" => "呪い",
        "blessing" => "祝福",
        _ => effectKey,
    };

    [Fact]
    public void 同一の効果は1行に集約され残りターンが併記される()
    {
        // 括弧内の要素数がそのまま重ね掛け数を表し、レイヤー内加算の内訳が読み取れる
        var effects = new[]
        {
            NewEffect("poison", 5),
            NewEffect("poison", 3),
            NewEffect("poison", 8),
        };

        var lines = EffectDisplay.Render(effects, NameOf);

        Assert.Equal("[毒] (3, 5, 8)", Assert.Single(lines));
    }

    [Fact]
    public void 無期限は無限記号で表記される()
    {
        var lines = EffectDisplay.Render([NewEffect("curse", null)], NameOf);

        Assert.Equal("[呪い] (∞)", Assert.Single(lines));
    }

    [Fact]
    public void 無期限と有限が混在すると無期限が先に来る()
    {
        var effects = new[] { NewEffect("poison", 3), NewEffect("poison", null) };

        var lines = EffectDisplay.Render(effects, NameOf);

        Assert.Equal("[毒] (∞, 3)", Assert.Single(lines));
    }

    [Fact]
    public void 表示順は無期限が先頭で以降は残り最短の昇順になる()
    {
        // 無期限を先頭に置くのは「絶対に消えない＝常に効き続ける」という情報の重要度による
        var effects = new[]
        {
            NewEffect("poison", 9),
            NewEffect("blessing", 2),
            NewEffect("curse", null),
        };

        var lines = EffectDisplay.Render(effects, NameOf);

        Assert.Equal(["[呪い] (∞)", "[祝福] (2)", "[毒] (9)"], lines);
    }

    [Fact]
    public void 残りターンが同値ならeffect_keyで順序が決まる()
    {
        // 最終タイブレーク。同値でも順序が決定的になる
        var effects = new[] { NewEffect("zebra", 3), NewEffect("alpha", 3) };

        var lines = EffectDisplay.Render(effects, NameOf);

        Assert.Equal(["[alpha] (3)", "[zebra] (3)"], lines);
    }

    [Fact]
    public void 表示名は16文字で切り詰められる()
    {
        // マスタ側は VARCHAR(100) のまま維持し、切り詰めは描画層の責務とする
        var longName = new string('あ', 30);

        var lines = EffectDisplay.Render(
            [NewEffect("long", 1)], _ => longName);

        Assert.Equal($"[{new string('あ', 16)}…] (1)", Assert.Single(lines));
    }

    [Fact]
    public void 上限ちょうどの表示名は切り詰められない()
    {
        var name = new string('あ', 16);

        var lines = EffectDisplay.Render([NewEffect("exact", 1)], _ => name);

        Assert.Equal($"[{name}] (1)", Assert.Single(lines));
    }

    [Fact]
    public void 種類数の上限を超えると他n件へ畳まれる()
    {
        // 切り落とされるのは常に「最も遠くに消えるもの」であり、略記の切り口が情報の緊急度と一致する
        var effects = Enumerable.Range(1, 20)
            .Select(i => NewEffect($"effect_{i:00}", (ushort)i))
            .ToList();

        var lines = EffectDisplay.Render(effects, NameOf, kindLimit: 15);

        Assert.Equal(16, lines.Count);
        Assert.Equal("他 5 件", lines[^1]);
        // 残り1〜15 が残り、16以降が落ちる
        Assert.Equal("[effect_01] (1)", lines[0]);
        Assert.Equal("[effect_15] (15)", lines[14]);
    }

    [Fact]
    public void 上限ちょうどなら略記は付かない()
    {
        var effects = Enumerable.Range(1, 15)
            .Select(i => NewEffect($"effect_{i:00}", (ushort)i))
            .ToList();

        var lines = EffectDisplay.Render(effects, NameOf, kindLimit: 15);

        Assert.Equal(15, lines.Count);
        Assert.DoesNotContain(lines, l => l.StartsWith("他 "));
    }

    [Fact]
    public void 効果が無ければ空になる()
    {
        Assert.Empty(EffectDisplay.Render([], NameOf));
    }

    [Fact]
    public void 両スコープの効果が同じ集合として扱われる()
    {
        // どちらも状態変化補正に加算されるため、片方だけでは表示と実ステータスが一致しない
        var effects = new[]
        {
            NewEffect("poison", 3, EffectScope.Battle),
            NewEffect("curse", 7, EffectScope.Player),
        };

        var lines = EffectDisplay.Render(effects, NameOf);

        Assert.Equal(["[毒] (3)", "[呪い] (7)"], lines);
    }

    private static EffectInstance NewEffect(
        string effectKey, ushort? remaining, EffectScope scope = EffectScope.Battle)
        => new(
            new EffectDefinition(effectKey, effectKey),
            AffectReason.Skill,
            Guid.NewGuid(),
            scope,
            grantSourceKey: "skill",
            remainingActions: remaining);
}
