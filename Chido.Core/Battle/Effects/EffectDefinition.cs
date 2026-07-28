using System.Collections.Generic;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 状態変化マスタ（chido_effect_master とサブテーブル 16〜18番・45番）。
///
/// 1つの状態変化が複数の効果種別を兼ねうる（マルチネイチャー）ため、各成分は独立に保持する。
/// <see cref="EffectTypes"/> はどのサブテーブルに行があるかの非正規化キャッシュであり、
/// 真実の情報源は各成分そのもの。
/// </summary>
public sealed class EffectDefinition
{
    public string EffectKey { get; }
    public string Name { get; }

    /// <summary>
    /// 戦闘終了時に解除するか。書き込み先スコープの判定に使う。
    /// false（戦闘を跨ぐ）の効果を付与する場合、持続は NOT NULL 必須
    /// （終わりを保証するものが行動数しかないため。<see cref="EffectApplier"/> が担保する）。
    /// </summary>
    public bool ClearOnBattleEnd { get; }

    /// <summary>
    /// ステータス変動成分。1つの effect_key が複数の対象ステータスを同時に変動させうるため複数行を許容する。
    /// DRR もこの成分の一値として表現される（専用サブテーブルを設けない）。
    /// </summary>
    public IReadOnlyList<StatusModifierSpec> StatusModifiers { get; }

    /// <summary>継続ダメージ成分。null なら SlipDamage を持たない。</summary>
    public SlipDamageSpec? SlipDamage { get; }

    /// <summary>
    /// 行動不能率。null なら DisableMove を持たない。
    /// 付与時に固定せず、保持者が行動しようとするたびに引く確率であるため、
    /// インスタンス側に複製する値を持たない。
    /// </summary>
    public Ratio? DisableRate { get; }

    /// <summary>
    /// 一時付与する属性。<see cref="Element.None"/> なら ElementGrant を持たない。
    /// %補正ではなくビット加算であるため他の3種と性質が異なり、専用のマスタで表現される。
    /// 付与される属性は effect_key ごとにマスタ側で固定であり、インスタンスごとに変わる値を持たない。
    /// </summary>
    public Element GrantedElements { get; }

    /// <summary>保有効果種別（ビット列）。各成分の有無から導出される非正規化キャッシュ。</summary>
    public EffectType EffectTypes { get; }

    public EffectDefinition(
        string effectKey,
        string name,
        bool clearOnBattleEnd = true,
        IEnumerable<StatusModifierSpec>? statusModifiers = null,
        SlipDamageSpec? slipDamage = null,
        Ratio? disableRate = null,
        Element grantedElements = Element.None)
    {
        EffectKey = effectKey;
        Name = name;
        ClearOnBattleEnd = clearOnBattleEnd;
        StatusModifiers = statusModifiers is null ? [] : [.. statusModifiers];
        SlipDamage = slipDamage;
        DisableRate = disableRate;
        GrantedElements = grantedElements;

        var types = EffectType.None;
        if (StatusModifiers.Count > 0) types |= EffectType.StatusModifier;
        if (SlipDamage is not null) types |= EffectType.SlipDamage;
        if (DisableRate is not null) types |= EffectType.DisableMove;
        if (GrantedElements != Element.None) types |= EffectType.ElementGrant;
        EffectTypes = types;
    }
}

/// <summary>
/// ステータス変動1行（chido_effect_status_modifier_master）。
/// </summary>
/// <param name="TargetStatus">対象ステータス。DRR もこの一値として表現される。</param>
/// <param name="FixedRate">
/// 固定変動率。NOT NULL なら常にこの値を使い（例: 防御の DRR 50%）、
/// NULL なら不定値として付与モーションの effect_rate をインスタンスへ複製する。
/// </param>
public readonly record struct StatusModifierSpec(TargetStatus TargetStatus, Ratio? FixedRate = null);

/// <summary>
/// 継続ダメージ成分（chido_effect_slip_damage_master）。
/// </summary>
/// <param name="Power">威力。整数%。攻撃モーションの power と同一の概念・同一のスケール。</param>
/// <param name="Elements">
/// 攻撃属性。マスタ由来のため付与後も不変であり、スナップショット対象ではない。
/// </param>
public readonly record struct SlipDamageSpec(int Power, Element Elements = Element.None);
