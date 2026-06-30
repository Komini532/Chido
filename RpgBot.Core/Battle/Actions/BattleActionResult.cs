using System.Collections.Generic;

namespace RpgBot.Core.Battle.Actions;

public sealed record BattleActionResult(
    bool                  SessionEnded,
    BattleEndReason?      EndReason,
    IReadOnlyList<string> LogEntries
);
