namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_group_member_master (43): 組の構成メンバー。
/// 組の全メンバーは出現時の chido_channel_state.cumulative_enemy_level と同一のレベルを持つ。
/// メンバーごとのレベル差は設けない（強さの差は chido_enemy_master.*_shape と strength_rate で表現する）。
/// </summary>
public class EnemyGroupMemberMasterRecord
{
    /// <summary>chido_enemy_group_master.group_key を参照。</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// 出現順。chido_channel_current_enemy.spawn_index に引き継がれ、
    /// Discord埋め込みでの表示順、ひいてはターゲット自動再選定における「先頭の敵」を決定する。
    /// </summary>
    public byte MemberIndex { get; set; }

    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;
}
