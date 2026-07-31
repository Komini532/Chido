using System.Numerics;
using Chido.Core.Battle.Effects;
using Chido.Data;
using Chido.Data.Loaders;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Chido.Battle;

/// <summary>
/// プレイヤー単位の<b>永続情報</b>の読み出し（<c>/status</c> / <c>/inventory</c>）。
///
/// <para>
/// <b>セッションに属する値は扱わない</b>（戦闘システム 9.1）。現在HP・現在TP・戦闘スコープの
/// 状態変化は非戦闘時には存在しない値であり、恒常的なステータスとは表示されるべき文脈が異なる。
/// 分離の基準は「セッションに属するか否か」であって「状態変化か否か」ではないため、
/// <b>永続スコープの状態変化はここに含まれる</b>（<c>[呪い] (7)</c> のように表示する）。
/// 戦闘中でないプレイヤーが、自分に何が残り何行動効いているかを知る手段が他に存在しない。
/// </para>
/// <para>
/// ロックを取らない。読み出しだけであり、途中で他人が書き換えても
/// 「少し古い表示が出る」以上のことは起こらない。
/// </para>
/// </summary>
public sealed class PlayerProfileService(
    IDbContextFactory<ChidoDbContext> dbFactory,
    GameCatalogs catalogs)
{
    public async Task<PlayerProfile> LoadAsync(
        ulong userId, string userName, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 初回実行のプレイヤーでも表示できるようにする。/status が「まず戦ってこい」と
        // 突き放す理由はない
        var players = new PlayerRepository(db);
        await players.EnsureAsync(userId, userName, cancellationToken);

        var player = await new PlayerLoader(db, catalogs.Effects)
            .LoadAsync(userId, cancellationToken: cancellationToken);

        return new PlayerProfile(
            player.Name,
            player.Exp,
            player.Level,
            await players.GetCurrencyAsync(userId, cancellationToken),
            player.MaxLife,
            player.PAtk,
            player.PDef,
            player.MAtk,
            player.MDef,
            player.Speed,
            player.Luck.ToString(),
            await new EquipmentRepository(db).EquippedAsync(userId, cancellationToken),
            await new TitleRepository(db).OwnedAsync(userId, cancellationToken),
            // 永続スコープのみ。戦闘スコープはセッションに属するため対象外
            player.Effects.Where(e => e.Scope == EffectScope.Player).ToList());
    }

    /// <summary>所持アイテム。</summary>
    public async Task<IReadOnlyList<OwnedItem>> InventoryAsync(
        ulong userId, string userName, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        await new PlayerRepository(db).EnsureAsync(userId, userName, cancellationToken);

        return await new InventoryRepository(db).OwnedItemsAsync(userId, cancellationToken);
    }
}

/// <summary>
/// <c>/status</c> が表示する内容。
///
/// ステータスは保持せず参照のたびに算出されるため（戦闘システム 2.5）、ここに並ぶ値は
/// すべて「読み出した瞬間のレベル・装備・永続効果から導かれた結果」であり、
/// どこかに保存されているものではない。
/// </summary>
public sealed record PlayerProfile(
    string Name,
    BigInteger Exp,
    BigInteger Level,
    BigInteger Currency,
    BigInteger MaxLife,
    BigInteger PAtk,
    BigInteger PDef,
    BigInteger MAtk,
    BigInteger MDef,
    int Speed,
    string Luck,
    IReadOnlyList<(Core.Equipment.EquipPart Part, OwnedEquipment Equipment)> Equipment,
    IReadOnlyList<OwnedTitle> Titles,
    IReadOnlyList<EffectInstance> Effects);
