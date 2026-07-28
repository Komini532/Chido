namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_skills_master: 敵の使用スキル。
/// 通常攻撃も他のスキルと同様、抽選プールの1エントリとして登録されうる（特別扱いはされない）。
/// 登録された通常攻撃とフォールバックの通常攻撃は別物であり、前者はローテ枠を占め抽選候補になる。
/// </summary>
public class EnemySkillsMasterRecord
{
    /// <summary>chido_enemy_master.enemy_key を参照。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>再生・抽選順序。ローテーションの total はこのテーブルの登録行数。</summary>
    public byte EnemySkillIndex { get; set; }

    /// <summary>chido_skill_master.skill_key を参照。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>
    /// 抽選の相対重み。合計値に意味を持たないため Ratio への変換対象外。
    /// action_pattern_type=WeightedRandom でのみ参照される。0 = 抽選対象外。
    /// ただし完全ランダム／ローテーションでは本列自体が無視されるため、weight=0 のスキルも
    /// それらのパターンでは通常通り使用される（意図的な非対称）。
    /// </summary>
    public byte Weight { get; set; }
}
