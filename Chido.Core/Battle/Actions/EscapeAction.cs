using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 離脱。反撃を受けずに一方的にセッションから離脱する。離脱後は同じ戦闘に再参加できない (4.3)。
/// </summary>
public sealed class EscapeAction : BattleActionBase
{
    public override ActionType Type => ActionType.Escape;

    protected override Task<BattleActionResult> ExecuteCoreAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng)
    {
        actor.MarkEscaped();
        session.RecordAction();

        var logs = new List<string> { $"{actor.Entity.Name} は戦闘から離脱した。" };
        return Task.FromResult(Conclude(session, logs));
    }
}
