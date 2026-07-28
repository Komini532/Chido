namespace Chido.Core.Battle.Skills;

/// <summary>
/// 対象の解決規則。モーション単位で持つ（スキル単位だと「自分を強化 → 敵を攻撃」のような
/// 複数対象のモーション列が表現できない）。影響範囲は常に単体固定であり、範囲という概念は存在しない
/// （旧 chido_skill_master.range_type からの改称。戦闘システム 4.2参照）。
///
/// これは「プレイヤーが選ぶ選択肢」ではなく「対象をどう解決するかの規則」であり、
/// 3つの値のうち2つはユーザー入力を一切消費しない。
/// </summary>
// DB(chido_skill_motion_master.target_rule: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum TargetRule
{
    /// <summary>行動者そのもの。[対象] の指定があっても対象は変わらない（Ally より強い規則）。</summary>
    Myself = 0,

    /// <summary>
    /// コマンドの [対象]。省略時は行動者自身に解決する。「味方」は自分自身を含む。
    /// 敵には [対象] 入力が無いため、敵側は EnemyMaster.ally_target_rule で解決する。
    /// </summary>
    Ally = 1,

    /// <summary>chido_battle_participant.current_target_id（戦闘システム 3.3 の単一導出関数）。</summary>
    Enemy = 2,
}
