using System.Numerics;
using Chido.Core.Battle.Damage.Modifiers;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// スリップパイプライン（SlipDamage、戦闘システム 5.1・5.4）。
///
/// <code>
/// 1. PreDefense  … 属性補正のみ（スナップショットATK × 1.3^x）
/// 2. 基礎ダメージ … スナップショットATK' と attack_type で選んだ対象DEF で攻撃式と同型
/// 3. PostDefense … power のみ
/// 4. Final / 最低ダメージ 1
/// </code>
///
/// <b>攻撃パイプラインとの違いはクリティカルとDRRを登録しないことのみ</b>で、
/// 被防御係数と属性相性は適用される。
///
/// 使用するATKは<b>付与時点のスナップショット</b>（chido_effect_slip_damage_instance.status_attack_value）
/// であり、付与者の付与時 StatusModifier を既に織り込んでいる。PreDefense が属性のみであるため
/// 二重適用は起こらない。付与者が既に離脱・戦闘不能でも、また付与後にステータスが変動しても、
/// スリップの威力は付与時点のまま固定される。
/// </summary>
public static class SlipDamagePipeline
{
    /// <summary>
    /// スリップダメージ1インスタンス分のダメージを算出する。HPへの適用は呼び出し側が行う。
    /// </summary>
    /// <param name="granterId">付与者の entity_id。与ダメージは付与者へ帰属する（戦闘システム 6.2）。</param>
    /// <param name="snapshotAtk">付与時点の攻撃力実値のスナップショット。</param>
    /// <param name="target">対象。有効DEFと実効属性の供給元。DRR は参照しない。</param>
    /// <param name="attackType">対象の物理／魔法DEFのどちらを引くかを決める、付与時に複製された静的な性質。</param>
    /// <param name="power">威力。整数%。</param>
    /// <param name="elements">状態変化マスタが持つ攻撃属性。マスタ由来のため付与後も不変。</param>
    public static DamageResult Resolve(
        Guid granterId,
        BigInteger snapshotAtk,
        IEntity target,
        AttackType attackType,
        int power,
        Element elements = Element.None,
        string? sourceName = null)
    {
        var defense = attackType == AttackType.Physical ? target.PDef : target.MDef;

        var builder = new DamageContext.Builder(granterId, attackType, snapshotAtk);

        // 攻撃と同じく PreDefense は属性補正のみ
        builder.AddModifier(ElementAffinityModifier.Create(elements, target.Elements));

        // PostDefense は power のみ。クリティカルとDRRは登録しない
        builder.AddModifier(new PowerModifier(power, sourceName));

        return AttackPipeline.Calculate(builder.Build(), defense);
    }
}
