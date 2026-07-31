using Chido.Core.Battle.Effects;

namespace Chido.Rendering;

/// <summary>
/// 状態変化の表示（戦闘システム 3.1）。
///
/// <code>
/// [&lt;状態変化名&gt;] (&lt;残りターン&gt;)
/// </code>
///
/// <b>同一 effect_key の複数インスタンスは1行に集約し、残り有効行動数をカンマ区切りで併記する</b>
/// （例: <c>[毒] (3, 5, 8)</c>）。括弧内の要素数がそのまま重ね掛け数を表し、
/// レイヤー内加算の内訳が読み取れる。無期限は <c>(∞)</c>。
///
/// 表示対象は戦闘スコープと永続スコープの<b>和集合</b>。どちらも状態変化補正に加算されるため、
/// 片方だけでは表示と実ステータスが一致しない。
///
/// <b>表示しないもの</b>: 付与者・付与元スキル・付与要因・効果量。閲覧者依存の情報、
/// または1戦闘行動のレスポンスとしては情報過多であるため。
///
/// UIの語としては「残りターン」と呼ぶ。保持者にとって1ターンは自分の1行動そのものなので
/// 数値としては行動数と一致しており、「行動数」という表現が一般的でないための丸めである。
/// </summary>
public static class EffectDisplay
{
    /// <summary>状態変化名の表示上限。超過分は「…」で切り詰める。</summary>
    public const int NameLength = 16;

    /// <summary>戦闘行動のレスポンスにおける状態変化の種類数の上限。</summary>
    public const int ResponseKindLimit = 15;

    /// <summary>無期限（残り有効行動数を持たない）の表記。</summary>
    public const string Endless = "∞";

    /// <summary>
    /// 保持中の状態変化を1行ずつの表示へ変換する。
    /// </summary>
    /// <param name="nameOf">effect_key → 表示名。マスタが引けない場合はキーをそのまま返す実装を渡す。</param>
    /// <param name="kindLimit">種類数の上限。超過分は「他 n 件」に畳む。</param>
    public static IReadOnlyList<string> Render(
        IEnumerable<EffectInstance> effects,
        Func<string, string> nameOf,
        int kindLimit = ResponseKindLimit)
    {
        var groups = Aggregate(effects).ToList();

        if (groups.Count <= kindLimit)
        {
            return groups.Select(g => Format(g, nameOf)).ToList();
        }

        // 表示順が「無期限 → 残り最短の昇順」であるため、切り落とされるのは常に
        // 「最も遠くに消えるもの」になり、略記の切り口が情報の緊急度と一致する
        var shown = groups.Take(kindLimit).Select(g => Format(g, nameOf)).ToList();
        shown.Add($"他 {groups.Count - kindLimit} 件");

        return shown;
    }

    /// <summary>
    /// effect_key ごとに集約し、表示順に並べる。
    ///
    /// <code>
    /// 1. 無期限（∞）を先頭
    /// 2. 最短の残り有効行動数の昇順
    /// 3. effect_key（最終タイブレーク）
    /// </code>
    ///
    /// 無期限を先頭に置くのは「絶対に消えない＝常に効き続ける」という情報の重要度による。
    /// effect_key の比較を最後に置くことで、同値でも順序が決定的になる。
    /// </summary>
    private static IEnumerable<EffectGroup> Aggregate(IEnumerable<EffectInstance> effects)
        => effects
            .GroupBy(e => e.EffectKey, StringComparer.Ordinal)
            .Select(g => new EffectGroup(
                g.Key,
                // 括弧内も同じ規則で並べる。無期限を先に、以降は残り最短の昇順
                [.. g.Select(e => e.RemainingActions)
                    .OrderBy(r => r is null ? 0 : 1)
                    .ThenBy(r => r ?? 0)],
                HasEndless: g.Any(e => e.RemainingActions is null),
                Shortest: g.Where(e => e.RemainingActions is not null)
                    .Select(e => (int)e.RemainingActions!.Value)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min()))
            .OrderBy(g => g.HasEndless ? 0 : 1)
            .ThenBy(g => g.Shortest)
            .ThenBy(g => g.EffectKey, StringComparer.Ordinal);

    private static string Format(EffectGroup group, Func<string, string> nameOf)
    {
        var name = Truncate(nameOf(group.EffectKey));
        var remaining = string.Join(", ", group.Remaining.Select(r => r?.ToString() ?? Endless));

        return $"[{name}] ({remaining})";
    }

    /// <summary>
    /// 表示名の切り詰め。マスタ側（VARCHAR(100)）は長いまま維持し、
    /// 切り詰めは描画層の責務とする。
    /// </summary>
    public static string Truncate(string name)
        => name.Length <= NameLength ? name : string.Concat(name.AsSpan(0, NameLength), "…");

    private readonly record struct EffectGroup(
        string EffectKey, IReadOnlyList<ushort?> Remaining, bool HasEndless, int Shortest);
}
