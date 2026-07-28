namespace Chido.Core.Battle.Skills;

/// <summary>
/// モーション種別。スキルは1つ以上のモーションから構成され、motion_index 昇順に再生される。
/// 攻撃と回復は列構成も意味論も異なる（被防御係数・クリティカル・属性相性は攻撃にのみ適用される）ため、
/// power を符号付きにして統合するような扱いは行わない（戦闘システム 4.2参照）。
/// </summary>
// DB(chido_skill_motion_master.motion_type: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。サブタイプテーブルの判別子でもあり、値の変更はデータの誤接続に直結する。
// 今後の変更は末尾への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
public enum MotionType
{
    Attack        = 0, // → chido_skill_motion_attack_master（attack_type / power / elements）
    Heal          = 1, // → chido_skill_motion_heal_master  （attack_type / power。属性を持たない）
    GrantEffect   = 2, // → chido_skill_motion_effect_master（effect_key / effect_rate / attack_type / duration_actions）
    Flee          = 3, // サブタイプなし（可変パラメータを持たない）。/escape と同一の戦闘離脱処理を呼び出す
    DispelEffect  = 4, // → chido_skill_motion_dispel_master（effect_key）
}
