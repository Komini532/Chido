using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Entities;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_participant: 戦闘参加者。
/// seat_order / initiative / has_acted は、共有ターンキューを前提とした概念のため持たない。
/// </summary>
public class BattleParticipantRecord
{
    /// <summary>chido_battle_session.session_id を参照。</summary>
    public Guid SessionId { get; set; }

    /// <summary>参加者インスタンスの使い捨てGuid（IEntity.Id）。</summary>
    public Guid EntityId { get; set; }

    /// <summary>0: Player, 1: Enemy。</summary>
    public EntityType EntityType { get; set; }

    /// <summary>entity_type=Player のとき必須。chido_player.user_id を参照。</summary>
    public ulong? UserId { get; set; }

    /// <summary>entity_type=Enemy のとき必須。chido_battle_enemy.enemy_id を参照。</summary>
    public Guid? EnemyId { get; set; }

    /// <summary>
    /// 生死・離脱状態。current_hp=0 からの間接判定ではなく状態そのものを一次情報として保持する
    /// （現在HPは MaxLife を超えうるうえ、戦闘不能の判定根拠を HP に置けないため）。
    /// entity_type を問わず全行に適用される（敵も戦闘離脱モーションにより Escaped になりうる）。
    /// </summary>
    public ParticipantStatus Status { get; set; }

    /// <summary>
    /// 戦闘中の現在HP。現在HPの唯一の真値。参加時は MaxLife（全快）で初期化される。
    /// MaxLife を超える値を取りうる（クランプしない）。「戦闘不能」の判定には使用しない（Status が唯一の根拠）。
    /// 同一の敵への同時攻撃が起きうるため、更新時はアプリ側で SELECT ... FOR UPDATE による悲観ロックを用いる運用。
    /// </summary>
    public BigInteger CurrentHp { get; set; }

    /// <summary>
    /// 現在のTP（0〜1000）。Player は参加時0、Enemy は出現時 chido_enemy_master.initial_tp で初期化。
    /// 蓄積量と上限は GameConstants が保持する（戦闘システム 4.4）。
    /// </summary>
    public ushort CurrentTp { get; set; }

    /// <summary>
    /// 現在の攻撃対象。同一session内の他行のentity_idを参照。
    /// 解決は初回既定・自動失効後の再選定を区別しない単一の導出関数で行い、結果を本列へ書き戻す（戦闘システム 3.3）。
    /// Player: 対象enemyのentity_idが入る／Enemy: ゲームシステム上使用されず常にNULL。
    /// </summary>
    public Guid? CurrentTargetId { get; set; }

    /// <summary>
    /// 敵のローテーション（action_pattern_type=2）の現在位置。出現時0で初期化。
    /// 敵が行動するたびに、選択の成否・require_tp フォールバックの有無に関わらず (rotation_index + 1) % total で進める。
    /// 1対1では結果的に (turn-1) % total に一致するが、それは観測される従属式であって決定規則ではない。
    /// Player およびローテ以外の敵では未使用（0のまま）。
    /// </summary>
    public byte RotationIndex { get; set; }

    /// <summary>
    /// 表示順。entity_type ごとに独立した番号空間を持つ。
    /// Enemy : chido_channel_current_enemy.spawn_index（＝組の member_index）をそのまま複製する。
    ///         ターゲット自動再選定における「先頭の敵」の唯一の根拠（戦闘システム 3.3）。
    /// Player: セッション内の参加順。参加時に同一 (session_id, entity_type=0) の最大値+1 を採番する。
    ///         Discord埋め込みの表示順にのみ使用され、ターゲット選定には使用されない。
    /// 時刻列（joined_at）を順序キーに流用しないための専用列。
    /// </summary>
    public ushort DisplayOrder { get; set; }

    /// <summary>
    /// セッション中に敵参加者へ与えた実効ダメージの累計（台帳）。
    /// 実効ダメージ = min(パイプラインの最終ダメージ, 適用直前の現在HP)。
    /// 経験値按分の分子 own、報酬付与ゲート（累計 &gt; 0）、および分母 sumDmg の集計元となる共通の基準量
    /// （戦闘システム 6.2）。SlipDamage によるダメージは付与者の側に計上される。
    /// </summary>
    public BigInteger TotalDamageDealt { get; set; }

    /// <summary>参加時刻の記録。順序付けには使用しない（DisplayOrder がその責務を持つ）。</summary>
    public DateTime JoinedAt { get; set; }
}
