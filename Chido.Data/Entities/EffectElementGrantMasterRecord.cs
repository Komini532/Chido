using Chido.Core.Battle.Damage;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_element_grant_master (45): 状態変化のうち一時的な属性付与成分。
/// %補正ではなくビット加算であるため、既存3種のいずれとも性質が異なり専用マスタとして持つ。
/// 後天的・一時的な属性付与には保持数の上限を設けない
/// （本体属性・スキル属性・モーション属性が「最大2つまでを目安」とするのとは異なる）。
///
/// 対応するインスタンス側テーブルは存在しない。付与元である 10c / 14番のどちらにも elements 列が無く、
/// 付与される属性は effect_key ごとにマスタ側で固定されているため
/// （chido_effect_disable_move_master と同じ扱い）。
/// </summary>
public class EffectElementGrantMasterRecord
{
    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 付与する属性（ビット列）。
    /// ダメージ計算時、対象の実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」として集計される。
    /// </summary>
    public Element Elements { get; set; }
}
