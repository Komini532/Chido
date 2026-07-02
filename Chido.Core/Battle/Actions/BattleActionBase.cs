using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// IBattleAction の共通の前提条件 (離脱・戦闘不能済みの参加者は行動できない) と、
/// 行動後のセッション終了判定をまとめた基底クラス。
/// </summary>
public abstract class BattleActionBase : IBattleAction
{
    public abstract ActionType Type { get; }

    public Task<BattleActionResult> ExecuteAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        if (!actor.IsActive)
        {
            return Task.FromResult(new BattleActionResult(
                false, null,
                new[] { $"{actor.Entity.Name} は既に離脱/戦闘不能のため行動できない。" }));
        }

        return ExecuteCoreAsync(actor, participants, session, rng);
    }

    protected abstract Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng);

    protected static BattleActionResult Conclude(BattleSession session, IReadOnlyList<string> logs)
    {
        var (ended, reason) = session.CheckEndCondition();
        if (ended) session.Finish(reason);
        return new BattleActionResult(ended, ended ? reason : null, logs);
    }
}
