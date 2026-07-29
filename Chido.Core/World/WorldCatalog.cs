using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Stats;

namespace Chido.Core.World;

/// <summary>
/// フィールドのマスタ参照（戦闘システム 10.3・10.4）。
///
/// 抽選ロジックそのものは <see cref="GroupDraw"/> / <see cref="FieldTransition"/> に純粋関数として置き、
/// データの出所を本インターフェイスに切り出している。これにより抽選の規則は
/// DBを用意せずに検証でき、Discord にもEF Coreにも依存しない。
/// </summary>
public interface IFieldCatalog
{
    /// <summary>フィールドが存在するか。起動時検証が参照する。</summary>
    bool HasField(string fieldKey);

    /// <summary>
    /// レアリティ抽選率（10.3 の1段目）。フィールド単位で個別に持つ。
    /// <see cref="Rarity.Hidden"/> はイベント専用であり、この表にデータとして存在させない。
    /// </summary>
    IReadOnlyList<RarityWeight> RarityWeightsOf(string fieldKey);

    /// <summary>フィールドに紐づく、指定レアリティの組（10.3 の2段目の候補）。</summary>
    IReadOnlyList<string> GroupsOf(string fieldKey, Rarity rarity);

    /// <summary>遷移先候補。行を持たない（行き止まりの）フィールドでは空になる。</summary>
    IReadOnlyList<string> TransitionsFrom(string fieldKey);
}

/// <summary>
/// 敵の組とその構成要素のマスタ参照（戦闘システム 10.3）。
/// </summary>
public interface IEnemyCatalog
{
    /// <summary>組の構成メンバー。<c>member_index</c> 昇順。</summary>
    IReadOnlyList<EnemyGroupMember> MembersOf(string groupKey);

    /// <summary>
    /// 敵マスタから素のインスタンスを1体作る。装備・状態変化は含まない
    /// （それらは <see cref="GroupSpawner"/> が抽選・適用する）。
    /// </summary>
    /// <param name="level">現在の累積敵レベル。組の全メンバーが同一レベルになる。</param>
    Enemy CreateEnemy(string enemyKey, BigInteger level);

    /// <summary>装備の抽選候補。出現の都度、各候補が <c>equip_rate</c> で抽選される。</summary>
    IReadOnlyList<EnemyEquipmentOption> EquipmentOptionsOf(string enemyKey);

    /// <summary>出現時に auto 付与される状態変化の候補。各候補が <c>grant_rate</c> で抽選される。</summary>
    IReadOnlyList<EnemyAutoEffect> AutoEffectsOf(string enemyKey);
}

/// <summary>レアリティ抽選率1行（chido_field_rarity_rate_master）。</summary>
public readonly record struct RarityWeight(Rarity Rarity, Ratio Rate);

/// <summary>組の構成メンバー1行（chido_enemy_group_member_master）。</summary>
/// <param name="MemberIndex">組内の位置。出現時に <c>spawn_index</c> と表示順へ恒等複製される。</param>
public readonly record struct EnemyGroupMember(byte MemberIndex, string EnemyKey);

/// <summary>
/// 敵の装備候補1行（chido_enemy_equipment_master と装備マスタの結合）。
/// </summary>
/// <param name="EquipRate">この装備を身につけている確率。出現の都度抽選される。</param>
/// <param name="DropRate">撃破時のドロップ率。報酬（6.2）で参照するため出現時から保持する。</param>
/// <param name="Parts">装着可能部位（ビット列）。複数部位に適合する装備がありうる。</param>
public readonly record struct EnemyEquipmentOption(
    string EquipKey,
    Ratio EquipRate,
    Ratio DropRate,
    EquipPart Parts,
    EquipmentBonus Bonus);

/// <summary>
/// 敵の初期付与状態変化1行（chido_enemy_effects_master）。
/// </summary>
/// <param name="GrantRate">付与そのものの確率。<paramref name="EffectRate"/> は付与された場合の変動量。</param>
/// <param name="DurationActions">
/// 持続。付与モーション側ではなく auto 付与側が持つため、
/// 「6行動で自滅する敵」のような表現がここから成立する。
/// </param>
public readonly record struct EnemyAutoEffect(
    string EffectKey,
    Ratio GrantRate,
    Ratio EffectRate,
    AttackType? AttackType,
    ushort? DurationActions);
