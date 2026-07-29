using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 状態変化の付与・解除（戦闘システム 5.4）。
/// スキルの付与／解除モーションから <see cref="IMotionEffectApplier"/> 越しに呼ばれる。
/// </summary>
/// <param name="definitions">effect_key から状態変化マスタを引く。</param>
public sealed class EffectApplier(IReadOnlyDictionary<string, EffectDefinition> definitions)
    : IMotionEffectApplier
{
    /// <summary>
    /// 状態変化を付与する。
    ///
    /// <b>重複時は拒否</b>する。モーションは実行され accuracy_rate の判定も行われるが、
    /// 付与のみがスキップされ、既存インスタンスの残り有効行動数は変更されない（延長しない）。
    ///
    /// 拒否をデフォルトに置いても、リフレッシュは「解除 → 付与」の2モーション構成で、
    /// スタックは付与元を変えることで表現できる。逆にリフレッシュをデフォルトにすると、
    /// 一度当たった付与を「無かったこと」にする手段がないため拒否は表現できなくなる。
    ///
    /// 拒否された場合も必ず通知する。埋め込みは共有表示であり「あなたが付与したもの」という
    /// 閲覧者依存の情報を載せる手段が原理的に存在しないため、行動者への個別の返信が唯一の置き場所になる。
    /// 通知がなければプレイヤーには「スキルが不発になった」としか映らない。
    /// </summary>
    public string? Grant(
        BattleParticipant granter, BattleParticipant target, GrantEffectMotion motion, string skillKey)
        => Grant(
            granter.Entity, target.Entity, target.EntityType, motion.EffectKey, AffectReason.Skill,
            grantSourceKey: skillKey,
            effectRate: motion.EffectRate,
            attackType: motion.AttackType,
            durationActions: motion.DurationActions);

    /// <summary>
    /// 敵の出現時の初期付与（auto）。付与者は自身であり、grant_source_key は NULL になる。
    /// 持続と攻撃種別の出所が付与モーション側ではなく auto 付与側になるため、
    /// 「6行動で自滅する敵」のような表現がここから成立する。
    /// </summary>
    public string? GrantAuto(
        BattleParticipant target, string effectKey,
        Ratio? effectRate = null, AttackType? attackType = null, ushort? durationActions = null)
        => GrantAuto(
            target.Entity, target.EntityType, effectKey, effectRate, attackType, durationActions);

    /// <summary>
    /// 参加者行が存在しない時点での auto 付与。
    ///
    /// 敵は<b>セッションに属さない状態で出現しうる</b>（戦闘チャンネルの初期化直後や、
    /// PlayerVictory 後に誰も行動していない期間。戦闘システム 10.5）。この時点では
    /// <see cref="BattleParticipant"/> がまだ無いため、エンティティを直接受ける口を用意している。
    /// </summary>
    public string? GrantAuto(
        IEntity target, EntityType entityType, string effectKey,
        Ratio? effectRate = null, AttackType? attackType = null, ushort? durationActions = null)
        => Grant(
            target, target, entityType, effectKey, AffectReason.Auto,
            grantSourceKey: null,
            effectRate: effectRate, attackType: attackType, durationActions: durationActions);

    private string? Grant(
        IEntity granter,
        IEntity target,
        EntityType targetType,
        string effectKey,
        AffectReason affectReason,
        string? grantSourceKey,
        Ratio? effectRate,
        AttackType? attackType,
        ushort? durationActions)
    {
        if (!definitions.TryGetValue(effectKey, out var definition))
            throw new InvalidOperationException($"状態変化マスタに {effectKey} が存在しない。");

        if (target is not EntityBase holder) return null;

        var scope = ResolveScope(targetType, definition);

        // 「戦闘を跨ぐ状態変化は必ず有限」の担保。テーブルをまたぐ条件のためCHECK制約では表現できず、
        // ここが唯一の防波堤になる。真に永久な効果を許すと、加算合成される永続デバフが
        // 単調増加して上限なくステータスを蝕む
        if (scope == EffectScope.Player && durationActions is null)
        {
            throw new InvalidOperationException(
                $"{effectKey} は戦闘を跨ぐ状態変化であるため、持続（duration_actions）が必須。");
        }

        var duplicate = holder.Effects.FirstOrDefault(
            e => e.IsDuplicateOf(scope, effectKey, affectReason, granter.Id, grantSourceKey));

        if (duplicate is not null)
            return $"{target.Name} は既に {definition.Name} の状態です。";

        holder.AddEffect(new EffectInstance(
            definition,
            affectReason,
            granter.Id,
            scope,
            grantSourceKey,
            durationActions,
            ResolveStatusModifiers(definition, effectRate, effectKey),
            definition.SlipDamage is null ? null : attackType,
            ResolveSlipSnapshot(granter, definition, attackType)));

        return $"{target.Name} は {definition.Name} の状態になった。";
    }

    /// <summary>
    /// 対象が保持する<b>全スコープ</b>から effect_key が一致する行をすべて削除する。
    ///
    /// 付与者・付与元・付与要因は参照しない。「解毒」は毒の出所を問わないためであり、
    /// 3体の敵から受けた毒3つも、戦闘を跨いで持ち越した毒も、effect_key が一致する限りすべて消える。
    /// 付与の重複判定の5値を反射的に流用しないこと（意図的な非対称）。
    ///
    /// 対象が該当の effect_key を1つも持っていない場合も0行削除で正常終了とし、
    /// モーションは実行された扱いになる。空振りした場合も通知する（拒否の通知と同じ理由）。
    /// </summary>
    public string? Dispel(BattleParticipant target, DispelEffectMotion motion)
    {
        if (target.Entity is not EntityBase holder) return null;

        var name = definitions.TryGetValue(motion.EffectKey, out var definition)
            ? definition.Name
            : motion.EffectKey;

        var removed = holder.Effects.Where(e => e.EffectKey == motion.EffectKey).ToList();
        foreach (var effect in removed) holder.RemoveEffect(effect);

        return removed.Count == 0
            ? $"{target.Entity.Name} は {name} の状態ではありません。"
            : $"{target.Entity.Name} の {name} が解除された。";
    }

    /// <summary>
    /// 書き込み先スコープ。敵の効果は clear_on_battle_end の値に関わらず常に戦闘内スコープになる
    /// （敵は出現の都度使い捨てのインスタンスであり、永続化する意味を持たないため）。
    /// </summary>
    private static EffectScope ResolveScope(EntityType entityType, EffectDefinition definition)
        => entityType == EntityType.Enemy || definition.ClearOnBattleEnd
            ? EffectScope.Battle
            : EffectScope.Player;

    /// <summary>
    /// ステータス変動の実値を確定させる。
    /// fixed_rate を持つ行はマスタの値をそのまま使い、持たない行は付与モーションの effect_rate を複製する
    /// （固定変動はインスタンス側への複製を避ける、という原則の適用）。
    /// </summary>
    private static IEnumerable<StatusModifier> ResolveStatusModifiers(
        EffectDefinition definition, Ratio? effectRate, string effectKey)
    {
        foreach (var spec in definition.StatusModifiers)
        {
            var rate = spec.FixedRate ?? effectRate
                ?? throw new InvalidOperationException(
                    $"{effectKey} の {spec.TargetStatus} は不定値のため、付与側が effect_rate を供給する必要がある。");

            yield return new StatusModifier(spec.TargetStatus, rate);
        }
    }

    /// <summary>
    /// SlipDamage の攻撃力スナップショット。attack_type が指す側の付与者ATK実値
    /// （付与時の StatusModifier 込み）を固定する。
    /// </summary>
    private static BigInteger ResolveSlipSnapshot(
        IEntity granter, EffectDefinition definition, AttackType? attackType)
    {
        if (definition.SlipDamage is null) return BigInteger.Zero;

        var resolved = attackType
            ?? throw new InvalidOperationException(
                $"{definition.EffectKey} は SlipDamage 成分を持つため、付与側が attack_type を供給する必要がある。");

        return resolved == AttackType.Physical ? granter.PAtk : granter.MAtk;
    }
}
