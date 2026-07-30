using System;
using Chido.Core.Stats;

namespace Chido.Core.Rewards;

/// <summary>
/// ドロップ判定と Luck による再抽選（戦闘システム 10.2）。
///
/// <code>
/// 1. まず drop_rate でドロップ判定を行う
/// 2. 「ドロップなし」だった場合のみ、Luck が1%以上であれば Luck% の確率で「再抽選の権利」が発生する
/// 3. 権利が発生したら同一の drop_rate でもう一度判定し、その結果で最終判定を上書きする（再抽選は1度だけ）
/// 4. 複数プレイヤーが関与した場合、判定はプレイヤーごとに各自の Luck で独立に行われる
/// </code>
///
/// Luck = 0%（装備なしの初期状態）では再抽選の権利が発生せず、通常の drop_rate のみで決まる。
/// </summary>
public static class DropRoll
{
    /// <summary>再抽選の権利が発生しうる Luck の下限（1%）。これ未満は権利そのものが発生しない。</summary>
    public static readonly Ratio MinimumLuck = Ratio.FromPercent(1m);

    /// <summary>
    /// Luck を使用時点でクランプする。
    ///
    /// <b>ステータスとしての Luck はクランプしない</b>（装備の加算は正負どちらも取り、
    /// レイヤー内加算の結果が定義域を外れうる）。定義域を強制するのは<b>使用時点のみ</b>とし、
    /// 負値は0%・100%超は100%へ寄せる。DRR の係数を途中でクランプせず最終段で吸収するのと同じ形であり、
    /// 「途中の合成は素直に足し、意味を持つ地点で初めて丸める」という一貫した扱いになる。
    ///
    /// 負の Luck は「再抽選が起きない」だけであり、1段目の drop_rate には干渉しない。
    /// 干渉させると Luck が実質的に2つの意味を持つことになり、装備の説明が破綻する。
    /// </summary>
    public static Ratio ClampLuck(Ratio luck)
    {
        if (luck < Ratio.Zero) return Ratio.Zero;
        if (luck > Ratio.Full) return Ratio.Full;

        return luck;
    }

    /// <summary>
    /// ドロップするかを判定する。
    /// </summary>
    /// <param name="dropRate">アイテム／装備ごとのドロップ率。</param>
    /// <param name="luck">プレイヤーの Luck。<see cref="ClampLuck"/> は内部で適用する。</param>
    public static bool Roll(Ratio dropRate, Ratio luck, Random rng)
    {
        if (dropRate.Roll(rng)) return true;

        var clamped = ClampLuck(luck);

        // 1%未満では権利そのものが発生しない。0% を素の確率として引くと、
        // 乱数の消費数が Luck の有無で変わり、同一シードでの再現性が Luck に依存してしまう
        if (clamped < MinimumLuck) return false;

        if (!clamped.Roll(rng)) return false;

        // 再抽選は1度だけ。結果は成否を問わず最終判定を上書きする
        return dropRate.Roll(rng);
    }
}
