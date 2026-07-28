using Chido.Core.Battle.Effects;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_status_modifier_master (16): 状態変化のうちステータス変動成分。
/// 1つの effect_key が複数の target_status を同時に変動させるケースを想定し、複合PKで複数行を許容する。
/// </summary>
public class EffectStatusModifierMasterRecord
{
    /// <summary>chido_effect_master.effect_key を参照。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>
    /// 対象ステータス。DRR（ダメージ軽減率）も本列の一値として編入されている。
    /// HP/攻撃/防御を指す行はレイヤー内加算の結果 (1 + Σr) を状態変化補正倍率として乗算レイヤーへ供給するが、
    /// DRR を指す行は Σr を (10000 - Σr) / 10000 の係数としてダメージパイプラインの PostDefense へ供給し、
    /// 乗算レイヤーには一切入らない（同じ rate を読みながら合成の意味が異なる。アプリ側で分岐する）。
    /// </summary>
    public TargetStatus TargetStatus { get; set; }

    /// <summary>
    /// 固定変動率。符号あり。
    /// NOT NULL = マスタ定義の固定値（例: 防御 Defend の DRR 50% → 5000）。
    /// NULL = 不定値（適用時にインスタンス側 chido_effect_status_modifier_instance が変動率を保持する）。
    /// </summary>
    public Ratio? FixedRate { get; set; }
}
