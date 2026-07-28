namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_enemy_equipment: 敵の装備インスタンス（戦闘内スコープ）。
/// chido_player_equipment と同型だが、chido_battle_enemy 自体が戦闘スコープの一時的な実体であるのに合わせ、
/// こちらも戦闘スコープの別テーブルとして持つ。
///
/// 装備がドロップする場合、本テーブルのインスタンスをそのまま所有者移転するのではなく、
/// 内容を複製した新しいインスタンスとして chido_player_equipment に新規 instance_id で INSERT する
/// （同一の敵から複数プレイヤーが報酬を受け取りうるため、1つの装備インスタンスを共有できない）。
/// </summary>
public class BattleEnemyEquipmentRecord
{
    /// <summary>使い捨てGuid。敵の出現時、chido_enemy_equipment_master の抽選結果に基づき新規発行される。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>chido_battle_enemy.enemy_id を参照。</summary>
    public Guid EnemyId { get; set; }

    /// <summary>chido_equipment_master.equip_key を参照。</summary>
    public string EquipKey { get; set; } = string.Empty;
}
