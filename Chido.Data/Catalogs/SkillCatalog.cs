using Chido.Core;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities.Enemies;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Catalogs;

/// <summary>
/// スキルマスタとモーションのサブタイプ（10a〜10d）から <see cref="Skill"/> を組み立てる。
///
/// <para>
/// <b>マスタは戦闘中に変化しないため、生成時に一括で読み込んでメモリ上に保持する。</b>
/// スキルの解決はチャンネル行ロック下の同一トランザクションで走るため、都度クエリを発行すると
/// ロック保持時間がそのまま伸び、直列化の待ち時間に上乗せされる（<c>DatabaseWorldCatalog</c> と同じ判断）。
/// マスタデータの投入・変更後は作り直すこと。
/// </para>
/// </summary>
public sealed class SkillCatalog
{
    private readonly Dictionary<string, Skill> skills;
    private readonly Dictionary<string, List<EnemySkillEntry>> enemySkills;

    private SkillCatalog(
        Dictionary<string, Skill> skills,
        Dictionary<string, List<EnemySkillEntry>> enemySkills)
    {
        this.skills = skills;
        this.enemySkills = enemySkills;
    }

    /// <summary>マスタを一括で読み込む。Bot起動時とマスタ投入後に1回だけ呼ぶ。</summary>
    public static async Task<SkillCatalog> LoadAsync(
        ChidoDbContext db, CancellationToken cancellationToken = default)
    {
        var masters = await db.SkillMasters.ToListAsync(cancellationToken);

        var motions = (await db.SkillMotionMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.SkillKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.MotionIndex).ToList());

        var attacks = (await db.SkillMotionAttackMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => (x.SkillKey, x.MotionIndex));
        var heals = (await db.SkillMotionHealMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => (x.SkillKey, x.MotionIndex));
        var grants = (await db.SkillMotionEffectMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => (x.SkillKey, x.MotionIndex));
        var dispels = (await db.SkillMotionDispelMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => (x.SkillKey, x.MotionIndex));

        var skills = masters.ToDictionary(
            master => master.SkillKey,
            master => new Skill(
                master.SkillKey,
                master.Name,
                BuildMotions(master.SkillKey, motions, attacks, heals, grants, dispels),
                master.Priority,
                master.RequireTp));

        var enemySkills = (await db.EnemySkillsMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.EnemyKey)
            .ToDictionary(
                g => g.Key,
                // enemy_skill_index が登録順を持ち、そのままローテーションの順序になる。
                // ローテーションの法 total はこの件数であり、順序に意味があるため列の指定に従う
                g => g.OrderBy(x => x.EnemySkillIndex)
                    .Where(x => skills.ContainsKey(x.SkillKey))
                    .Select(x => new EnemySkillEntry(skills[x.SkillKey], x.Weight))
                    .ToList());

        return new SkillCatalog(skills, enemySkills);
    }

    /// <summary>スキルを引く。存在しなければ null。</summary>
    public Skill? Find(string skillKey) => skills.GetValueOrDefault(skillKey);

    /// <summary>
    /// スキルを引く。存在しなければ例外。
    /// 通常攻撃・防御はマスタに必ず存在する前提であり、欠けているならマスタ投入の不備。
    /// </summary>
    public Skill Get(string skillKey)
        => Find(skillKey) ?? throw new InvalidOperationException(
            $"スキルマスタに {skillKey} が存在しない。");

    /// <summary>
    /// 通常攻撃。TP蓄積の契機判定・習得管理の除外・<c>priority</c> 既定値の3者が
    /// 同じ <see cref="GameConstants.AttackSkillKey"/> を参照する。
    /// </summary>
    public Skill Attack => Get(GameConstants.AttackSkillKey);

    /// <summary>防御。自分自身への DRR 付与モーション1つで、反撃モーションを含まない。</summary>
    public Skill Defend => Get(GameConstants.DefendSkillKey);

    /// <summary>敵の保有スキル（<c>chido_enemy_skills_master</c>）。</summary>
    public IReadOnlyList<EnemySkillEntry> SkillsOf(string enemyKey)
        => enemySkills.GetValueOrDefault(enemyKey) ?? [];

    /// <summary>
    /// モーション列を組み立てる。サブタイプ側の行が欠けているモーションは<b>落とさずに例外</b>にする。
    /// 親（10番）に行があるのにサブタイプが無い状態はマスタの不整合であり、
    /// 黙って短いスキルとして成立させると「なぜか効果が出ないスキル」として運用に紛れ込む。
    /// </summary>
    private static IEnumerable<SkillMotion> BuildMotions(
        string skillKey,
        IReadOnlyDictionary<string, List<SkillMotionMasterRecord>> motions,
        IReadOnlyDictionary<(string, byte), SkillMotionAttackMasterRecord> attacks,
        IReadOnlyDictionary<(string, byte), SkillMotionHealMasterRecord> heals,
        IReadOnlyDictionary<(string, byte), SkillMotionEffectMasterRecord> grants,
        IReadOnlyDictionary<(string, byte), SkillMotionDispelMasterRecord> dispels)
    {
        if (!motions.TryGetValue(skillKey, out var rows)) yield break;

        foreach (var motion in rows)
        {
            var key = (motion.SkillKey, motion.MotionIndex);

            yield return motion.MotionType switch
            {
                MotionType.Attack when attacks.TryGetValue(key, out var attack) =>
                    new AttackMotion(
                        motion.MotionIndex, motion.TargetRule, motion.AccuracyRate,
                        attack.AttackType, attack.Power, attack.Elements, motion.AccuracyGateGroup),

                MotionType.Heal when heals.TryGetValue(key, out var heal) =>
                    new HealMotion(
                        motion.MotionIndex, motion.TargetRule, motion.AccuracyRate,
                        heal.AttackType, heal.Power, motion.AccuracyGateGroup),

                MotionType.GrantEffect when grants.TryGetValue(key, out var grant) =>
                    new GrantEffectMotion(
                        motion.MotionIndex, motion.TargetRule, motion.AccuracyRate,
                        grant.EffectKey, grant.EffectRate, grant.AttackType,
                        grant.DurationActions, motion.AccuracyGateGroup),

                MotionType.DispelEffect when dispels.TryGetValue(key, out var dispel) =>
                    new DispelEffectMotion(
                        motion.MotionIndex, motion.TargetRule, motion.AccuracyRate,
                        dispel.EffectKey, motion.AccuracyGateGroup),

                // 戦闘離脱は可変パラメータを持たないためサブタイプを持たない
                MotionType.Flee =>
                    new FleeMotion(
                        motion.MotionIndex, motion.TargetRule, motion.AccuracyRate,
                        motion.AccuracyGateGroup),

                _ => throw new InvalidOperationException(
                    $"{skillKey} のモーション {motion.MotionIndex}（{motion.MotionType}）に " +
                    "対応するサブタイプの行が存在しない。"),
            };
        }
    }
}
