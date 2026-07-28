using System.Numerics;

namespace Chido.Core.Battle.Damage.Modifiers;

/// <summary>
/// 属性補正（PreDefense）。有効ATKに 1.3^x を乗算する（戦闘システム 5.1・5.3）。
///
/// PreDefense に乗るのは属性補正<b>のみ</b>である。StatusModifier によるバフ・デバフは
/// 有効ATKに既に織り込まれているため、ここで再適用すると二重適用になる。
///
/// 倍率を Ratio へ丸めてから乗算するのではなく <see cref="ElementAffinity.ApplyToAttack"/> を
/// 直接呼ぶ。permyriad へ丸める過程で誤差が乗り、13^x ÷ 10^x の有理数演算という
/// 決定事項の意味が失われるため。
///
/// 属性倍率はATKに乗ってから基礎ダメージ式に入るため、新式では倍率がATKごと2乗される形になり、
/// 有利／不利の影響度が非線形に拡大する。これは意図した挙動である
/// （厳密には DEF ≫ ATK のとき2乗へ、DEF ≪ ATK のとき1乗へ漸近する）。
/// </summary>
public sealed class ElementAffinityModifier : IDamageModifier
{
    /// <summary>相性スコア x（有利ペア数 − 不利ペア数）。</summary>
    public int Score { get; }

    public ModifierPhase Phase => ModifierPhase.PreDefense;
    public string? LogLabel { get; }

    private ElementAffinityModifier(int score, string? logLabel)
    {
        Score = score;
        LogLabel = logLabel;
    }

    /// <summary>
    /// 攻撃モーションの属性と対象の実効属性から生成する。
    /// スコアが 0（等倍）の場合は補正が不要なため null を返す。
    /// </summary>
    public static ElementAffinityModifier? Create(Element motionElements, Element targetElements)
    {
        var score = ElementAffinity.GetScore(motionElements, targetElements);
        if (score == 0) return null;

        var label = score > 0
            ? $"属性有利 ×1.3^{score}"
            : $"属性不利 ×1.3^{score}";

        return new ElementAffinityModifier(score, label);
    }

    public BigInteger Apply(BigInteger current, DamageContext context)
        => ElementAffinity.ApplyToAttack(current, Score);
}
