using System;
using System.Collections.Generic;
using System.Linq;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 属性相性の判定。有利 ×1.3・不利 ×0.76 (≒1.3の逆数) を、複数属性が絡む場合は
/// 1.3^x (x = 有利属性数 - 不利属性数) で合成する。
///
/// 相性表そのもの (どの属性が何に対して有利/不利か) は設計ドキュメント上も未確定 (10.1)。
/// ここでは会話中に例示された組み合わせのみを暫定登録し、それ以外は無相性 (等倍) として扱う。
/// 10×10 相性表が確定次第、以下のテーブルを拡充すること。
/// </summary>
public static class ElementAffinity
{
    public static readonly Ratio Advantage    = Ratio.FromMultiplier(1.3m);
    public static readonly Ratio Disadvantage = Ratio.FromMultiplier(0.76m);

    private static readonly (Element Attacker, Element Defender)[] AdvantagePairs =
    [
        (Element.Fire, Element.Water),
    ];

    private static readonly (Element Attacker, Element Defender)[] DisadvantagePairs =
    [
        (Element.Thunder, Element.Earth),
    ];

    /// <summary>
    /// 攻撃側の属性 (最大2つ想定) を防御側の属性それぞれと総当たりで判定し、合成した倍率を返す。
    /// 該当する組み合わせがなければ Ratio.Full (等倍) を返す。
    /// </summary>
    public static Ratio GetMultiplier(Element attackerElements, Element defenderElements)
    {
        var attackerFlags = Decompose(attackerElements).ToList();
        var defenderFlags = Decompose(defenderElements).ToList();

        int advantageCount = 0, disadvantageCount = 0;
        foreach (var attacker in attackerFlags)
        {
            foreach (var defender in defenderFlags)
            {
                if (AdvantagePairs.Contains((attacker, defender))) advantageCount++;
                else if (DisadvantagePairs.Contains((attacker, defender))) disadvantageCount++;
            }
        }

        var x = advantageCount - disadvantageCount;
        if (x == 0) return Ratio.Full;

        var multiplier = (decimal)Math.Pow(1.3, x);
        return Ratio.FromMultiplier(multiplier);
    }

    private static IEnumerable<Element> Decompose(Element flags)
    {
        foreach (Element e in Enum.GetValues<Element>())
        {
            if (e == Element.None) continue;
            if (flags.HasFlag(e)) yield return e;
        }
    }
}
