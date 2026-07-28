using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;
using Xunit;

namespace Chido.Core.Tests.Battle.Damage;

/// <summary>
/// 属性相性の検証（戦闘システム 5.3）。
/// 相性表はDBマスタではなくC#定数として保持するため、表の不変条件は単体テストで守る、というのが
/// 設計上の取り決め（DB化しても再デプロイ不要という実利が得られないため定数側を選んだ）。
/// </summary>
public class ElementAffinityTests
{
    /// <summary>相性表の行・列の並び。戦闘システム 5.3 の表と同一。</summary>
    private static readonly Element[] Order =
    [
        Element.Fire, Element.Water, Element.Grass, Element.Earth, Element.Sky,
        Element.Thunder, Element.Ice, Element.Light, Element.Dark, Element.Neutral,
    ];

    /// <summary>
    /// 期待される相性表。設計ドキュメント 戦闘システム 5.3 の表を実装とは独立に書き起こしたもの。
    /// 行が攻撃側、列が防御側。'O' = 有利(+1) / 'X' = 不利(-1) / '-' = 等倍(0)。
    /// </summary>
    private static readonly string[] Expected =
    [
        //火水草土空雷氷光闇無
        "-XOXX-O-O-", // 火
        "O-XOOXX---", // 水
        "XO-OXOX-X-", // 草
        "OXX-XOOO--", // 土
        "OXOO-XX-X-", // 空
        "-OXXO-X---", // 雷
        "XOOXOO-X--", // 氷
        "---X--O-O-", // 光
        "X-O-O--X--", // 闇
        "----------", // 無
    ];

    private static int ExpectedRel(int attacker, int defender) => Expected[attacker][defender] switch
    {
        'O' => +1,
        'X' => -1,
        _   => 0,
    };

    // --- 相性表そのもの ---

    [Fact]
    public void 相性表が設計ドキュメントの表と一致する()
    {
        for (var a = 0; a < Order.Length; a++)
        {
            for (var d = 0; d < Order.Length; d++)
            {
                Assert.Equal(ExpectedRel(a, d), ElementAffinity.GetScore(Order[a], Order[d]));
            }
        }
    }

    [Fact]
    public void 相性表は完全反対称である()
    {
        // 全45ペアが (有利, 不利) か (等倍, 等倍) のいずれかであり、
        // 「相互に有利」「片方だけ有利で逆は等倍」の組み合わせは存在しない。
        // プレイヤーから見た「その敵に有利なら、その敵からも有利を取られない」という直感の保証でもある。
        for (var a = 0; a < Order.Length; a++)
        {
            for (var b = a + 1; b < Order.Length; b++)
            {
                var forward = ElementAffinity.GetScore(Order[a], Order[b]);
                var reverse = ElementAffinity.GetScore(Order[b], Order[a]);

                Assert.True(forward == -reverse,
                    $"反対称でない: {Order[a]} → {Order[b]} = {forward}, 逆 = {reverse}");
            }
        }
    }

    [Fact]
    public void 対角はすべて等倍である()
    {
        foreach (var element in Order)
        {
            Assert.Equal(0, ElementAffinity.GetScore(element, element));
        }
    }

    [Fact]
    public void Neutralの行と列はすべて等倍である()
    {
        // elements = 0 が相性計算をスキップして全属性等倍になる以上、Neutral 単体との等価性を
        // 保つためにこの制約が必要になる（戦闘システム 2.4）
        foreach (var element in Order)
        {
            Assert.Equal(0, ElementAffinity.GetScore(Element.Neutral, element));
            Assert.Equal(0, ElementAffinity.GetScore(element, Element.Neutral));
        }
    }

    // --- 属性なし（Element.None）との等価性 ---

    [Fact]
    public void 属性なしはNeutral単体と同じ結果になる()
    {
        // 「属性なしなら相性計算を省略する」は仕様上の分岐ではなく実装上の最適化にすぎない、
        // という主張（戦闘システム 2.4）の裏付け
        foreach (var element in Order)
        {
            Assert.Equal(ElementAffinity.GetScore(element, Element.Neutral),
                         ElementAffinity.GetScore(element, Element.None));

            Assert.Equal(ElementAffinity.GetScore(Element.Neutral, element),
                         ElementAffinity.GetScore(Element.None, element));
        }
    }

    [Fact]
    public void Neutralを足しても結果は変わらない()
    {
        foreach (var attacker in Order)
        {
            foreach (var defender in Order)
            {
                Assert.Equal(ElementAffinity.GetScore(attacker, defender),
                             ElementAffinity.GetScore(attacker, defender | Element.Neutral));

                Assert.Equal(ElementAffinity.GetScore(attacker, defender),
                             ElementAffinity.GetScore(attacker | Element.Neutral, defender));
            }
        }
    }

    // --- 総当たりの加法性 ---

    [Fact]
    public void スコアは防御側属性について加法的である()
    {
        // 「防御側の属性は増えても希釈されず、振れ幅を増幅する」（戦闘システム 5.3）ことの直接の表現。
        // 各ペアが加算されるため、属性を足すと影響が薄まるのではなく広がる
        foreach (var attacker in Order)
        {
            for (var i = 0; i < Order.Length; i++)
            {
                for (var j = i + 1; j < Order.Length; j++)
                {
                    var combined = ElementAffinity.GetScore(attacker, Order[i] | Order[j]);
                    var separate = ElementAffinity.GetScore(attacker, Order[i])
                                 + ElementAffinity.GetScore(attacker, Order[j]);

                    Assert.Equal(separate, combined);
                }
            }
        }
    }

    [Fact]
    public void スコアは攻撃側属性についても加法的である()
    {
        foreach (var defender in Order)
        {
            for (var i = 0; i < Order.Length; i++)
            {
                for (var j = i + 1; j < Order.Length; j++)
                {
                    var combined = ElementAffinity.GetScore(Order[i] | Order[j], defender);
                    var separate = ElementAffinity.GetScore(Order[i], defender)
                                 + ElementAffinity.GetScore(Order[j], defender);

                    Assert.Equal(separate, combined);
                }
            }
        }
    }

    [Fact]
    public void 攻撃1属性_防御5属性までのスコアはマイナス4からプラス4に収まる()
    {
        // 装備由来の属性は最大5スロット分の OR となりうるため、防御側が5属性になる場合を上限として見る
        var min = int.MaxValue;
        var max = int.MinValue;

        foreach (var attacker in Order)
        {
            foreach (var defenders in CombinationsUpTo(Order, 5))
            {
                var score = ElementAffinity.GetScore(attacker, defenders);
                min = Math.Min(min, score);
                max = Math.Max(max, score);
            }
        }

        Assert.Equal(-4, min);
        Assert.Equal(+4, max);
    }

    private static IEnumerable<Element> CombinationsUpTo(Element[] source, int size)
    {
        // 5要素までの全組み合わせを OR した集合を列挙する
        var total = 1 << source.Length;
        for (var mask = 0; mask < total; mask++)
        {
            if (BitOperations.PopCount((uint)mask) > size) continue;

            var combined = Element.None;
            for (var i = 0; i < source.Length; i++)
            {
                if ((mask & (1 << i)) != 0) combined |= source[i];
            }

            yield return combined;
        }
    }

    // --- 攻撃力への適用 ---

    [Fact]
    public void ApplyToAttack_スコア0は恒等である()
    {
        Assert.Equal(new BigInteger(1000), ElementAffinity.ApplyToAttack(1000, 0));
    }

    [Theory]
    [InlineData(1000, 1, 1300)]   // × 13 ÷ 10
    [InlineData(1000, 2, 1690)]   // × 169 ÷ 100
    [InlineData(1000, -1, 769)]   // × 10 ÷ 13   = 769.2… → 769
    [InlineData(1000, -2, 591)]   // × 100 ÷ 169 = 591.7… → 591
    public void ApplyToAttack_有理数のまま倍率を適用する(int attack, int score, int expected)
    {
        Assert.Equal(new BigInteger(expected), ElementAffinity.ApplyToAttack(attack, score));
    }

    [Fact]
    public void ApplyToAttack_端数は切り捨てられる()
    {
        // 1 × 10 ÷ 13 = 0.769… → 0
        Assert.Equal(BigInteger.Zero, ElementAffinity.ApplyToAttack(1, -1));
    }

    [Fact]
    public void ApplyToAttack_負の攻撃力でも負の無限大方向へ丸める()
    {
        // -1 × 13 ÷ 10 = -1.3 → floor は -2（0方向切り捨てなら -1）。
        // 戦闘システム 5.1 の「すべての除算は floor」に従うことの確認
        Assert.Equal(new BigInteger(-2), ElementAffinity.ApplyToAttack(-1, 1));
    }

    [Fact]
    public void ApplyToAttack_巨大なBigIntegerでも桁落ちしない()
    {
        var attack = BigInteger.Pow(10, 40);

        Assert.Equal(attack * 13 / 10, ElementAffinity.ApplyToAttack(attack, 1));
        Assert.Equal(attack * 10 / 13, ElementAffinity.ApplyToAttack(attack, -1));
    }

    [Fact]
    public void ApplyToAttack_属性を渡す短縮形はスコア経由と一致する()
    {
        foreach (var attacker in Order)
        {
            foreach (var defender in Order)
            {
                var viaScore = ElementAffinity.ApplyToAttack(
                    12345, ElementAffinity.GetScore(attacker, defender));

                Assert.Equal(viaScore, ElementAffinity.ApplyToAttack(12345, attacker, defender));
            }
        }
    }

    // --- Ratio 互換シム ---

    [Fact]
    public void GetMultiplier_等倍のときはRatio_Fullを返す()
    {
        Assert.Equal(Ratio.Full, ElementAffinity.GetMultiplier(Element.Fire, Element.Fire));
        Assert.Equal(Ratio.Full, ElementAffinity.GetMultiplier(Element.Fire, Element.None));
    }

    [Fact]
    public void GetMultiplier_有利不利の倍率が相性表に従う()
    {
        // 火 → 草 は有利、火 → 水 は不利（表の該当セル）
        Assert.Equal(Ratio.FromPermyriad(13000), ElementAffinity.GetMultiplier(Element.Fire, Element.Grass));
        Assert.Equal(Ratio.FromPermyriad(7692),  ElementAffinity.GetMultiplier(Element.Fire, Element.Water));
    }
}
