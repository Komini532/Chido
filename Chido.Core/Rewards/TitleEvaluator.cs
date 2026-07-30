using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Progression;

namespace Chido.Core.Rewards;

/// <summary>
/// 称号の獲得判定（DB設計 34番・35番）。
///
/// 判定は<b>報酬を適用した後</b>の状態に対して行う。撃破で得た経験値・通貨・アイテムが
/// そのターンの称号条件を満たしうるため、適用前の状態で判定すると獲得が1戦闘ぶん遅れる。
///
/// 既に獲得済みの称号は再判定しない。<c>chido_player_title</c> は獲得の記録であり、
/// 条件を下回っても剥奪されない（所持金額の到達称号を、使ったからといって失うのは直感に反する）。
/// </summary>
public static class TitleEvaluator
{
    /// <summary>
    /// 新たに獲得した称号を返す。
    /// </summary>
    /// <param name="titles">称号マスタ。</param>
    /// <param name="owned">既に獲得済みの称号キー。</param>
    public static IReadOnlyList<string> Evaluate(
        IEnumerable<TitleCondition> titles, IReadOnlySet<string> owned, TitleProgress progress)
        => titles
            .Where(title => !owned.Contains(title.TitleKey))
            .Where(title => IsSatisfied(title, progress))
            .Select(title => title.TitleKey)
            .ToList();

    private static bool IsSatisfied(TitleCondition title, TitleProgress progress)
        => title.AcquisitionType switch
        {
            // 判定値は識別ID形式。参照先は acquisition_type により分岐する
            TitleAcquisitionType.ItemObtained =>
                title.ConditionKey is { } key && progress.AcquiredItemKeys.Contains(key),

            TitleAcquisitionType.EnemyDefeated =>
                title.ConditionKey is { } key && progress.DefeatedEnemyKeys.Contains(key),

            // 閾値は BigInteger。10進整数文字列で格納されているためC#側で比較する
            TitleAcquisitionType.LevelReached =>
                title.ConditionValue is { } threshold && progress.Level >= threshold,

            TitleAcquisitionType.CurrencyReached =>
                title.ConditionValue is { } threshold && progress.Currency >= threshold,

            _ => false,
        };
}

/// <summary>称号マスタ1行（chido_title_master）のうち判定に必要な部分。</summary>
/// <param name="ConditionKey">acquisition_type = 0 なら item_key、1 なら enemy_key。</param>
/// <param name="ConditionValue">acquisition_type = 2 ならレベル閾値、3 なら所持金額閾値。</param>
public readonly record struct TitleCondition(
    string TitleKey,
    TitleAcquisitionType AcquisitionType,
    string? ConditionKey,
    BigInteger? ConditionValue);

/// <summary>
/// 判定時点のプレイヤーの状態。
/// </summary>
/// <param name="AcquiredItemKeys">
/// 所持しているアイテム。「特定アイテム獲得」は所持を条件とするため、
/// その戦闘で得たものだけでなく所持品全体を渡す。
/// </param>
/// <param name="DefeatedEnemyKeys">
/// その戦闘で撃破した敵の種族キー。撃破の履歴を持つテーブルが存在しないため、
/// 判定は撃破した瞬間にのみ成立する（獲得済みの称号は記録として残る）。
/// </param>
public readonly record struct TitleProgress(
    IReadOnlySet<string> AcquiredItemKeys,
    IReadOnlySet<string> DefeatedEnemyKeys,
    BigInteger Level,
    BigInteger Currency);
