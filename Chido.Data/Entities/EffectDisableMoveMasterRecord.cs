using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_disable_move_master (18): 状態変化のうち行動不能成分。
/// 確率のみで完結する効果のため、対応するインスタンス側テーブルは存在しない。
/// </summary>
public class EffectDisableMoveMasterRecord
{
    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 行動不能率。付与時に固定せず、保持者が行動しようとするたびに引く確率。
    /// 成立時にスキップされるのはスキル1本ぶんのモーション再生のみで、
    /// ターン消費・相手の反撃・残り有効行動数の減衰は成否によらず常に行われる
    /// （TP+100 はモーション再生を契機とするため成立時は発生しない）。
    /// 併存する複数インスタンスは instance_id 昇順に独立抽選し、最初の成功で打ち切る。
    /// </summary>
    public Ratio DisableRate { get; set; }
}
