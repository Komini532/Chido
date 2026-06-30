using System.Collections.Generic;

namespace Chido.Core.Battle.Actions;

public sealed record BattleActionResult(
    bool                  SessionEnded,
    BattleEndReason?      EndReason,
    IReadOnlyList<string> LogEntries
);
