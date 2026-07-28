using Chido.Core.Battle.Actions;

namespace Chido.Data.Entities;

/// <summary>
/// chido_battle_log: 戦闘ログ。
/// 非同期設計において、離席中に起きた出来事を後から追える点で特に価値が高いテーブル。
/// </summary>
public class BattleLogRecord
{
    /// <summary>ログの連番ID（AUTO_INCREMENT）。</summary>
    public ulong LogId { get; set; }

    /// <summary>chido_battle_session.session_id を参照。</summary>
    public Guid SessionId { get; set; }

    /// <summary>行動主体のentity_id（chido_battle_participant.entity_id）。</summary>
    public Guid ActorId { get; set; }

    /// <summary>ActionType（Attack/Skill/Item/Defend/Escape）。</summary>
    public ActionType ActionType { get; set; }

    /// <summary>対象のentity_id（対象がいない行動ではNULL）。</summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// ダメージ量等の詳細（DamageResult等をシリアライズしたJSON文字列）。
    /// 固定スキーマを持たせず、常にアプリ側でシリアライズ/デシリアライズする。
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>ログ発生時刻（UTC想定）。</summary>
    public DateTime CreatedAt { get; set; }
}
