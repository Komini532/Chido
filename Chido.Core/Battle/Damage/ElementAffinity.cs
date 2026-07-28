using System;
using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 属性相性の判定（戦闘システム 5.3）。
///
///     属性補正倍率 = 1.3 ^ x
///     x = Σ(a ∈ モーション属性) Σ(d ∈ 実効属性) rel(a, d)      … 総当たりペアの合計
///     rel(a, d) = 有利 +1 / 不利 -1 / 等倍 0
///
/// 式は1本しかなく、属性の数によって分岐しない。単一属性も属性なしもこの式の特殊ケースにすぎない。
/// elements = 0 は総当たりの対象集合が空になるため x = 0 となり、Neutral 単体と同じ結果になる
/// （＝「属性なしなら相性計算を省略する」は仕様上の分岐ではなく実装上の最適化）。
///
/// 相性表はC#定数として保持し、DBマスタ化しない。属性の定義が [Flags] enum としてC#側にある以上、
/// DB化すると属性の定義（enum）と属性の関係（行）が二重管理になり、かつ属性追加時はどのみち
/// 再コンパイルが必要なため「再デプロイなしに変更できる」という唯一の実利が得られないため。
/// これはゲームルールの構造そのものであって、バランス調整用のパラメータではない。
/// </summary>
public static class ElementAffinity
{
    /// <summary>相性表の行・列の並び。戦闘システム 5.3 の表と同一の順序を保つこと。</summary>
    private static readonly Element[] Order =
    [
        Element.Fire,    // 火
        Element.Water,   // 水
        Element.Grass,   // 草
        Element.Earth,   // 土
        Element.Sky,     // 空
        Element.Thunder, // 雷
        Element.Ice,     // 氷
        Element.Light,   // 光
        Element.Dark,    // 闇
        Element.Neutral, // 無
    ];

    /// <summary>
    /// rel(攻撃側, 防御側)。行が攻撃側（モーション属性）、列が防御側（実効属性）。
    /// +1 = 有利 / -1 = 不利 / 0 = 等倍。並びは <see cref="Order"/> に対応する。
    ///
    /// 不変条件（単体テストで強制する）:
    ///   1. 完全反対称   … rel[a,b] == -rel[b,a]（「相互に有利」「片方だけ有利」は存在しない）
    ///   2. Neutral の行・列はすべて 0 … elements = 0 との等価性を保つために必要
    ///   3. 対角はすべて 0 … 同属性同士は等倍
    /// </summary>
    private static readonly sbyte[,] Table =
    {
        //        火   水   草   土   空   雷   氷   光   闇   無
        /* 火 */ {  0, -1, +1, -1, -1,  0, +1,  0, +1,  0 },
        /* 水 */ { +1,  0, -1, +1, +1, -1, -1,  0,  0,  0 },
        /* 草 */ { -1, +1,  0, +1, -1, +1, -1,  0, -1,  0 },
        /* 土 */ { +1, -1, -1,  0, -1, +1, +1, +1,  0,  0 },
        /* 空 */ { +1, -1, +1, +1,  0, -1, -1,  0, -1,  0 },
        /* 雷 */ {  0, +1, -1, -1, +1,  0, -1,  0,  0,  0 },
        /* 氷 */ { -1, +1, +1, -1, +1, +1,  0, -1,  0,  0 },
        /* 光 */ {  0,  0,  0, -1,  0,  0, +1,  0, +1,  0 },
        /* 闇 */ { -1,  0, +1,  0, +1,  0,  0, -1,  0,  0 },
        /* 無 */ {  0,  0,  0,  0,  0,  0,  0,  0,  0,  0 },
    };

    // 攻撃属性ごとに「有利を取れる防御属性のビットマスク」「不利を取られる防御属性のビットマスク」を
    // 前計算しておく。これにより x の算出が popcount 2回で済み、仕様文の直訳になる。
    private static readonly Element[] AdvantageMask = new Element[Order.Length];
    private static readonly Element[] DisadvantageMask = new Element[Order.Length];

    static ElementAffinity()
    {
        for (var a = 0; a < Order.Length; a++)
        {
            for (var d = 0; d < Order.Length; d++)
            {
                switch (Table[a, d])
                {
                    case > 0: AdvantageMask[a]    |= Order[d]; break;
                    case < 0: DisadvantageMask[a] |= Order[d]; break;
                }
            }
        }
    }

    /// <summary>
    /// 相性スコア x（= 有利ペア数 - 不利ペア数）を返す。
    /// 攻撃側は攻撃モーションのモーション属性、防御側は実効属性（本体 ∪ 装備 ∪ 一時付与）を渡す。
    /// どちらかが Element.None（空集合）なら総当たりの対象が無いため 0 を返す。
    /// </summary>
    public static int GetScore(Element attackerElements, Element defenderElements)
    {
        var defender = (int)defenderElements;
        if (defender == 0 || attackerElements == Element.None) return 0;

        var score = 0;
        for (var a = 0; a < Order.Length; a++)
        {
            if ((attackerElements & Order[a]) == 0) continue;

            score += BitOperations.PopCount((uint)((int)AdvantageMask[a]    & defender));
            score -= BitOperations.PopCount((uint)((int)DisadvantageMask[a] & defender));
        }

        return score;
    }

    /// <summary>
    /// 攻撃力に属性補正 1.3^score を適用する。PreDefense フェーズで攻撃者のATKに乗算される
    /// （防御差し引き前。戦闘システム 5.1・5.3）。
    ///
    /// 1.3^score を double で計算すると浮動小数点が混入するため、有理数のまま
    /// score ≥ 0 なら × 13^score ÷ 10^score、score &lt; 0 なら × 10^|score| ÷ 13^|score| として適用する。
    /// 除算は floor（負の無限大方向）。
    /// </summary>
    public static BigInteger ApplyToAttack(BigInteger attack, int score)
    {
        if (score == 0) return attack;

        var magnitude = Math.Abs(score);
        var thirteen  = BigInteger.Pow(GameConstants.ElementAffinityNumerator,   magnitude);
        var ten       = BigInteger.Pow(GameConstants.ElementAffinityDenominator, magnitude);

        // 有利なら 13/10 を、不利なら 10/13 を magnitude 乗した分だけ掛ける（分母分子を入れ替える）
        return score > 0
            ? BigIntegerMath.FloorDiv(attack * thirteen, ten)
            : BigIntegerMath.FloorDiv(attack * ten, thirteen);
    }

    /// <summary>攻撃力に属性補正を適用する（スコア算出込みの短縮形）。</summary>
    public static BigInteger ApplyToAttack(BigInteger attack, Element attackerElements, Element defenderElements)
        => ApplyToAttack(attack, GetScore(attackerElements, defenderElements));
}
