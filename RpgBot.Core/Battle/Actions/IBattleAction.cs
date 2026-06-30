using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RpgBot.Core.Battle.Actions;

public interface IBattleAction
{
    ActionType Type { get; }

    Task<BattleActionResult> ExecuteAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng);
}
