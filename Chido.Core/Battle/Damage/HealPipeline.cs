using System.Numerics;
using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 回復パイプライン（戦闘システム 5.1）。
///
/// <code>
/// 回復量 = floor(有効ATK × power ÷ 100)     （下限0）
/// </code>
///
/// かつては攻撃と1本のパイプラインを共有し、回復側で属性・クリティカル・被防御係数を
/// 「登録しない例外」として扱っていたが、例外処理のメンテナンスコストが共通化による負担軽減を
/// 上回るため分離された。Modifier のパイプラインを通らないのはこのため。
///
/// <b>属性補正・クリティカル・対象DEF・DRR のいずれも適用しない。</b>
/// <list type="bullet">
///   <item>対象DEF … 回復は「防御されない攻撃」であり突き合わせる相手が味方である。
///         適用すると「硬い味方ほど回復しづらい」という直感に反する</item>
///   <item>対象の最大HP基準も採らない … 回復量が術者のレベル・装備から独立してしまい、
///         2.3 の原則の外に飛び出すため</item>
///   <item>クリティカル … 適用対象は「最終ダメージへの乗算」であり回復量は最終ダメージではない。
///         「会心の回復」は認めない（戦闘システム 5.2）</item>
/// </list>
///
/// <b>下限は0</b>（攻撃・スリップの「最低1」とは異なり、丸めで0になる回復は0のままとする）。
///
/// 較正上、同格では被防御係数が 0.5 になるため、通常攻撃（威力100%）と釣り合う回復は威力50%。
/// </summary>
public static class HealPipeline
{
    /// <summary>
    /// 回復モーション1回分の回復量を算出する。HPへの適用は呼び出し側が行う。
    /// </summary>
    /// <param name="healer">術者。有効ATKの供給元。対象のステータスは一切参照しない。</param>
    /// <param name="attackType">参照する攻撃力（物理／魔法）。攻撃時と同じく attack_type で選択する。</param>
    /// <param name="power">威力。整数%。</param>
    public static BigInteger Resolve(IEntity healer, AttackType attackType, int power)
    {
        var effectiveAtk = attackType == AttackType.Physical ? healer.PAtk : healer.MAtk;

        var amount = BigIntegerMath.FloorDiv(effectiveAtk * power, GameConstants.PowerScale);

        return BigInteger.Max(BigInteger.Zero, amount);
    }
}
