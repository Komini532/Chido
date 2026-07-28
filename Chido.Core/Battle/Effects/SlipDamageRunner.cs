using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 継続ダメージの発動（戦闘システム 5.4）。
///
/// <b>契機は「そのエンティティの行動枠の終了直後」</b>。Escape を除く全 ActionType は
/// いずれもスキル発動に収束するため、この契機は ActionType を問わず一律に適用できる。
/// 行動不能でモーション再生がスキップされた場合も行動枠自体は開くため発動する（A-7-j）。
/// 発動しないと、行動不能と毒を併せ持つ相手に対して毒が実質無効化されてしまう。
/// <c>/escape</c>（ターンを消費しない＝行動枠が開かない）では発動しない。
///
/// <b>合算則の対象外</b>。SlipDamage は補正値ではなく独立したダメージ発生源であるため、
/// 2.3 のレイヤー内加算は適用されず、<b>各インスタンスがそれぞれ発動する</b>（毒3つなら3回）。
/// </summary>
public static class SlipDamageRunner
{
    /// <summary>
    /// 保持者の SlipDamage インスタンスを instance_id 昇順にすべて発動させる。
    ///
    /// 各インスタンスに最低1が保証されるため合計ダメージは順序に依らないが、
    /// 途中で対象が戦闘不能になる場合の「とどめのインスタンス」の帰属や、
    /// 被攻撃TP・報酬ゲートにおける実効0の割り当ては instance_id 順で決定的になる。
    /// </summary>
    /// <param name="holder">効果の保持者。すなわち被弾側。</param>
    /// <param name="onDamageDealt">
    /// 実効ダメージの通知。第1引数は<b>付与者の entity_id</b> であり、保持者ではない
    /// （SlipDamage の与ダメージは付与者へ帰属する。戦闘システム 6.2）。
    /// 付与者は既に離脱・戦闘不能でもよく、そもそも参加者として解決できる保証がないため、
    /// ライブ攻撃側の通知（BattleParticipant を渡す）とは異なり entity_id を渡す。
    /// </param>
    public static IReadOnlyList<string> Run(
        BattleParticipant holder,
        Action<Guid, BattleParticipant, BigInteger>? onDamageDealt = null)
    {
        var logs = new List<string>();

        if (holder.Entity is not EntityBase entity) return logs;

        // 離脱した参加者は戦場に存在しないため発動させない。
        // 戦闘不能はこの限りではなく、実効ダメージが min(最終ダメージ, 適用直前HP) により
        // 自然に0へ落ちる（6.2 の「とどめ以降の実効0は台帳に寄与しない」がそのまま働く）
        if (holder.Status == ParticipantStatus.Escaped) return logs;

        var instances = entity.Effects
            .Where(e => e.Definition.SlipDamage is not null)
            .OrderBy(e => e, EffectInstanceOrder.Instance)
            .ToList();

        foreach (var instance in instances)
        {
            var spec = instance.Definition.SlipDamage!.Value;

            // 付与時に attack_type を要求しているため、ここに到達する時点で必ず値を持つ
            var attackType = instance.SlipAttackType
                ?? throw new InvalidOperationException(
                    $"{instance.EffectKey} の SlipDamage に attack_type が複製されていない。");

            var result = SlipDamagePipeline.Resolve(
                instance.GranterEntityId,
                instance.SlipAttackSnapshot,
                holder.Entity,
                attackType,
                spec.Power,
                spec.Elements,
                sourceName: instance.Definition.Name);

            var effectiveDamage = holder.Entity.TakeDamage(result.FinalDamage);

            logs.Add($"{holder.Entity.Name} は {instance.Definition.Name} で {effectiveDamage} のダメージを受けた。");
            onDamageDealt?.Invoke(instance.GranterEntityId, holder, effectiveDamage);

            if (holder.Entity.IsAlive || !holder.IsActive) continue;

            holder.MarkDefeated();
            logs.Add($"{holder.Entity.Name} は戦闘不能になった！");
        }

        return logs;
    }
}
