using System.Numerics;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Chido.Core.World;
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

    private DatabaseWorldCatalog(
        HashSet<string> fields,
        Dictionary<string, List<RarityWeight>> rarityWeights,
        Dictionary<(string, Rarity), List<string>> groupsByField,
        Dictionary<string, List<string>> transitions,
        Dictionary<string, List<EnemyGroupMember>> groupMembers,
        Dictionary<string, EnemyMasterRecord> enemies,
        Dictionary<string, List<EnemyEquipmentOption>> equipmentOptions,
        Dictionary<string, List<EnemyAutoEffect>> autoEffects)
    {
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
    public static async Task<DatabaseWorldCatalog> LoadAsync(
        ChidoDbContext db, CancellationToken cancellationToken = default)
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
            groupMembers, enemies, equipmentOptions, autoEffects);
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
    /// 敵マスタから素のインスタンスを作る。
    ///
    /// 保有スキルは <c>chido_enemy_skills_master</c> にあるが、スキル本体
    /// （<see cref="Chido.Core.Battle.Skills.Skill"/>）の組み立てはモーションのサブタイプ4種の
    /// 読み出しを伴うため、スキルマスタの投入と合わせて別途供給する。
    /// </summary>
    public Enemy CreateEnemy(string enemyKey, BigInteger level)
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
            allyTargetRule: master.AllyTargetRule);
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
