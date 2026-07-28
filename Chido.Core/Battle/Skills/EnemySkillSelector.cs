using System;
using System.Collections.Generic;
using System.Linq;
using Chido.Core.Entities.Enemies;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// 敵が反撃に使うスキルの選択（戦闘システム 4.2）。
///
/// <b>選択は行動順の決定より前に済んでいる必要がある。</b>
/// 行動順は Speed だけでは決まらず両者の選択スキルの Priority を参照するため、
/// スキルが確定していないと 4.1 の行動順決定そのものが成立しない。
///
/// <c>require_tp</c> を払えないスキルの扱いは <see cref="ActionPatternType"/> により分岐する。
/// ランダム系で「払えないスキルも抽選対象に含め、当たってから通常攻撃に落とす」方式を採ると、
/// 存在するはずのないエントリが抽選される不自然さが生じるため、払えるものだけでプールを構成する。
/// ローテーションは順序そのものに意味があるため、払えないときに「飛ばして次へ」進めると順序が崩れる。
/// よって選択自体はローテ順を維持し、出力だけを通常攻撃へ差し替える。
/// </summary>
/// <param name="attackSkill">
/// フォールバック先の通常攻撃。<b>登録された通常攻撃とは別物</b>である。
/// 登録された通常攻撃はローテ枠を1つ占め（total に数えられる）抽選候補にもなるが、
/// フォールバックの通常攻撃は枠を持たず、その回の出力だけが差し替わる。
/// 両者はいずれも「Attackモーションの再生」でTPを蓄積するため、
/// どちらのAttackかを判定する分岐は下流のどこにも要らない（4.4）。
/// </param>
public sealed class EnemySkillSelector(Skill attackSkill)
{
    /// <summary>
    /// 反撃スキルを1本決める。<b>ローテーションの前進という副作用を伴う</b>ため、
    /// 1ターンにつき1回だけ呼ぶこと。
    /// </summary>
    public Skill Select(BattleParticipant enemy, Random rng)
    {
        if (enemy.Entity is not Enemy master) return attackSkill;

        var entries = master.Skills;

        // 1つもスキルを保有しない敵は通常攻撃を行う
        if (entries.Count == 0) return attackSkill;

        return master.ActionPatternType switch
        {
            ActionPatternType.PureRandom => PickUniform(entries, enemy, rng),
            ActionPatternType.WeightedRandom => PickWeighted(entries, enemy, rng),
            ActionPatternType.Rotation => PickRotation(entries, enemy),

            _ => throw new ArgumentOutOfRangeException(
                nameof(enemy), master.ActionPatternType, "未知の行動パターン。"),
        };
    }

    /// <summary>
    /// require_tp を満たすスキルのみのプールから等確率。プールが空なら通常攻撃へ。
    /// weight は参照しない（本パターンでは weight = 0 のスキルも通常通り使用される）。
    /// </summary>
    private Skill PickUniform(IReadOnlyList<EnemySkillEntry> entries, BattleParticipant enemy, Random rng)
    {
        var pool = entries.Where(e => enemy.CanAfford(e.Skill.RequireTp)).ToList();

        return pool.Count == 0 ? attackSkill : pool[rng.Next(pool.Count)].Skill;
    }

    /// <summary>
    /// require_tp を満たすスキルのみのプールを、<b>残存エントリの weight をそのまま用いて
    /// その合計で正規化して</b>抽選する。プールが空なら通常攻撃へ。
    ///
    /// weight = 0 は本パターンにおいてのみ「抽選対象外」を意味する。
    /// 保有スキルの weight がすべて 0 でプールが空になる場合も通常攻撃へフォールバックする。
    /// </summary>
    private Skill PickWeighted(IReadOnlyList<EnemySkillEntry> entries, BattleParticipant enemy, Random rng)
    {
        var pool = entries.Where(e => e.Weight > 0 && enemy.CanAfford(e.Skill.RequireTp)).ToList();
        if (pool.Count == 0) return attackSkill;

        var total = pool.Sum(e => (int)e.Weight);
        var roll = rng.Next(total);

        foreach (var entry in pool)
        {
            roll -= entry.Weight;
            if (roll < 0) return entry.Skill;
        }

        // 合計を厳密に消費しきるため到達しない
        return pool[^1].Skill;
    }

    /// <summary>
    /// rotation_index の位置のスキルを選び、位置を1つ進める。
    ///
    /// <b>払えない場合も順番は飛ばさない。</b>選択自体はローテ順を維持し、
    /// その回の出力だけが通常攻撃に差し替わる。rotation_index は
    /// 本来選ばれるはずだったスキルの位置として前進する。
    /// </summary>
    private Skill PickRotation(IReadOnlyList<EnemySkillEntry> entries, BattleParticipant enemy)
    {
        // 登録スキル数は戦闘中不変だが、格納値が範囲外でも安全側に倒す
        var index = enemy.RotationIndex % entries.Count;
        var selected = entries[index].Skill;

        enemy.AdvanceRotation(entries.Count);

        return enemy.CanAfford(selected.RequireTp) ? selected : attackSkill;
    }
}
