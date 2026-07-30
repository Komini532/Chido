using System.Numerics;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Chido.Core.World;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.World;

/// <summary>
/// マスタテーブルから <see cref="IFieldCatalog"/> / <see cref="IEnemyCatalog"/> を供給する。
///
/// <para>
/// 抽選・生成の規則そのものは Core 側（<see cref="GroupDraw"/> / <see cref="FieldTransition"/> /
/// <see cref="GroupSpawner"/>）にあり、本型はデータの取り出しに徹する。
/// </para>
/// <para>
/// <b>マスタは戦闘中に変化しないため、生成時に一括で読み込んでメモリ上に保持する。</b>
/// 抽選はチャンネル行ロック下の同一トランザクションで走るため、ここで都度クエリを発行すると
/// ロック保持時間がそのまま伸び、7.3 の直列化の待ち時間に上乗せされる。
/// マスタデータの投入・変更後は作り直すこと。
/// </para>
/// </summary>
public sealed class DatabaseWorldCatalog : IFieldCatalog, IEnemyCatalog
{
    private readonly HashSet<string> fields;
    private readonly Dictionary<string, List<RarityWeight>> rarityWeights;
    private readonly Dictionary<(string FieldKey, Rarity Rarity), List<string>> groupsByField;
    private readonly Dictionary<string, List<string>> transitions;
    private readonly Dictionary<string, List<EnemyGroupMember>> groupMembers;
    private readonly Dictionary<string, EnemyMasterRecord> enemies;
    private readonly Dictionary<string, List<EnemyEquipmentOption>> equipmentOptions;
    private readonly Dictionary<string, List<EnemyAutoEffect>> autoEffects;

    /// <summary>
    /// 保有スキルの供給元。省略された場合、生成される敵はスキルを持たず通常攻撃へフォールバックする
    /// （マスタ未投入の段階でも組の生成は成立させる）。
    /// </summary>
    private readonly SkillCatalog? skills;

    private DatabaseWorldCatalog(
        HashSet<string> fields,
        Dictionary<string, List<RarityWeight>> rarityWeights,
        Dictionary<(string, Rarity), List<string>> groupsByField,
        Dictionary<string, List<string>> transitions,
        Dictionary<string, List<EnemyGroupMember>> groupMembers,
        Dictionary<string, EnemyMasterRecord> enemies,
        Dictionary<string, List<EnemyEquipmentOption>> equipmentOptions,
        Dictionary<string, List<EnemyAutoEffect>> autoEffects,
        SkillCatalog? skills)
    {
        this.skills = skills;
        this.fields = fields;
        this.rarityWeights = rarityWeights;
        this.groupsByField = groupsByField;
        this.transitions = transitions;
        this.groupMembers = groupMembers;
        this.enemies = enemies;
        this.equipmentOptions = equipmentOptions;
        this.autoEffects = autoEffects;
    }

    /// <summary>マスタを一括で読み込む。Bot起動時とマスタ投入後に1回だけ呼ぶ。</summary>
    /// <param name="skills">
    /// 生成する敵へ載せる保有スキル。<c>chido_enemy_skills_master</c> の解決を本型が
    /// 内側で行うことで、出現・復元のどちらの経路を通っても敵が必ずスキルを持つ。
    /// </param>
    public static async Task<DatabaseWorldCatalog> LoadAsync(
        ChidoDbContext db, SkillCatalog? skills = null, CancellationToken cancellationToken = default)
    {
        var fields = (await db.FieldMasters.Select(x => x.FieldKey).ToListAsync(cancellationToken))
            .ToHashSet();

        var rarityWeights = (await db.FieldRarityRateMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.FieldKey)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new RarityWeight(x.Rarity, x.RarityRate)).ToList());

        var groupsByField = (await db.FieldEnemyGroupMasters.ToListAsync(cancellationToken))
            .GroupBy(x => (x.FieldKey, x.Rarity))
            .ToDictionary(g => g.Key, g => g.Select(x => x.GroupKey).ToList());

        var transitions = (await db.FieldTransitionMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.FieldKey)
            .ToDictionary(g => g.Key, g => g.Select(x => x.NextFieldKey).ToList());

        var groupMembers = (await db.EnemyGroupMemberMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.GroupKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.MemberIndex)
                    .Select(x => new EnemyGroupMember(x.MemberIndex, x.EnemyKey))
                    .ToList());

        var enemies = (await db.EnemyMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => x.EnemyKey);

        var equipment = (await db.EquipmentMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => x.EquipKey);

        // 抽選順は enemy_equipment_index 昇順。部位が競合したとき先に引かれた候補が部位を取るため、
        // この順序は結果を左右する（乱数を追加で消費しない決定的なタイブレークになっている）
        var equipmentOptions = (await db.EnemyEquipmentMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.EnemyKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.EnemyEquipmentIndex)
                    .Where(x => equipment.ContainsKey(x.EquipKey))
                    .Select(x => ToOption(x, equipment[x.EquipKey]))
                    .ToList());

        var autoEffects = (await db.EnemyEffectsMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.EnemyKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.EnemyEffectIndex)
                    .Select(x => new EnemyAutoEffect(
                        x.EffectKey, x.GrantRate, x.EffectRate, x.AttackType, x.DurationActions))
                    .ToList());

        return new DatabaseWorldCatalog(
            fields, rarityWeights, groupsByField, transitions,
            groupMembers, enemies, equipmentOptions, autoEffects, skills);
    }

    public bool HasField(string fieldKey) => fields.Contains(fieldKey);

    public IReadOnlyList<RarityWeight> RarityWeightsOf(string fieldKey)
        => rarityWeights.TryGetValue(fieldKey, out var value) ? value : [];

    public IReadOnlyList<string> GroupsOf(string fieldKey, Rarity rarity)
        => groupsByField.TryGetValue((fieldKey, rarity), out var value) ? value : [];

    public IReadOnlyList<string> TransitionsFrom(string fieldKey)
        => transitions.TryGetValue(fieldKey, out var value) ? value : [];

    public IReadOnlyList<EnemyGroupMember> MembersOf(string groupKey)
        => groupMembers.TryGetValue(groupKey, out var value) ? value : [];

    public IReadOnlyList<EnemyEquipmentOption> EquipmentOptionsOf(string enemyKey)
        => equipmentOptions.TryGetValue(enemyKey, out var value) ? value : [];

    public IReadOnlyList<EnemyAutoEffect> AutoEffectsOf(string enemyKey)
        => autoEffects.TryGetValue(enemyKey, out var value) ? value : [];

    /// <summary>
    /// 敵マスタから出現インスタンスを作る。保有スキルは本型が保持する
    /// <see cref="SkillCatalog"/> から供給される。
    /// </summary>
    public Enemy CreateEnemy(string enemyKey, BigInteger level) => CreateEnemy(enemyKey, level, null);

    /// <summary>
    /// 識別子を指定して敵を作る。既存の出現インスタンスを参加者行から復元する経路で使う。
    /// </summary>
    /// <param name="entityId">
    /// 参加者行の <c>entity_id</c>。<c>CurrentTarget</c> と台帳の帰属がこのIDで解決されるため、
    /// 戦闘中は参加者と実体の識別子を一致させる必要がある。
    /// </param>
    public Enemy CreateEnemy(string enemyKey, BigInteger level, Guid? entityId)
    {
        if (!enemies.TryGetValue(enemyKey, out var master))
        {
            throw new InvalidOperationException($"敵マスタに {enemyKey} が存在しない。");
        }

        return new Enemy(
            masterKey: master.EnemyKey,
            name: master.Name,
            level: level,
            shape: new StatShape(
                master.HpShape, master.PAtkShape, master.PDefShape, master.MAtkShape, master.MDefShape),
            strengthRate: master.StrengthRate,
            expRate: master.ExpRate,
            baseSpeed: master.Speed,
            innateElements: master.Elements,
            initialTp: master.InitialTp,
            actionPatternType: master.ActionPatternType,
            allyTargetRule: master.AllyTargetRule,
            skills: skills?.SkillsOf(enemyKey),
            entityId: entityId);
    }

    private static EnemyEquipmentOption ToOption(
        EnemyEquipmentMasterRecord entry, EquipmentMasterRecord equipment)
        => new(
            entry.EquipKey,
            entry.EquipRate,
            entry.DropRate,
            equipment.EquipParts,
            new EquipmentBonus(
                equipment.ProgressionValue,
                equipment.Rarity,
                equipment.HpRate,
                equipment.PAtkRate,
                equipment.PDefRate,
                equipment.MAtkRate,
                equipment.MDefRate,
                equipment.SpeedBonus,
                equipment.LuckBonusRate,
                equipment.Elements));
}
