using Chido.Core.Progression;

namespace Chido.Data.Entities;

/// <summary>
/// chido_player_skill: プレイヤー習得スキル。
/// 通常攻撃（Attack）と防御（Defend）は習得管理の対象外であり、本テーブルに行を持たない
/// （対象の skill_key は Chido.Core.GameConstants に集約されている）。
/// 装備限定スキルも本テーブルには保持せず、装備側から動的に参照する（将来対応）。
/// </summary>
public class PlayerSkillRecord
{
    /// <summary>chido_player.user_id を参照。</summary>
    public ulong UserId { get; set; }

    /// <summary>chido_skill_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>習得理由。</summary>
    public LearnedReason LearnedReason { get; set; }
}
