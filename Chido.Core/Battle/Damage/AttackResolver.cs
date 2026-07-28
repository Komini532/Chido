using System;
using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 攻撃パイプラインの呼び出しから、クリティカル抽選・HPへの適用・ログ整形までを1回分まとめた薄い層。
///
/// クリティカルの発生判定は行動側の責務であり（戦闘システム 5.2）、
/// 行動に渡された <see cref="Random"/> をここで消費する。パイプライン自体は
/// 抽選結果を受け取るだけで乱数に依存しない。
/// </summary>
public static class AttackResolver
{
    /// <summary>
    /// 1回の攻撃を解決し、実効ダメージ（＝ min(最終ダメージ, 適用直前の現在HP)）とログを返す。
    /// 戻り値のダメージは台帳に積む値であり、与ダメージ帰属・被攻撃TP・報酬ゲートが
    /// 共通で参照する基準量になる（戦闘システム 6.2）。
    /// </summary>
    /// <param name="motionElements">
    /// 攻撃モーションの属性。相性判定の攻撃側はスキル属性でも攻撃者の実効属性でもなく、
    /// モーション属性である（戦闘システム 5.3）。通常攻撃は無属性。
    /// </param>
    public static (BigInteger Damage, string Log) Resolve(
        IEntity attacker,
        IEntity defender,
        AttackType attackType,
        Random rng,
        int power = GameConstants.PowerScale,
        Element motionElements = Element.None,
        string? skillName = null)
    {
        var isCritical = GameConstants.CriticalRate.Roll(rng);

        var result = AttackPipeline.Resolve(
            attacker, defender, attackType, power,
            motionElements: motionElements,
            isCritical: isCritical,
            sourceName: skillName);

        var effectiveDamage = defender.TakeDamage(result.FinalDamage);

        var critText = isCritical ? "会心の一撃！ " : string.Empty;
        var log = $"{attacker.Name} の攻撃！ {critText}{defender.Name} に {effectiveDamage} ダメージ。";

        return (effectiveDamage, log);
    }
}
