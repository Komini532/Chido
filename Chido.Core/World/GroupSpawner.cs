using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Effects;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Stats;

namespace Chido.Core.World;

/// <summary>
/// 敵の組の生成（戦闘システム 10.3 の <c>SpawnGroup</c>）。
///
/// <b>呼び出し元・組が同一かどうかを問わず、常に新規生成する。</b>
/// 「前のインスタンスを引き継ぐ」経路は存在しない。
///
/// | 項目 | 値 |
/// |---|---|
/// | entity_id | 新規 Guid |
/// | レベル | 現在の累積敵レベル（組の全メンバー共通） |
/// | CurrentLife | MaxLife（全快） |
/// | 装備 | chido_enemy_equipment_master から再抽選 |
/// | 状態変化 | chido_enemy_effects_master の auto 付与を再適用 |
/// | current_tp | chido_enemy_master.initial_tp |
/// | rotation_index | 0 |
/// | spawn_index | 組の member_index を複製 |
///
/// <c>PlayerEscaped</c>（前組が Common/Uncommon）で「同一の組が再出現」する場合も本処理を通るため、
/// HP全快・装備再抽選・状態変化の再適用が行われる。前セッションで敵が保持していた状態変化は
/// セッション終了時に除去されるため、再生成は常にクリーンな状態から始まる。
/// </summary>
/// <param name="effectApplier">
/// auto 付与を行う。未提供なら状態変化を適用しない（マスタ未投入の段階でも組の生成は成立する）。
/// </param>
public sealed class GroupSpawner(IEnemyCatalog catalog, EffectApplier? effectApplier = null)
{
    public IReadOnlyList<SpawnedEnemy> Spawn(string groupKey, BigInteger level, Random rng)
    {
        var members = catalog.MembersOf(groupKey);

        if (members.Count == 0)
        {
            throw new InvalidOperationException(
                $"組 {groupKey} にメンバーが1体も登録されていない。");
        }

        return members
            .OrderBy(m => m.MemberIndex)
            .Select(member => SpawnMember(member, level, rng))
            .ToList();
    }

    private SpawnedEnemy SpawnMember(EnemyGroupMember member, BigInteger level, Random rng)
    {
        var enemy = catalog.CreateEnemy(member.EnemyKey, level);

        var equipment = DrawEquipment(member.EnemyKey, rng);
        enemy.SetEquipment(equipment.Select(e => e.Option.Bonus));

        ApplyAutoEffects(member.EnemyKey, enemy, rng);

        // 全快は装備と状態変化を載せた後に行う。最大HPは動的算出であり、
        // 先に全快させると装備ぶんが乗る前の値で固定されてしまう
        enemy.RestoreToFull();

        return new SpawnedEnemy(member.MemberIndex, enemy, equipment);
    }

    /// <summary>
    /// 装備の再抽選。各候補を <c>equip_rate</c> で独立に引き、当たったものを装着可能な部位へ収める。
    ///
    /// <b>1部位につき1つまで</b>とし、競合した場合は先に引かれた候補（<c>enemy_equipment_index</c> 順）が
    /// 部位を取る。複数部位に適合する装備は、空いている部位のうち最も小さいものへ入る。
    /// 部位の競合解決は設計に明示がないため、抽選順という既に決まっている順序に委ねている
    /// （乱数を追加で消費しないため、同じシードなら結果が再現する）。
    /// </summary>
    private List<SpawnedEquipment> DrawEquipment(string enemyKey, Random rng)
    {
        var result = new List<SpawnedEquipment>();
        var occupied = EquipPart.None;

        foreach (var option in catalog.EquipmentOptionsOf(enemyKey))
        {
            if (!option.EquipRate.Roll(rng)) continue;

            var slot = FirstFreePart(option.Parts, occupied);
            if (slot == EquipPart.None) continue;

            occupied |= slot;
            result.Add(new SpawnedEquipment(slot, option));
        }

        return result;
    }

    /// <summary>装着可能部位のうち、まだ埋まっていない最も小さいビットを返す。</summary>
    private static EquipPart FirstFreePart(EquipPart parts, EquipPart occupied)
    {
        foreach (var part in Enum.GetValues<EquipPart>())
        {
            if (part == EquipPart.None) continue;
            if ((parts & part) == 0) continue;
            if ((occupied & part) != 0) continue;

            return part;
        }

        return EquipPart.None;
    }

    /// <summary>
    /// auto 付与の再適用。各候補を <c>grant_rate</c> で引き、当たったものを付与する。
    /// 付与者は自身であり、<c>grant_source_key</c> は NULL になる。
    /// </summary>
    private void ApplyAutoEffects(string enemyKey, Enemy enemy, Random rng)
    {
        if (effectApplier is null) return;

        foreach (var auto in catalog.AutoEffectsOf(enemyKey))
        {
            if (!auto.GrantRate.Roll(rng)) continue;

            effectApplier.GrantAuto(
                enemy, EntityType.Enemy, auto.EffectKey,
                auto.EffectRate, auto.AttackType, auto.DurationActions);
        }
    }
}

/// <summary>
/// 生成された敵1体。
/// </summary>
/// <param name="SpawnIndex">
/// 組の <c>member_index</c> の恒等複製。表示順（<c>display_order</c>）の唯一の根拠でもある。
/// </param>
/// <param name="Equipment">
/// 抽選された装備。ドロップ率を保持しているため、撃破時の報酬計算（6.2）が
/// 出現時に確定した内容をそのまま参照できる。
/// </param>
public sealed record SpawnedEnemy(
    byte SpawnIndex,
    Enemy Enemy,
    IReadOnlyList<SpawnedEquipment> Equipment);

/// <summary>装着された装備1つ。部位は出現時に確定し、セッション中に変化しない。</summary>
public readonly record struct SpawnedEquipment(EquipPart Part, EnemyEquipmentOption Option);
