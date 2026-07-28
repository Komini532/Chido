namespace Chido.Core.Entities.Enemies;

/// <summary>
/// 敵の行動パターン（スキルの選択規則）。対をなす対象の選択規則は <see cref="AllyTargetRule"/>。
///
/// require_tp を払えないスキルの扱いが本値により分岐する（戦闘システム 4.2参照）。
/// ランダム系は「払えるものだけで抽選プールを構成」し、ローテーションは「順序を維持したまま
/// 出力だけ通常攻撃へ差し替える」。順序そのものに意味があるかどうかの違いによる。
/// </summary>
// DB(chido_enemy_master.action_pattern_type: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum ActionPatternType
{
    /// <summary>require_tp を満たすスキルのみのプールから等確率。プールが空なら通常攻撃へフォールバック。</summary>
    PureRandom = 0,

    /// <summary>
    /// require_tp を満たすスキルのみのプールを、残存エントリの weight をそのまま用いて
    /// その合計で正規化して抽選する。プールが空なら通常攻撃へフォールバック。
    /// weight を参照するのは本パターンだけであり、他パターンでは weight = 0 のスキルも通常通り使用される。
    /// </summary>
    WeightedRandom = 1,

    /// <summary>
    /// rotation_index の順に選択する。選ばれたスキルが require_tp を満たさない場合、そのターンは
    /// 通常攻撃へフォールバックするが順番は飛ばさない。rotation_index は成否に関わらず前進する。
    /// </summary>
    Rotation = 2,
}
