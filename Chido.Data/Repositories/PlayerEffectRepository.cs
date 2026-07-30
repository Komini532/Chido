using Chido.Core.Battle.Effects;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 戦闘を跨ぐ状態変化（<c>chido_player_effect</c>）の永続化。
///
/// <b>戦闘中にその場で減衰させる。</b>「戦闘開始時に <c>chido_battle_effect</c> へ複製し、
/// 終了時に書き戻す」という作業コピー方式は採らない。全参加者が戦闘不能でもセッションは
/// 終了せず、セッションは非同期・長期間開きっぱなしになりうるため、<b>書き戻しの契機が来る
/// 保証がない</b>。作業コピー方式では永続効果の真値が無期限に戦闘テーブルに人質に取られる
/// （戦闘システム 5.4）。
///
/// 本テーブルへの書き込みは、対象が行動者本人か他の参加者かを問わず
/// <b>チャンネル行②に包摂される</b>。<c>target_rule = 味方</c> の付与・解除モーションは
/// 他プレイヤーの行を書き込むが、行動者は他プレイヤーの行①を取得しない。
/// ②を飛ばす唯一の経路である装備変更は本テーブルを書かないため、②のみで安全が保たれる。
/// </summary>
public sealed class PlayerEffectRepository(ChidoDbContext db)
{
    public Task<List<PlayerEffectRecord>> LoadAsync(
        ulong userId, CancellationToken cancellationToken = default)
        => db.PlayerEffects
            .Where(x => x.UserId == userId)
            // 併存インスタンスの発動順は instance_id 昇順。BINARY(16) の照合順序は
            // 格納バイト列の辞書順であり、Core 側の EffectInstanceOrder と一致する
            .OrderBy(x => x.InstanceId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// 付与する。<b>重複判定は呼び出し側（<c>EffectApplier</c>）の責務</b>であり、ここでは行わない。
    ///
    /// 永続スコープの判定キーは <c>user_id + effect_key + affect_reason + grant_source_key</c> の4値で、
    /// <c>granter_entity_id</c> を<b>含めない</b>。DBレベルの UNIQUE では守れない
    /// （MySQL の UNIQUE は NULL を互いに異なる値として扱うため、<c>grant_source_key</c> が
    /// NULL の行は何行でも入る）。アプリ側担保は消極的な選択ではなく唯一の選択肢である。
    /// </summary>
    public void Add(ulong userId, EffectInstance effect)
    {
        if (effect.Scope != EffectScope.Player)
        {
            throw new InvalidOperationException(
                $"{effect.EffectKey} は戦闘内スコープであり、chido_player_effect には書き込めない。");
        }

        // 戦闘を跨ぐ状態変化は必ず有限（EffectApplier が付与時に担保している）
        var remaining = effect.RemainingActions
            ?? throw new InvalidOperationException(
                $"{effect.EffectKey} は戦闘を跨ぐ状態変化であるため、残り有効行動数が必須。");

        db.PlayerEffects.Add(new PlayerEffectRecord
        {
            InstanceId = effect.InstanceId,
            UserId = userId,
            EffectKey = effect.EffectKey,
            AffectReason = effect.AffectReason,
            GranterEntityId = effect.GranterEntityId,
            GrantSourceKey = effect.GrantSourceKey,
            RemainingActions = remaining,
        });

        // 不定値のステータス変動と SlipDamage のスナップショットはインスタンス側にしか存在しない。
        // 書き漏らすと、復元のたびに補正が消える（マスタの固定変動だけが生き残る）
        EffectInstanceRows.Write(db, effect);
    }

    /// <summary>
    /// 残り有効行動数を1つ消費し、使い切った行を<b>同一トランザクション内で</b>削除する。
    ///
    /// 減算と削除を分けると <c>remaining_actions = 0</c> の行が他から観測されうる。
    /// 対象は関与者集合のプレイヤーのみであり、集合外のプレイヤーは減衰しない。
    /// </summary>
    /// <returns>使い切って削除された行数。</returns>
    public async Task<int> DecayAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var effects = await db.PlayerEffects
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var removed = new List<Guid>();

        foreach (var effect in effects)
        {
            effect.RemainingActions--;

            if (effect.RemainingActions != 0) continue;

            db.PlayerEffects.Remove(effect);
            removed.Add(effect.InstanceId);
        }

        await EffectInstanceRows.DeleteAsync(db, removed, cancellationToken);

        return removed.Count;
    }

    /// <summary>
    /// 解除モーションによる削除。<b>effect_key が一致する行をすべて</b>落とし、
    /// 付与者・付与元・付与要因は参照しない（「解毒」は毒の出所を問わないため。
    /// 付与の重複判定キーとは意図的に非対称）。
    /// </summary>
    public async Task<int> DispelAsync(
        ulong userId, string effectKey, CancellationToken cancellationToken = default)
    {
        var instanceIds = await db.PlayerEffects
            .Where(x => x.UserId == userId && x.EffectKey == effectKey)
            .Select(x => x.InstanceId)
            .ToListAsync(cancellationToken);

        var deleted = await db.PlayerEffects
            .Where(x => x.UserId == userId && x.EffectKey == effectKey)
            .ExecuteDeleteAsync(cancellationToken);

        await EffectInstanceRows.DeleteAsync(db, instanceIds, cancellationToken);

        // ExecuteDelete は変更追跡を経由しないため、消えた行が Unchanged のまま残る。
        // 解除直後に同じ effect_key を再付与する（「解除 → 付与」でリフレッシュを表現する）経路で
        // 主キーが衝突しないよう、ここで追跡から外す
        foreach (var entry in db.ChangeTracker.Entries<PlayerEffectRecord>()
                     .Where(e => e.Entity.UserId == userId && e.Entity.EffectKey == effectKey)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return deleted;
    }
}
