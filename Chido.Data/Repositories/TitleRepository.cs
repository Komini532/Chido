using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>獲得済みの称号（表示用）。付与そのものは <see cref="RewardRepository"/> が行う。</summary>
public sealed class TitleRepository(ChidoDbContext db)
{
    public async Task<IReadOnlyList<OwnedTitle>> OwnedAsync(
        ulong userId, CancellationToken cancellationToken = default)
    {
        var keys = await db.PlayerTitles
            .Where(x => x.UserId == userId)
            .Select(x => x.TitleKey)
            .ToListAsync(cancellationToken);

        if (keys.Count == 0) return [];

        var masters = await db.TitleMasters
            .Where(x => keys.Contains(x.TitleKey))
            .ToListAsync(cancellationToken);

        return masters
            .Select(x => new OwnedTitle(x.TitleKey, x.Name, x.Emoji))
            .OrderBy(x => x.TitleKey, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>
/// 獲得済みの称号1件。
/// </summary>
/// <param name="Emoji">
/// Unicode文字、または Discord カスタム絵文字の完成済みタグ文字列。
/// そのまま本文へ差し込める形で格納されている。
/// </param>
public readonly record struct OwnedTitle(string TitleKey, string Name, string Emoji);
