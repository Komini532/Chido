using Chido.Data;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Chido.Battle;

/// <summary>
/// 装備の変更（<c>/equip</c>）。
///
/// <para>
/// <b>ロック順序は ① プレイヤー行 → ③ セッション行で、②チャンネル行を飛ばす</b>
/// （戦闘システム 7.2）。段の飛ばしは順序違反ではない。装備は所有者本人にしか
/// 書き換えられないためチャンネル単位の直列化を要さず、一方で<b>戦闘中の変更が
/// 許されている</b>ためセッションとの排他だけが要る。
/// </para>
/// <para>
/// ステータスは動的算出であるため、変更は次の参照から即座に反映される。
/// 最大HPが下がっても現在HPは切り詰めない（クランプすると「装備を外して戻すとHPが減る」という
/// 不可逆な副作用が生まれる。戦闘システム 3.4）。
/// </para>
/// </summary>
public sealed class EquipmentService(IDbContextFactory<ChidoDbContext> dbFactory)
{
    /// <summary>
    /// 装備を装着する。
    /// </summary>
    /// <param name="instanceId">
    /// 所持している装備インスタンスのID。オートコンプリートから選ばれた値がそのまま届く。
    /// </param>
    public async Task<EquipOutcome> EquipAsync(
        ulong userId, string userName, string? instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(instanceId, out var parsed))
        {
            return EquipOutcome.Refuse("装備を候補から選んでください。");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // アンカー行の用意はロックスコープの外側で行う
        await new PlayerRepository(db).EnsureAsync(userId, userName, cancellationToken);

        var equipment = new EquipmentRepository(db);

        // 構造体の既定値と取り違えないよう、見つからなかったことを列の有無で判定する
        var matches = (await equipment.OwnedAsync(userId, cancellationToken))
            .Where(x => x.InstanceId == parsed)
            .ToList();

        if (matches.Count == 0)
        {
            return EquipOutcome.Refuse("その装備を所持していません。");
        }

        var owned = matches[0];

        await using var scope = await BattleLock.BeginAsync(db, cancellationToken);
        await scope.LockPlayerAsync(userId, cancellationToken);

        // 参加中のセッションがあればその行も取る。②を飛ばす経路であるため、
        // 戦闘行動との排他はここでしか成立しない
        var membership = await db.PlayerInBattleSessions
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (membership is not null)
        {
            await scope.LockSessionAsync(membership.SessionId, cancellationToken);
        }

        var result = await equipment.EquipAsync(userId, parsed, owned.Parts, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        var displaced = result.Displaced is { } id
            ? (await equipment.OwnedAsync(userId, cancellationToken))
                .FirstOrDefault(x => x.InstanceId == id).Name
            : null;

        return new EquipOutcome(true, owned.Name, result.Part, displaced, null);
    }
}

/// <summary>
/// 装着の結果。
/// </summary>
/// <param name="Displaced">押し出された装備の表示名。空きへ入った場合は null（所持には残る）。</param>
public readonly record struct EquipOutcome(
    bool Accepted,
    string? Name,
    Core.Equipment.EquipPart Part,
    string? Displaced,
    string? Refusal)
{
    public static EquipOutcome Refuse(string reason)
        => new(false, null, default, null, reason);
}
