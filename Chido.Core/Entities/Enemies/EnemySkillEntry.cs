using Chido.Core.Battle.Skills;

namespace Chido.Core.Entities.Enemies;

/// <summary>
/// 敵が保有するスキル1件（chido_enemy_skills_master）。種族単位のマスタであり、戦闘中は不変。
/// </summary>
/// <param name="Skill">スキル本体。通常攻撃も他と同様に1エントリとして登録されうる。</param>
/// <param name="Weight">
/// 抽選の相対重み。<see cref="ActionPatternType.WeightedRandom"/> でのみ参照される。
///
/// <c>weight = 0</c> は「そのパターンにおいて抽選対象外」を意味するが、
/// 完全ランダム・ローテーションでは本列自体が無視されるため、
/// <c>weight = 0</c> のスキルも通常通り使用される（意図的な非対称）。
/// </param>
public readonly record struct EnemySkillEntry(Skill Skill, ushort Weight = 1);
