using System.Collections.Generic;
using System.Linq;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 残り有効行動数の減衰（戦闘システム 5.4 / A-8）。
///
/// <b>減衰の対象は関与者集合</b>＝ターン開始時（行動が受理され、反撃者が確定した時点）に固定される
/// 「行動者と反撃者の2エンティティ」のみ。<c>Use</c>・味方対象モーションで効果を受けた第三者、
/// 組の他の敵、同チャンネルの他プレイヤーは減衰しない。味方に付与したバフは、
/// その味方自身が行動するまで残り有効行動数を保つ（弾は撃つから減る、という比喩と一貫する）。
///
/// <b>集合はターン中の出来事で変化しない</b>。<see cref="ParticipantStatus.Defeated"/>／
/// <see cref="ParticipantStatus.Escaped"/> 化、行動キャンセル、モーション中断、命中失敗、
/// 行動不能（DisableMove）の成立は、いずれも集合を変えない。したがって本関数は参加者の状態を見ない。
///
/// 減衰は両スコープ（chido_battle_effect / chido_player_effect）に一律で及ぶ。
/// <see cref="EntityBase.Effects"/> が両者を1つのリストで保持しているため、ここでの区別は不要。
/// 無期限（remaining_actions = NULL）のインスタンスは対象外であり、
/// SQL の <c>NULL - 1 = NULL</c> / <c>WHERE remaining_actions = 0</c> が UNKNOWN を返す挙動と一致する。
/// </summary>
public static class EffectDecay
{
    /// <summary>
    /// 関与者集合の残り有効行動数を1つずつ消費し、使い切ったインスタンスを取り除く。
    ///
    /// 減算と削除を分けずここで完結させるのは、remaining_actions = 0 の行が
    /// 他から観測されないようにするため（永続化層では同一トランザクション内で行う）。
    /// </summary>
    /// <returns>取り除かれたインスタンス。通知の材料になる。</returns>
    public static IReadOnlyList<(BattleParticipant Holder, EffectInstance Effect)> Apply(
        params BattleParticipant[] participantSet)
        => Apply((IEnumerable<BattleParticipant>)participantSet);

    public static IReadOnlyList<(BattleParticipant Holder, EffectInstance Effect)> Apply(
        IEnumerable<BattleParticipant> participantSet)
    {
        var expired = new List<(BattleParticipant, EffectInstance)>();

        // 行動者と反撃者が同一エンティティになる経路は現行では存在しないが、
        // 重複して渡された場合に二重減衰させない（集合であって多重集合ではない）
        foreach (var holder in participantSet.Distinct())
        {
            if (holder.Entity is not EntityBase entity) continue;

            // 減衰中に取り除くため、列挙前に確定させる
            foreach (var effect in entity.Effects.ToList())
            {
                effect.Decay();

                if (!effect.IsExpired) continue;

                entity.RemoveEffect(effect);
                expired.Add((holder, effect));
            }
        }

        return expired;
    }
}
