namespace Chido.Data.Entities;

/// <summary>
/// chido_channel_current_enemy (38): 現在出現中の敵。
/// chido_battle_enemy は履歴として物理削除されないため、そちらに channel_id 列を持たせても
/// 現在出現中の敵を識別できないことによる。
///
/// 行の生存期間は「敵の組が出現してから、セッションが終了して次の組に入れ替わるまで」。
/// 書き込みは常に新規インスタンスであり、「前のインスタンスを引き継ぐ」経路は存在しない
/// （前組が同一 group_key の場合も再生成する。戦闘システム 10.3）。
///
/// セッションがまだ存在しない状態（初期化直後、および次の組が出現してから誰も行動していない状態）でも
/// 敵は存在するため、本テーブルは session_id を持たない。
/// </summary>
public class ChannelCurrentEnemyRecord
{
    /// <summary>chido_channel_state.channel_id を参照。</summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// 組内の出現順。chido_enemy_group_member_master.member_index を引き継ぎ、
    /// さらに chido_battle_participant.display_order へ恒等複製される。
    /// </summary>
    public byte SpawnIndex { get; set; }

    /// <summary>chido_battle_enemy.enemy_id を参照。</summary>
    public Guid EnemyId { get; set; }
}
