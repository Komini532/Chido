using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chido.Core.Battle.Actions;

/// <summary>
/// プレイヤーの戦闘行為1回分。共有ターンキューは存在せず、行動したプレイヤーと
/// その対象の敵1体との間で完結する「交換」として扱う。
/// 実装は、プレイヤー側の行動結果を計算した後、続けて対象の敵参加者による
/// 反撃（行動したプレイヤー本人を狙うことを基本方針とする）を同一呼び出し内で
/// 計算し、両方のログをまとめて <see cref="BattleActionResult.LogEntries"/> に返すこと。
/// </summary>
public interface IBattleAction
{
    ActionType Type { get; }

    Task<BattleActionResult> ExecuteAsync(
        BattleParticipant                actor,
        IReadOnlyList<BattleParticipant>  participants,
        BattleSession                     session,
        Random                            rng);
}
