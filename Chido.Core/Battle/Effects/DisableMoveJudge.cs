using System;
using System.Linq;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 行動不能の判定（戦闘システム 5.4 / A-7）。
///
/// 判定はエンティティごと・その行動の直前に1回行う。<c>disable_rate</c> は<b>毎回抽選</b>であり、
/// 付与時に固定しない。したがってインスタンス側に複製される値を持たない。
///
/// <b>判定は行動順決定の「後」に置く</b>（A-7-i）。前に置くとスキルが確定せず
/// <c>priority</c> が読めないため、4.1 の行動順決定そのものが崩れる。
///
/// <b>成否によらず常に行われること</b>: ターン消費・TP蓄積・相手の反撃・残り有効行動数の減衰。
/// 成立時にスキップされるのは<b>そのエンティティのスキル1本ぶんのモーション再生のみ</b>。
/// なお「TP蓄積は常に行われる」は「+100 も付与する」という意味ではない。4.4 の通り +100 の契機は
/// 「Attack/Defend モーションが効果適用に到達したこと」であり、行動不能ではモーションが
/// 再生されないため +100 は構造的に発生しない（行動不能に特別扱いを設けない、という趣旨と一致する）。
/// </summary>
public static class DisableMoveJudge
{
    /// <summary>
    /// 行動不能が成立するかを判定する。
    ///
    /// 併存する複数の DisableMove インスタンスは <b>instance_id 昇順に独立抽選し、
    /// 最初の成功で打ち切る</b>（A-7-f）。確率であるため <c>StatusModifier</c> の加算合成は適用しない。
    /// 打ち切りにより消費される乱数の個数が変わるため、抽選順は決定性の一部である。
    /// </summary>
    /// <returns>成立したインスタンス。成立しなければ null。</returns>
    public static EffectInstance? Judge(BattleParticipant participant, Random rng)
    {
        if (participant.Entity is not EntityBase entity) return null;

        var instances = entity.Effects
            .Where(e => e.Definition.DisableRate is not null)
            .OrderBy(e => e, EffectInstanceOrder.Instance);

        foreach (var instance in instances)
        {
            if (instance.Definition.DisableRate!.Value.Roll(rng)) return instance;
        }

        return null;
    }
}
