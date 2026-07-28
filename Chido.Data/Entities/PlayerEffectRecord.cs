using Chido.Core.Battle.Effects;

namespace Chido.Data.Entities;

/// <summary>
/// chido_player_effect (20): 状態変化保持（永続スコープ）。
/// Player の clear_on_battle_end=false の効果のみがここに書き込まれる。
/// 付与先が 19番／20番のどちらになるかは付与時点の entity_type と clear_on_battle_end の組み合わせで
/// その場で確定するため、戦闘終了時に一方から他方へ行を移し替える処理は発生しない。
///
/// 減衰はその場で行う（戦闘開始時に複製し終了時に書き戻す作業コピー方式は採らない）。
/// セッションは非同期・長期間開きっぱなしになりうるため、書き戻しの契機が来る保証がなく、
/// 永続効果の真値が無期限に戦闘テーブルに人質に取られるため。
///
/// 重複の判定キーは user_id + effect_key + affect_reason + grant_source_key の4値で、
/// granter_entity_id を含めない。同IDはセッションごとに発行される使い捨てGuidであり、
/// セッションをまたぐ本テーブルの一意性判定に用いると、同じ敵種と戦うたびに granter が異なるため
/// 常に「重複ではない」と判定され、判定が機能しなくなる。
/// </summary>
public class PlayerEffectRecord
{
    /// <summary>使い捨てGuid。1回の付与ごとに新規発行。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// chido_player.user_id を参照。効果保持者（Playerのみ。
    /// Enemy は出現の都度使い捨てのインスタンスであり永続効果を持つ意味がない）。
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>付与要因。</summary>
    public AffectReason AffectReason { get; set; }

    /// <summary>
    /// 付与時点における付与者のentity_id（履歴的参照）。
    /// chido_battle_participant の行は戦闘終了後も物理削除されない前提のため参照可能。
    /// 重複付与の一意性判定には使用しない（上記参照）。
    /// </summary>
    public Guid GranterEntityId { get; set; }

    /// <summary>識別キー。skill付与時は skill_key。auto付与時は NULL。</summary>
    public string? GrantSourceKey { get; set; }

    /// <summary>
    /// 残り有効行動数。保持者が1ターンに関与するごとに -1 し、0 で消滅する。
    /// 戦闘の境界では減衰も消滅もしない（戦闘を跨いで持続する）。
    ///
    /// NOT NULL である理由: 永続スコープの効果は必ず有限でなければならない。
    /// NULL（無期限）を許すと「真に永久」な効果が表現可能になるが、真に永久なステータス補正は
    /// レベルや装備や称号と同じプレイヤーの属性であって、付与・解除というライフサイクルを持つ
    /// インスタンスではない。それを状態変化として持つと加算合成される永続デバフが単調増加し、
    /// 上限なくステータスを蝕む。
    /// </summary>
    public ushort RemainingActions { get; set; }
}
