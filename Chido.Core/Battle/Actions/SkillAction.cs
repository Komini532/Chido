using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chido.Core.Battle.Damage;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// スキル発動。Attack と同じ「交換」モデルで処理されるが、行動者側の一撃にスキル倍率・
/// 任意の攻撃種別 (物理/魔法) が乗る点のみ異なる (4.2)。
/// スキルのコスト制・マスタデータ構造は未確定 (設計ドキュメント 10.3) のため、
/// 呼び出し側 (スキルマスタ参照層) が解決した内容をそのままコンストラクタで受け取る。
/// </summary>
public sealed class SkillAction : BattleActionBase
{
    private readonly string     _skillName;
    private readonly AttackType _attackType;
    private readonly int        _power;
    private readonly Element    _motionElements;

    public SkillAction(string skillName, AttackType attackType, int power, Element motionElements = Element.None)
    {
        _skillName      = skillName;
        _attackType     = attackType;
        _power          = power;
        _motionElements = motionElements;
    }

    public override ActionType Type => ActionType.Skill;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        var target = session.ResolveTarget(actor);
        if (target is null)
        {
            return Task.FromResult(new BattleActionResult(
                false, null, new[] { $"{actor.Entity.Name} の攻撃対象が見つからない。" }));
        }

        session.RecordAction();

        var logs = ExchangeRunner.Run(
            actor,  (a, d, r) => AttackResolver.Resolve(a, d, _attackType, r, power: _power, motionElements: _motionElements, skillName: _skillName),
            target, (a, d, r) => AttackResolver.Resolve(a, d, AttackType.Physical, r),
            rng);

        return Task.FromResult(Conclude(session, logs));
    }
}
