using System.Numerics;

namespace RpgBot.Core.Battle.Damage;

public interface IDamageModifier
{
    ModifierPhase Phase { get; }

    /// <summary>
    /// 現フェーズの累積ダメージを受け取り、補正後の値を返す。
    /// context は読み取り専用 (ターゲット情報は DamageCalculator 側が持つ)。
    /// </summary>
    BigInteger Apply(BigInteger current, DamageContext context);

    /// <summary>バトルログ表示用。null = ログ出力しない</summary>
    string? LogLabel { get; }
}
