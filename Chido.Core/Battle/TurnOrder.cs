using System;
using Chido.Core.Battle.Skills;

namespace Chido.Core.Battle;

/// <summary>
/// 1回のターンに関与する2エンティティの先攻・後攻の決定（戦闘システム 4.1）。
///
/// <code>
/// OrderBy(Priority) → ThenBy(Speed) → ThenBy(Random)   （いずれも降順・先攻が先）
/// </code>
///
/// 共有ターンキューが存在しないため、この順序は「そのターン内の先攻・後攻」に限定される。
///
/// <b>Priority を導入する理由</b>: Speed のみで決めると Defend の有効性が Speed だけで決まる。
/// Defend は「相手の攻撃を受ける前に軽減の構えを取る」行動であり、鈍足側が Defend しても
/// 先に被弾してから構えが立つため無意味になる。これはプレイヤー・敵の双方に対称に生じる欠陥であり、
/// Defend に高い Priority を与えることで解消される。
/// </summary>
public static class TurnOrder
{
    /// <summary>
    /// 先攻・後攻を決める。
    ///
    /// <b>乱数はそのターンにつき1回だけ引く。</b> 引いた結果は行動の実行順とログの整形順の両方に使う。
    /// 行動順を決める抽選とログの並び順を別々に引くと、ログが実際の処理順とずれるため。
    /// Priority と Speed のいずれかで決着した場合は抽選そのものを行わない。
    ///
    /// なお、これは行動順のタイブレークであり、埋め込みの表示ソート（DisplayOrder による安定ソート）
    /// とは別レイヤーである。
    /// </summary>
    public static (TurnSide First, TurnSide Second) Decide(TurnSide a, TurnSide b, Random rng)
    {
        var byPriority = b.Skill.Priority.CompareTo(a.Skill.Priority);
        if (byPriority != 0) return byPriority < 0 ? (a, b) : (b, a);

        var bySpeed = b.Participant.Entity.Speed.CompareTo(a.Participant.Entity.Speed);
        if (bySpeed != 0) return bySpeed < 0 ? (a, b) : (b, a);

        return rng.Next(2) == 0 ? (a, b) : (b, a);
    }
}

/// <summary>ターンに関与する一方の側（行動者と、その行動で使うスキル）。</summary>
public sealed record TurnSide(BattleParticipant Participant, Skill Skill);
