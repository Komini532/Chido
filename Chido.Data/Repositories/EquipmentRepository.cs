using Chido.Core.Entities;
using Chido.Core.Equipment;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 所持装備と装着状況（<c>chido_player_equipment</c> / <c>chido_player_equipment_slot</c>）。
///
/// <para>
/// <b>ロック順序は ① プレイヤー行 → ③ セッション行</b>で、②チャンネル行を飛ばす
/// （戦闘システム 7.2）。装備は所有者本人にしか書き換えられないため②の直列化を要さず、
/// 一方で戦闘中の変更が許されているためセッションとの排他だけが要る。
/// </para>
/// <para>
/// ステータスは動的算出であるため、装着を書き換えれば次の参照から即座に反映される
/// （再計算を明示的に呼ぶ経路は存在しない。戦闘システム 2.5）。
/// </para>
/// </summary>
public sealed class EquipmentRepository(ChidoDbContext db)
{
    /// <summary>スロットの並び。装備可能部位の解決順であり、表示順でもある。</summary>
    public static readonly EquipPart[] Slots =
    [
        EquipPart.Weapon, EquipPart.Head, EquipPart.Chest, EquipPart.Legs, EquipPart.Accessory1,
    ];

    /// <summary>所持している装備を、マスタの内容を添えて返す。</summary>
    public async Task<IReadOnlyList<OwnedEquipment>> OwnedAsync(
        ulong userId, CancellationToken cancellationToken = default)
    {
        var instances = await db.PlayerEquipments
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (instances.Count == 0) return [];

        var keys = instances.Select(x => x.EquipKey).Distinct().ToList();

        var masters = await db.EquipmentMasters
            .Where(x => keys.Contains(x.EquipKey))
            .ToDictionaryAsync(x => x.EquipKey, cancellationToken);

        var slot = await FindSlotAsync(userId, cancellationToken);

        return instances
            .Where(x => masters.ContainsKey(x.EquipKey))
            .Select(x => new OwnedEquipment(
                x.InstanceId,
                x.EquipKey,
                masters[x.EquipKey].Name,
                masters[x.EquipKey].EquipParts,
                masters[x.EquipKey].Rarity,
                EquippedPartOf(slot, x.InstanceId)))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 装着する。
    ///
    /// <para>
    /// 部位は<b>空いている適合部位のうち最も小さいもの</b>へ入れ、空きが無ければ
    /// 最も小さい適合部位を置き換える。装備可能部位はビット列であり
    /// 「複数スロットのいずれかを選んで装着できる」という択一の候補提示であるため、
    /// どこへ入るかはアプリ側が決めるほかない。外された装備は所持に残る
    /// （<c>chido_player_equipment</c> の行は消さない）。
    /// </para>
    /// </summary>
    /// <returns>装着した部位と、押し出された装備のインスタンスID。</returns>
    public async Task<EquipResult> EquipAsync(
        ulong userId, Guid instanceId, EquipPart parts, CancellationToken cancellationToken = default)
    {
        var slot = await FindSlotAsync(userId, cancellationToken);

        if (slot is null)
        {
            slot = new PlayerEquipmentSlotRecord { UserId = userId };
            db.PlayerEquipmentSlots.Add(slot);
        }

        var candidates = Slots.Where(part => (parts & part) != 0).ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("装備可能な部位が定義されていない。");
        }

        // 同じ装備が既に別の部位に入っている場合は先に外す。
        // 外さないと1つのインスタンスが2つの部位を占め、補正が二重に乗る
        if (EquippedPartOf(slot, instanceId) is { } occupied) Assign(slot, occupied, null);

        var target = candidates.FirstOrDefault(part => Read(slot, part) is null, candidates[0]);
        var displaced = Read(slot, target);

        Assign(slot, target, instanceId);

        return new EquipResult(target, displaced);
    }

    /// <summary>装着中の装備を部位ごとに返す（表示用）。</summary>
    public async Task<IReadOnlyList<(EquipPart Part, OwnedEquipment Equipment)>> EquippedAsync(
        ulong userId, CancellationToken cancellationToken = default)
    {
        var owned = (await OwnedAsync(userId, cancellationToken))
            .Where(x => x.EquippedPart is not null)
            .ToList();

        return owned
            .Select(x => (Part: x.EquippedPart!.Value, Equipment: x))
            .OrderBy(x => Array.IndexOf(Slots, x.Part))
            .ToList();
    }

    private Task<PlayerEquipmentSlotRecord?> FindSlotAsync(
        ulong userId, CancellationToken cancellationToken)
        => db.PlayerEquipmentSlots.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    private static EquipPart? EquippedPartOf(PlayerEquipmentSlotRecord? slot, Guid instanceId)
    {
        if (slot is null) return null;

        foreach (var part in Slots)
        {
            if (Read(slot, part) == instanceId) return part;
        }

        return null;
    }

    private static Guid? Read(PlayerEquipmentSlotRecord slot, EquipPart part) => part switch
    {
        EquipPart.Weapon => slot.WeaponInstanceId,
        EquipPart.Head => slot.HeadInstanceId,
        EquipPart.Chest => slot.ChestInstanceId,
        EquipPart.Legs => slot.LegsInstanceId,
        EquipPart.Accessory1 => slot.Accessory1InstanceId,

        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "未知の装備部位。"),
    };

    private static void Assign(PlayerEquipmentSlotRecord slot, EquipPart part, Guid? instanceId)
    {
        switch (part)
        {
            case EquipPart.Weapon: slot.WeaponInstanceId = instanceId; break;
            case EquipPart.Head: slot.HeadInstanceId = instanceId; break;
            case EquipPart.Chest: slot.ChestInstanceId = instanceId; break;
            case EquipPart.Legs: slot.LegsInstanceId = instanceId; break;
            case EquipPart.Accessory1: slot.Accessory1InstanceId = instanceId; break;

            default:
                throw new ArgumentOutOfRangeException(nameof(part), part, "未知の装備部位。");
        }
    }
}

/// <summary>所持している装備1つ。</summary>
/// <param name="EquippedPart">装着中の部位。装着していなければ null。</param>
public readonly record struct OwnedEquipment(
    Guid InstanceId,
    string EquipKey,
    string Name,
    EquipPart Parts,
    Rarity Rarity,
    EquipPart? EquippedPart);

/// <summary>装着の結果。</summary>
/// <param name="Displaced">押し出された装備。空きへ入った場合は null。</param>
public readonly record struct EquipResult(EquipPart Part, Guid? Displaced);
