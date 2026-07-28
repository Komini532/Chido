namespace Chido.Core.Progression;

/// <summary>
/// スキルの習得理由。
/// 通常攻撃（Attack）と防御（Defend）は習得手続きなしに全プレイヤーが常時使用でき、
/// chido_player_skill の管理対象外であるため本値を持たない
/// （対象の skill_key は <see cref="GameConstants"/> に集約されている）。
/// 装備限定スキルも本テーブルには保持せず、装備側から動的に参照する（将来対応）。
/// </summary>
// DB(chido_player_skill.learned_reason: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum LearnedReason
{
    /// <summary>レベルアップ時の自動習得（chido_skill_master.learnable_level が条件）。</summary>
    Level = 0,

    /// <summary>アイテム消費による習得（item_usage_type = LearnSkill）。</summary>
    Item = 1,

    /// <summary>管理者コマンドによる付与。</summary>
    Cheat = 2,
}
