using System.Numerics;
using Chido.Core.Battle.Effects;
using Chido.Core.Stats;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// インスタンス側サブテーブル（21・22番）の読み書き。
///
/// <para>
/// この2表は <c>chido_battle_effect</c> と <c>chido_player_effect</c> の<b>両方</b>の
/// <c>instance_id</c> を受け入れる共有テーブルであり、親がどちらであるかを区別しない
/// （<c>instance_id</c> はGUIDのため衝突しない）。両スコープのリポジトリが同じ処理を
/// 二重に持たないよう、ここに1箇所だけ置く。
/// </para>
/// <para>
/// <b>ここが欠けると、不定値の状態変化が復元のたびに素通りする。</b>
/// ステータス変動の実値は「マスタの <c>fixed_rate</c>」か「付与時の <c>effect_rate</c>」の
/// どちらかであり、後者はインスタンス側にしか存在しない。<c>SlipDamage</c> の
/// 攻撃力スナップショットも同様で、失うと付与時点の術者ステータスという意味が消えて
/// 継続ダメージが0になる。
/// </para>
/// </summary>
internal static class EffectInstanceRows
{
    /// <summary>
    /// インスタンス側の行を書き出す。
    ///
    /// マスタの <c>fixed_rate</c> を持つ行は書かない（固定変動はインスタンス側への複製を
    /// 避けるという原則。値は常に同じであり、マスタから読み直せる）。
    /// </summary>
    public static void Write(ChidoDbContext db, EffectInstance effect)
    {
        var indeterminate = effect.Definition.StatusModifiers
            .Where(spec => spec.FixedRate is null)
            .Select(spec => spec.TargetStatus)
            .ToHashSet();

        foreach (var modifier in effect.StatusModifiers.Where(m => indeterminate.Contains(m.TargetStatus)))
        {
            db.EffectStatusModifierInstances.Add(new EffectStatusModifierInstanceRecord
            {
                InstanceId = effect.InstanceId,
                TargetStatus = modifier.TargetStatus,
                Rate = modifier.Rate,
            });
        }

        if (effect.SlipAttackType is not { } attackType) return;

        db.EffectSlipDamageInstances.Add(new EffectSlipDamageInstanceRecord
        {
            InstanceId = effect.InstanceId,
            AttackType = attackType,
            StatusAttackValue = effect.SlipAttackSnapshot,
        });
    }

    /// <summary>指定インスタンス群のサブテーブル行をまとめて読む。</summary>
    public static async Task<InstanceDetails> ReadAsync(
        ChidoDbContext db, IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0)
        {
            return new InstanceDetails(
                new Dictionary<Guid, List<EffectStatusModifierInstanceRecord>>(),
                new Dictionary<Guid, EffectSlipDamageInstanceRecord>());
        }

        var modifiers = (await db.EffectStatusModifierInstances
                .Where(x => instanceIds.Contains(x.InstanceId))
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.InstanceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slips = (await db.EffectSlipDamageInstances
                .Where(x => instanceIds.Contains(x.InstanceId))
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.InstanceId);

        return new InstanceDetails(modifiers, slips);
    }

    /// <summary>サブテーブルの行を落とす。親の行を消すときは必ず一緒に呼ぶこと。</summary>
    public static async Task DeleteAsync(
        ChidoDbContext db, IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken)
    {
        if (instanceIds.Count == 0) return;

        await db.EffectStatusModifierInstances
            .Where(x => instanceIds.Contains(x.InstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        await db.EffectSlipDamageInstances
            .Where(x => instanceIds.Contains(x.InstanceId))
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDelete は変更追跡を経由しないため、消えた行が Unchanged のまま残る。
        // 「解除 → 再付与」でリフレッシュを表現する経路で主キーが衝突しないよう追跡から外す
        Detach<EffectStatusModifierInstanceRecord>(db, instanceIds, x => x.InstanceId);
        Detach<EffectSlipDamageInstanceRecord>(db, instanceIds, x => x.InstanceId);
    }

    private static void Detach<T>(
        ChidoDbContext db, IReadOnlyList<Guid> instanceIds, Func<T, Guid> idOf)
        where T : class
    {
        foreach (var entry in db.ChangeTracker.Entries<T>()
                     .Where(e => instanceIds.Contains(idOf(e.Entity)))
                     .ToList())
        {
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
    }

    /// <summary>
    /// マスタとインスタンスを突き合わせて <see cref="EffectInstance"/> を組み立てる。
    ///
    /// ステータス変動の実値は「<c>fixed_rate</c> を持つ行はマスタの値、持たない行は
    /// インスタンス側の実値」という付与時と同じ規則で決まる。インスタンス側の行が
    /// 欠けている不定値は復元できないため落とす（0%として復元すると、効いていない補正が
    /// 表示上だけ存在することになる）。
    /// </summary>
    public static EffectInstance Rebuild(
        EffectDefinition definition,
        Guid instanceId,
        AffectReason affectReason,
        Guid granterEntityId,
        EffectScope scope,
        string? grantSourceKey,
        ushort? remainingActions,
        InstanceDetails details)
    {
        var stored = details.Modifiers.TryGetValue(instanceId, out var rows)
            ? rows.ToDictionary(x => x.TargetStatus, x => x.Rate)
            : [];

        var modifiers = new List<StatusModifier>();

        foreach (var spec in definition.StatusModifiers)
        {
            if (spec.FixedRate is { } fixedRate)
            {
                modifiers.Add(new StatusModifier(spec.TargetStatus, fixedRate));
            }
            else if (stored.TryGetValue(spec.TargetStatus, out var rate))
            {
                modifiers.Add(new StatusModifier(spec.TargetStatus, rate));
            }
        }

        details.Slips.TryGetValue(instanceId, out var slip);

        return new EffectInstance(
            definition,
            affectReason,
            granterEntityId,
            scope,
            grantSourceKey,
            remainingActions,
            modifiers,
            slip?.AttackType,
            slip?.StatusAttackValue ?? BigInteger.Zero,
            instanceId);
    }
}

/// <summary>読み出したインスタンス側サブテーブルの行。</summary>
internal readonly record struct InstanceDetails(
    IReadOnlyDictionary<Guid, List<EffectStatusModifierInstanceRecord>> Modifiers,
    IReadOnlyDictionary<Guid, EffectSlipDamageInstanceRecord> Slips);
