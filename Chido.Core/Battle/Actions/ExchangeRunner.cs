using System;
using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// 「行動者と対象、双方が打ち合う1回の交換」の共通解決ロジック。
/// Speed の高い側が先攻し (同速なら行動者を先攻とする)、先攻の一撃で後攻が戦闘不能になった場合は
/// 後攻の行動をキャンセルする (4.1)。
/// </summary>
internal static class ExchangeRunner
{
    public delegate (BigInteger Damage, string Log) Strike(IEntity attacker, IEntity defender, Random rng);

    public static List<string> Run(
        BattleParticipant actor,
        Strike            actorStrike,
        BattleParticipant target,
        Strike            targetStrike,
        Random            rng)
    {
        var logs = new List<string>();

        bool actorFirst = actor.Entity.Speed >= target.Entity.Speed;
        var (firstP, firstStrike, secondP, secondStrike) = actorFirst
            ? (actor, actorStrike, target, targetStrike)
            : (target, targetStrike, actor, actorStrike);

        var (_, log1) = firstStrike(firstP.Entity, secondP.Entity, rng);
        logs.Add(log1);

        if (!secondP.Entity.IsAlive)
        {
            secondP.MarkDefeated();
            logs.Add($"{secondP.Entity.Name} は戦闘不能になった！");
            return logs; // 後攻の行動はキャンセル
        }

        var (_, log2) = secondStrike(secondP.Entity, firstP.Entity, rng);
        logs.Add(log2);

        if (!firstP.Entity.IsAlive)
        {
            firstP.MarkDefeated();
            logs.Add($"{firstP.Entity.Name} は戦闘不能になった！");
        }

        return logs;
    }
}
