using Chido.Core.Items;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 所持アイテムと習得スキル。<c>/skill</c> と <c>/use</c> の受理判定がここを通る。
/// </summary>
public sealed class InventoryRepository(ChidoDbContext db)
{
    /// <summary>
    /// 習得済みスキルのキー。
    ///
    /// <b>通常攻撃と防御は含まれない。</b>両者は習得管理の対象外であり
    /// （<c>chido_player_skill</c> に行を持たない）、習得手続きなしで常に使える。
    /// 除外の根拠は <c>GameConstants</c> の <c>AttackSkillKey</c> / <c>DefendSkillKey</c> にあり、
    /// TP蓄積の契機判定・<c>priority</c> 既定値と同じ1箇所を参照する。
    /// </summary>
    public async Task<IReadOnlySet<string>> LearnedSkillsAsync(
        ulong userId, CancellationToken cancellationToken = default)
        => (await db.PlayerSkills
                .Where(x => x.UserId == userId)
                .Select(x => x.SkillKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

    /// <summary>所持数が1以上のアイテム。表示名つきで返す。</summary>
    public async Task<IReadOnlyList<OwnedItem>> OwnedItemsAsync(
        ulong userId, CancellationToken cancellationToken = default)
    {
        var owned = await db.PlayerItems
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .ToListAsync(cancellationToken);

        if (owned.Count == 0) return [];

        var keys = owned.Select(x => x.ItemKey).ToList();

        var masters = await db.ItemMasters
            .Where(x => keys.Contains(x.ItemKey))
            .ToDictionaryAsync(x => x.ItemKey, cancellationToken);

        return owned
            .Where(x => masters.ContainsKey(x.ItemKey))
            .Select(x => new OwnedItem(
                x.ItemKey, masters[x.ItemKey].Name, x.Quantity,
                masters[x.ItemKey].ItemType, masters[x.ItemKey].IsConsumable))
            .OrderBy(x => x.ItemKey, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// アイテムが発動するスキル（<c>item_usage_type = UseSkill</c>）。
    ///
    /// アイテムの効果は「特定スキルの発動」に収束するため、発動そのものは通常のスキル発動と
    /// 同じ経路を通る。<b>習得状況は問わない</b>（習得していないスキルでも発動する）。
    /// </summary>
    public async Task<string?> UsedSkillKeyAsync(
        string itemKey, CancellationToken cancellationToken = default)
    {
        var usage = await db.ItemUsedEffectMasters
            .Where(x => x.ItemKey == itemKey && x.ItemUsageType == ItemUsageType.UseSkill)
            .OrderBy(x => x.UsageIndex)
            .FirstOrDefaultAsync(cancellationToken);

        return usage?.SkillKey;
    }

    /// <summary>
    /// アイテムを1つ消費する。消費アイテムでなければ何もしない。
    /// </summary>
    /// <returns>実際に減らしたなら true。</returns>
    public async Task<bool> ConsumeAsync(
        ulong userId, string itemKey, CancellationToken cancellationToken = default)
    {
        var master = await db.ItemMasters
            .FirstOrDefaultAsync(x => x.ItemKey == itemKey, cancellationToken);

        if (master is null || !master.IsConsumable) return false;

        var owned = await db.PlayerItems
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ItemKey == itemKey, cancellationToken);

        if (owned is null || owned.Quantity == 0) return false;

        owned.Quantity--;
        return true;
    }
}

/// <summary>所持アイテム1件。</summary>
public readonly record struct OwnedItem(
    string ItemKey, string Name, uint Quantity, ItemType ItemType, bool IsConsumable);
