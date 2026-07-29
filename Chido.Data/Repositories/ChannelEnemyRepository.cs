using Chido.Core.Equipment;
using Chido.Core.World;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// チャンネルに出現中の敵の永続化（戦闘システム 10.3・10.5）。
///
/// <b>敵はセッションに属さない状態で存在しうる。</b>戦闘チャンネルの初期化直後や、
/// <c>PlayerVictory</c> 後に次の組が出現してから誰も行動していない期間がそれにあたる。
/// <c>chido_channel_current_enemy</c> はその「セッションに属さない敵の集合」を表現する。
///
/// <b>すべてチャンネル行②のロック下で呼ぶこと</b>（戦闘システム 7.2）。
/// 次の敵の抽選・出現は既に②を保持している経路であり、追加のロックは要らない。
/// </summary>
public sealed class ChannelEnemyRepository(ChidoDbContext db)
{
    /// <summary>
    /// 出現中の敵を、新しく生成された組で置き換える。
    ///
    /// 差し替えであって追加ではない。前の組の記録（<c>chido_channel_current_enemy</c>）だけを外し、
    /// <c>chido_battle_enemy</c> の行そのものは<b>物理削除しない</b>
    /// （戦闘ログや参加者行から参照されうる記録であるため。戦闘システム 3.2・8.1）。
    /// </summary>
    public async Task ReplaceAsync(
        ulong channelId,
        IReadOnlyList<SpawnedEnemy> spawned,
        CancellationToken cancellationToken = default)
    {
        await ClearAsync(channelId, cancellationToken);

        foreach (var member in spawned)
        {
            db.BattleEnemies.Add(new BattleEnemyRecord
            {
                EnemyId = member.Enemy.Id,
                MasterKey = member.Enemy.MasterKey,
                Level = member.Enemy.Level,
            });

            db.ChannelCurrentEnemies.Add(new ChannelCurrentEnemyRecord
            {
                ChannelId = channelId,
                // 組の member_index の恒等複製。表示順の唯一の根拠になる
                SpawnIndex = member.SpawnIndex,
                EnemyId = member.Enemy.Id,
            });

            AddEquipment(member);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>出現中の敵を <c>spawn_index</c> 昇順で取得する。</summary>
    public Task<List<ChannelCurrentEnemyRecord>> LoadAsync(
        ulong channelId, CancellationToken cancellationToken = default)
        => db.ChannelCurrentEnemies
            .Where(x => x.ChannelId == channelId)
            .OrderBy(x => x.SpawnIndex)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// 出現中の敵の記録を外す。チャンネル消失時の削除
    /// （<c>ChannelStateRepository.DeleteAsync</c>）でも同じ行が対象になる。
    /// </summary>
    public async Task ClearAsync(ulong channelId, CancellationToken cancellationToken = default)
    {
        await db.ChannelCurrentEnemies
            .Where(x => x.ChannelId == channelId)
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete は変更追跡を経由しないため、追跡済みの行が残ると
        // 同じ主キーの再挿入（同一チャンネルへの再出現）が衝突する
        foreach (var entry in db.ChangeTracker.Entries<ChannelCurrentEnemyRecord>()
                     .Where(e => e.Entity.ChannelId == channelId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// 出現時に抽選された装備を記録する。
    ///
    /// 敵の装備は<b>出現時に確定しセッション中に変化しない</b>ため、ロック対象外であり
    /// 書き込みもここ1回だけになる（プレイヤー側との意図的な非対称。戦闘システム 2.5）。
    /// </summary>
    private void AddEquipment(SpawnedEnemy member)
    {
        if (member.Equipment.Count == 0) return;

        var slot = new BattleEnemyEquipmentSlotRecord { EnemyId = member.Enemy.Id };

        foreach (var equipment in member.Equipment)
        {
            var instanceId = Guid.NewGuid();

            db.BattleEnemyEquipments.Add(new BattleEnemyEquipmentRecord
            {
                InstanceId = instanceId,
                EnemyId = member.Enemy.Id,
                EquipKey = equipment.Option.EquipKey,
            });

            switch (equipment.Part)
            {
                case EquipPart.Weapon: slot.WeaponInstanceId = instanceId; break;
                case EquipPart.Head: slot.HeadInstanceId = instanceId; break;
                case EquipPart.Chest: slot.ChestInstanceId = instanceId; break;
                case EquipPart.Legs: slot.LegsInstanceId = instanceId; break;
                case EquipPart.Accessory1: slot.Accessory1InstanceId = instanceId; break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(member), equipment.Part, "未知の装備部位。");
            }
        }

        db.BattleEnemyEquipmentSlots.Add(slot);
    }
}
