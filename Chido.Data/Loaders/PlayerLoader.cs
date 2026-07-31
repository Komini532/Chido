using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Stats;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Chido.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Loaders;

/// <summary>
/// 永続テーブルから <see cref="Player"/> を復元する。
///
/// <para>
/// <b>復元するのは「算出できない値」だけ。</b>ステータスは保持せず参照のたびに算出されるため
/// （戦闘システム 2.5）、ここで読むのは経験値・装備・永続スコープの状態変化に限る。
/// 現在HP・現在TPはセッションに属する値であり、参加者行から別途与える。
/// </para>
/// <para>
/// 装備は <c>chido_player_equipment_slot</c>（5列）→ <c>chido_player_equipment</c> →
/// <c>chido_equipment_master</c> と辿る。<b>スロットに入っているものだけ</b>が対象であり、
/// 所持しているだけの装備はステータスに影響しない。
/// </para>
/// </summary>
public sealed class PlayerLoader(ChidoDbContext db, EffectCatalog effects)
{
    /// <summary>
    /// プレイヤーを復元する。
    /// </summary>
    /// <param name="entityId">
    /// 参加者行の <c>entity_id</c>。戦闘中はこれを与えて参加者と実体の識別子を一致させる
    /// （<c>CurrentTarget</c> と台帳の帰属がこのIDで解決されるため）。
    /// 戦闘外（<c>/status</c> 等）では省略してよい。
    /// </param>
    public async Task<Player> LoadAsync(
        ulong userId, Guid? entityId = null, CancellationToken cancellationToken = default)
    {
        var status = await db.BattleStatuses
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"chido_battle_status に user_id = {userId} の行が存在しない。" +
                "PlayerRepository.EnsureAsync が呼ばれていない。");

        var record = await db.Players.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        var player = new Player(userId, record?.UserName ?? userId.ToString(), status.Exp, entityId);

        player.SetEquipment(await LoadEquipmentAsync(userId, cancellationToken));

        foreach (var effect in await LoadEffectsAsync(userId, cancellationToken))
        {
            player.AddEffect(effect);
        }

        // 現在HPはセッションに属する値であり非戦闘時には存在しない。
        // 呼び出し側が参加者行の current_hp で上書きするまでは全快として扱う
        player.RestoreToFull();

        return player;
    }

    /// <summary>装着中の装備を <see cref="EquipmentBonus"/> へ変換する。</summary>
    private async Task<List<EquipmentBonus>> LoadEquipmentAsync(
        ulong userId, CancellationToken cancellationToken)
    {
        var slot = await db.PlayerEquipmentSlots
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (slot is null) return [];

        var instanceIds = new[]
        {
            slot.WeaponInstanceId, slot.HeadInstanceId, slot.ChestInstanceId,
            slot.LegsInstanceId, slot.Accessory1InstanceId,
        }.OfType<Guid>().ToList();

        if (instanceIds.Count == 0) return [];

        var instances = await db.PlayerEquipments
            .Where(x => instanceIds.Contains(x.InstanceId))
            .ToListAsync(cancellationToken);

        var equipKeys = instances.Select(x => x.EquipKey).Distinct().ToList();

        var masters = await db.EquipmentMasters
            .Where(x => equipKeys.Contains(x.EquipKey))
            .ToDictionaryAsync(x => x.EquipKey, cancellationToken);

        return instances
            .Where(x => masters.ContainsKey(x.EquipKey))
            .Select(x => ToBonus(masters[x.EquipKey]))
            .ToList();
    }

    /// <summary>
    /// 永続スコープの状態変化を復元する。
    ///
    /// ステータス変動の実値は「マスタの <c>fixed_rate</c> を持つ行はその値、持たない行は
    /// インスタンス側（21番）の実値」という付与時と同じ規則で決まる。両スコープが同じ共有テーブルを
    /// 使うため、組み立ては <c>EffectInstanceRows</c> の1箇所に寄せてある。
    /// </summary>
    private async Task<List<EffectInstance>> LoadEffectsAsync(
        ulong userId, CancellationToken cancellationToken)
    {
        var rows = await db.PlayerEffects
            .Where(x => x.UserId == userId)
            // 併存インスタンスの発動順は instance_id 昇順。BINARY(16) の照合順序は
            // 格納バイト列の辞書順であり、Core 側の EffectInstanceOrder と一致する
            .OrderBy(x => x.InstanceId)
            .ToListAsync(cancellationToken);

        var details = await EffectInstanceRows.ReadAsync(
            db, rows.Select(x => x.InstanceId).ToList(), cancellationToken);

        var result = new List<EffectInstance>();

        foreach (var row in rows)
        {
            var definition = effects.Find(row.EffectKey);

            // マスタから消えた効果は復元できない。落として進む（状態変化が1つ効かなくなるだけで、
            // プレイヤーが戦闘に入れなくなるよりは軽い）
            if (definition is null) continue;

            result.Add(EffectInstanceRows.Rebuild(
                definition, row.InstanceId, row.AffectReason, row.GranterEntityId,
                EffectScope.Player, row.GrantSourceKey, row.RemainingActions, details));
        }

        return result;
    }

    private static EquipmentBonus ToBonus(EquipmentMasterRecord master)
        => new(
            master.ProgressionValue,
            master.Rarity,
            master.HpRate,
            master.PAtkRate,
            master.PDefRate,
            master.MAtkRate,
            master.MDefRate,
            master.SpeedBonus,
            master.LuckBonusRate,
            master.Elements);
}
