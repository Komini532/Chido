using System.Numerics;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_enemy: 戦闘中の敵の状態。
/// current_hp は chido_battle_participant 側に既に存在するため、ここには重複して持たない。
/// </summary>
public class BattleEnemyRecord
{
    /// <summary>
    /// 出現の都度新規発行される使い捨てGuid。
    /// 1つのenemy_idにつきchido_battle_participant行は常に1つのみ。
    /// </summary>
    public Guid EnemyId { get; set; }

    /// <summary>
    /// chido_enemy_master.enemy_key を参照。どの敵か（種別）を示す。
    /// enemy_master自体は暫定・未確定のため、DB上のFK制約は張っていない。
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;

    /// <summary>敵のレベル。基本ステータスはプレイヤー同様レベルから動的算出するためこれ以外は持たない。</summary>
    public BigInteger Level { get; set; }
}
