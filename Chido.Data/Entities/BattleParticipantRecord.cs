using System.Numerics;
using Chido.Core.Battle;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_participant: 戦闘参加者。
/// seat_order / initiative / has_acted は、共有ターンキューを前提とした概念のため持たない。
/// </summary>
public class BattleParticipantRecord
{
    /// <summary>chido_battle_session.session_id を参照。</summary>
    public Guid SessionId { get; set; }

    /// <summary>参加者インスタンスの使い捨てGuid（IEntity.Id）。</summary>
    public Guid EntityId { get; set; }

    /// <summary>0: Player, 1: Enemy。</summary>
    public EntityType EntityType { get; set; }

    /// <summary>entity_type=Player のとき必須。chido_player.user_id を参照。</summary>
    public ulong? UserId { get; set; }

    /// <summary>entity_type=Enemy のとき必須。chido_battle_enemy.enemy_id を参照。</summary>
    public Guid? EnemyId { get; set; }

    /// <summary>
    /// 戦闘中の現在HP。同一の敵への同時攻撃が起きうるため、更新時はアプリ側で
    /// SELECT ... FOR UPDATE による悲観ロックを用いる運用。
    /// </summary>
    public BigInteger CurrentHp { get; set; }

    /// <summary>参加時刻。ターン制御には使わず、Discord埋め込みでの表示順序付けにのみ使用。</summary>
    public DateTime JoinedAt { get; set; }
}
