using System;
using System.Collections.Generic;
using System.Linq;
using Chido.Core.Entities;

namespace Chido.Core.World;

/// <summary>
/// 敵の組の抽選（戦闘システム 10.3）。
///
/// <code>
/// DrawGroup(field, forced_rarity = NULL):
///     rarity = forced_rarity ?? field のレアリティ重みで抽選（1段目）
///     候補 = field に紐づく組のうち rarity が一致するもの
///     if 候補が空:
///         縮退を通知・記録する
///         候補 = 草原に紐づく組のうち rarity = Common のもの   ← フォールバック
///         if 候補が空: throw
///     return 候補から完全ランダムに1つ
/// </code>
///
/// レアリティは<b>組が持つ</b>。個体（chido_enemy_master.rarity）もレアリティを持つが、
/// あちらは表示専用であり抽選には使用しない。
/// </summary>
public static class GroupDraw
{
    /// <summary>
    /// 組を1つ抽選する。
    /// </summary>
    /// <param name="forcedRarity">
    /// 1段目を飛ばして固定するレアリティ。<c>PlayerEscaped</c>（前組が Rare 以上）と
    /// <c>EnemyEscaped</c> が <see cref="Rarity.Common"/> を指定する。
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// 草原の Common の組が0件の場合。10.5 の起動時検証がこの正常系を塞ぐため、
    /// ここに到達するのはマスタが壊れているときだけ。抽選はチャンネル行ロック下で
    /// 撃破を含む処理と同一トランザクションに入るため、例外はそのターン全体をロールバックさせ、
    /// 不整合なチャンネル状態を残さない最も安全な失敗になる。
    /// </exception>
    public static GroupDrawResult Draw(
        IFieldCatalog catalog, string fieldKey, Random rng, Rarity? forcedRarity = null)
    {
        var rarity = forcedRarity ?? DrawRarity(catalog, fieldKey, rng);

        var candidates = catalog.GroupsOf(fieldKey, rarity);

        if (candidates.Count > 0)
        {
            return new GroupDrawResult(Pick(candidates, rng), rarity, Degraded: false);
        }

        // 草原の Common へ落とす。レアリティは保存しない。
        // 保存すると「フィールドAの Mythic 定義漏れが草原の Mythic 報酬を引き出す抜け道」になる。
        // Common への降格により、マスタ不整合は常に「報酬が減る」方向にしか作用しない
        var fallback = catalog.GroupsOf(GameConstants.GrasslandFieldKey, Rarity.Common);

        if (fallback.Count == 0)
        {
            throw new InvalidOperationException(
                $"{fieldKey} の {rarity} に組が無く、フォールバック先である草原の Common にも組が存在しない。" +
                "起動時検証（戦闘システム 10.5）が通っていれば到達しない。");
        }

        // フィールド自体は変更しない（草原から借りるのは組だけ）
        return new GroupDrawResult(Pick(fallback, rng), Rarity.Common, Degraded: true);
    }

    /// <summary>
    /// 1段目のレアリティ抽選。
    ///
    /// 重みの合計で正規化する（合計が 10000 である前提を置かない）。
    /// マスタの重みが端数で 10000 に満たない場合に、素の permyriad で引くと
    /// 余りの確率だけ「どれにも当たらない」区間が生まれ、静かに縮退経路へ落ちてしまう。
    /// <see cref="Rarity.Hidden"/> はイベント専用のため、表に混入していても除外する。
    /// </summary>
    private static Rarity DrawRarity(IFieldCatalog catalog, string fieldKey, Random rng)
    {
        var weights = catalog.RarityWeightsOf(fieldKey)
            .Where(w => w.Rarity != Rarity.Hidden && w.Rate.Permyriad > 0)
            .ToList();

        // 重みが1件も無ければ Common とみなす。組の候補が空なら後段のフォールバックが働く
        if (weights.Count == 0) return Rarity.Common;

        var total = weights.Sum(w => w.Rate.Permyriad);
        var roll = rng.Next(total);

        foreach (var weight in weights)
        {
            roll -= weight.Rate.Permyriad;
            if (roll < 0) return weight.Rarity;
        }

        // 合計を厳密に消費しきるため到達しない
        return weights[^1].Rarity;
    }

    private static string Pick(IReadOnlyList<string> candidates, Random rng)
        => candidates[rng.Next(candidates.Count)];
}

/// <summary>組の抽選結果。</summary>
/// <param name="GroupKey">抽選された組。</param>
/// <param name="Rarity">確定したレアリティ。縮退した場合は <see cref="Rarity.Common"/> になる。</param>
/// <param name="Degraded">
/// 草原の Common へフォールバックしたか。真なら<b>縮退を通知・記録する</b>
/// （マスタ不整合が無言で進行しないようにするため）。
/// </param>
public readonly record struct GroupDrawResult(string GroupKey, Rarity Rarity, bool Degraded);
