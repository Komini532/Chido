using Chido.Core.Battle;
using Chido.Core.Battle.Effects;
using Chido.Data.Catalogs;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 戦闘内スコープの状態変化（<c>chido_battle_effect</c>）の永続化。
///
/// <para>
/// <b>1回の戦闘行動ごとにプロセスの記憶は失われる。</b>コマンドは互いに独立した
/// トランザクションで走るため、敵が持つ auto 付与の効果も、プレイヤーが戦闘中に受けた
/// 効果も、ここへ書き出さなければ次のコマンドには何も残らない。
/// 「6行動で自滅する敵」が永遠に自滅しなくなるのがその典型で、
/// マスタ上は定義されている挙動が実行時にだけ消えるという形で現れる。
/// </para>
/// <para>
/// 参加者の効果は<b>毎回すべて置き換える</b>。差分を取ると、付与・拒否・減衰・解除・
/// 使い切りによる消滅が同一ターンに混在したときの整合が呼び出し側の問題になる。
/// 対象は関与者集合（最大3体）に限られるため、置き換えの費用は行数に比例して小さい。
/// </para>
/// <para>
/// <b>チャンネル行②のロック下で呼ぶこと</b>（戦闘システム 7.2）。
/// </para>
/// </summary>
public sealed class BattleEffectRepository(ChidoDbContext db)
{
    /// <summary>
    /// 指定した参加者群が保持する戦闘内スコープの効果を読む。
    ///
    /// 併存インスタンスの発動順は <c>instance_id</c> 昇順。<c>BINARY(16)</c> の照合順序は
    /// 格納バイト列の辞書順であり、Core 側の <c>EffectInstanceOrder</c> と一致する。
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, List<EffectInstance>>> LoadAsync(
        IReadOnlyList<Guid> entityIds,
        EffectCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        if (entityIds.Count == 0) return new Dictionary<Guid, List<EffectInstance>>();

        var rows = await db.BattleEffects
            .Where(x => entityIds.Contains(x.EntityId))
            .OrderBy(x => x.InstanceId)
            .ToListAsync(cancellationToken);

        var details = await EffectInstanceRows.ReadAsync(
            db, rows.Select(x => x.InstanceId).ToList(), cancellationToken);

        var result = new Dictionary<Guid, List<EffectInstance>>();

        foreach (var row in rows)
        {
            // マスタから消えた効果は復元できない。落として進む（状態変化が1つ効かなくなるだけで、
            // プレイヤーが戦闘に入れなくなるよりは軽い）
            if (catalog.Find(row.EffectKey) is not { } definition) continue;

            result.TryAdd(row.EntityId, []);
            result[row.EntityId].Add(EffectInstanceRows.Rebuild(
                definition, row.InstanceId, row.AffectReason, row.GranterEntityId,
                EffectScope.Battle, row.GrantSourceKey, row.RemainingActions, details));
        }

        return result;
    }

    /// <summary>
    /// 参加者が保持する戦闘内スコープの効果を、現在の内容で置き換える。
    ///
    /// 永続スコープの効果は<b>対象外</b>である。あちらは <c>chido_player_effect</c> が真値を持ち、
    /// 減衰も解除もその場で行われる（作業コピー方式を採らない理由と同じ。戦闘システム 5.4）。
    /// </summary>
    public async Task ReplaceAsync(
        BattleParticipant participant, CancellationToken cancellationToken = default)
    {
        var entityId = participant.Entity.Id;

        var existing = await db.BattleEffects
            .Where(x => x.EntityId == entityId)
            .Select(x => x.InstanceId)
            .ToListAsync(cancellationToken);

        await db.BattleEffects.Where(x => x.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);

        foreach (var entry in db.ChangeTracker.Entries<BattleEffectRecord>()
                     .Where(e => e.Entity.EntityId == entityId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        await EffectInstanceRows.DeleteAsync(db, existing, cancellationToken);

        if (participant.Entity is not Core.Entities.EntityBase holder) return;

        foreach (var effect in holder.Effects.Where(e => e.Scope == EffectScope.Battle))
        {
            db.BattleEffects.Add(new BattleEffectRecord
            {
                InstanceId = effect.InstanceId,
                EntityId = entityId,
                EffectKey = effect.EffectKey,
                AffectReason = effect.AffectReason,
                GranterEntityId = effect.GranterEntityId,
                GrantSourceKey = effect.GrantSourceKey,
                RemainingActions = effect.RemainingActions,
            });

            EffectInstanceRows.Write(db, effect);
        }
    }

    /// <summary>
    /// セッション終了時に戦闘内スコープの効果を除去する（戦闘システム 5.4）。
    ///
    /// 永続スコープには一切触れない。あちらは戦闘の境界を何も参照せず、
    /// 残り有効行動数だけが終わりを保証する。
    /// </summary>
    public async Task ClearAsync(
        IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken = default)
    {
        if (entityIds.Count == 0) return;

        var instanceIds = await db.BattleEffects
            .Where(x => entityIds.Contains(x.EntityId))
            .Select(x => x.InstanceId)
            .ToListAsync(cancellationToken);

        await db.BattleEffects
            .Where(x => entityIds.Contains(x.EntityId))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var entry in db.ChangeTracker.Entries<BattleEffectRecord>()
                     .Where(e => entityIds.Contains(e.Entity.EntityId))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        await EffectInstanceRows.DeleteAsync(db, instanceIds, cancellationToken);
    }
}
