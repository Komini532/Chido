using System;
using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 付与された状態変化1件（chido_battle_effect / chido_player_effect と、そのインスタンス側サブテーブル）。
///
/// マスタ側は「種別・属性など静的な性質」を持ち、インスタンス側は「発生時点でしか決まらない量」を持つ、
/// という原則に従う。ただし固定変動（fixed_rate）は常に同じ値になるため、
/// インスタンス側への複製を避けてマスタ側に値を持たせる例外を認めている。
/// </summary>
public sealed class EffectInstance
{
    /// <summary>
    /// 使い捨てGuid。1回の付与ごとに新規発行される。
    /// 併存インスタンスの発動順（SlipDamage・DisableMove）の最終タイブレークでもある。
    /// 意味を持たない列だが決定的であるため、安定した順序として十分に機能する。
    /// </summary>
    public Guid InstanceId { get; }

    public EffectDefinition Definition { get; }

    public string EffectKey => Definition.EffectKey;

    /// <summary>付与要因。GrantSourceKey が「何のキーであるか」を示す型タグ。</summary>
    public AffectReason AffectReason { get; }

    /// <summary>付与者の entity_id。auto 付与時は保持者自身と同値（自己付与）。</summary>
    public Guid GranterEntityId { get; }

    /// <summary>識別キー。skill 付与時は skill_key、auto 付与時は NULL。</summary>
    public string? GrantSourceKey { get; }

    /// <summary>保持スコープ。付与時点で確定し、以後変わらない。</summary>
    public EffectScope Scope { get; }

    /// <summary>
    /// 残り有効行動数。保持者が1ターンに関与するごとに -1 し、0 に達した時点で消失する。
    /// NULL = 無期限（残り有効行動数という属性を持たない）。
    ///
    /// 減衰の契機は時間の経過ではなく保持者がターンに関与したことであり、時計ではなくカウンタの消費である。
    /// 「ターン」という語は「戦闘内の時計」という誤った含意を持ち込むため、名前でも避けている。
    /// </summary>
    public ushort? RemainingActions { get; private set; }

    /// <summary>
    /// ステータス変動の実値。fixed_rate を持つ行はその値、持たない行は付与モーションの effect_rate。
    /// </summary>
    public IReadOnlyList<StatusModifier> StatusModifiers { get; }

    /// <summary>
    /// SlipDamage の攻撃種別。同一の effect_key でも、物理スキルで付与されたら物理スリップ、
    /// 魔法スキルで付与されたら魔法スリップになるため、付与モーション側から複製される。
    /// ダメージ計算時に対象の物理／魔法DEFのどちらを引くかを決めるために保持し続ける。
    /// </summary>
    public AttackType? SlipAttackType { get; }

    /// <summary>
    /// SlipDamage の攻撃力スナップショット。付与時点の付与者ATK実値（付与時の StatusModifier 込み）。
    ///
    /// スナップショットするのは付与時ATKと攻撃種別のみであり、
    /// 対象DEF・対象の実効属性・攻撃側属性は発動時に取得する。したがって
    /// 「毒を受けた後に装備を変えて対象DEFを上げるとスリップが軽くなる」という挙動になり、これは意図的である。
    /// </summary>
    public BigInteger SlipAttackSnapshot { get; }

    /// <summary>残り有効行動数を使い切ったか。無期限（NULL）は決して真にならない。</summary>
    public bool IsExpired => RemainingActions == 0;

    public EffectInstance(
        EffectDefinition definition,
        AffectReason affectReason,
        Guid granterEntityId,
        EffectScope scope,
        string? grantSourceKey = null,
        ushort? remainingActions = null,
        IEnumerable<StatusModifier>? statusModifiers = null,
        AttackType? slipAttackType = null,
        BigInteger slipAttackSnapshot = default,
        Guid? instanceId = null)
    {
        Definition = definition;
        AffectReason = affectReason;
        GranterEntityId = granterEntityId;
        Scope = scope;
        GrantSourceKey = grantSourceKey;
        RemainingActions = remainingActions;
        StatusModifiers = statusModifiers is null ? [] : [.. statusModifiers];
        SlipAttackType = slipAttackType;
        SlipAttackSnapshot = slipAttackSnapshot;
        InstanceId = instanceId ?? Guid.NewGuid();
    }

    /// <summary>
    /// 残り有効行動数を1つ消費する。無期限（NULL）は対象外。
    /// 0 に達した行の削除は呼び出し側が同一のトランザクション内で行う
    /// （remaining_actions = 0 の行が他から観測されないようにするため）。
    /// </summary>
    public void Decay()
    {
        if (RemainingActions is { } remaining && remaining > 0)
            RemainingActions = (ushort)(remaining - 1);
    }

    /// <summary>
    /// 重複付与の判定キーが一致するか（戦闘システム 5.4）。
    ///
    /// 判定キーはスコープにより異なる。永続スコープでは <see cref="GranterEntityId"/> を<b>含めない</b>。
    /// 同IDはセッションごとに発行される使い捨てGuidであり、セッションをまたぐ一意性判定に用いると
    /// 同じ敵種と戦うたびに granter が異なるため常に「重複ではない」と判定され、判定が機能しなくなる。
    /// granter を判定に含める意味は「複数の敵から同時に毒を受ける」＝付与者が同じ戦場に同時に存在する
    /// という構造があるからであり、永続スコープにその構造はない。
    ///
    /// GrantSourceKey は auto 付与のとき null を取るため、比較は必ず NULL 安全に行う。
    /// SQLの素直な等価比較では auto 付与だけが無制限に重複するという、テストで気づきにくいバグになる。
    /// </summary>
    public bool IsDuplicateOf(
        EffectScope scope, string effectKey, AffectReason affectReason,
        Guid granterEntityId, string? grantSourceKey)
    {
        if (Scope != scope) return false;
        if (EffectKey != effectKey) return false;
        if (AffectReason != affectReason) return false;
        if (!string.Equals(GrantSourceKey, grantSourceKey, StringComparison.Ordinal)) return false;

        return scope == EffectScope.Player || GranterEntityId == granterEntityId;
    }
}
