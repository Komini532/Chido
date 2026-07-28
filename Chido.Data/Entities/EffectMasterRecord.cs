using Chido.Core.Battle.Effects;

namespace Chido.Data.Entities;

/// <summary>
/// chido_effect_master: 状態変化マスタ。
/// 効果種別ごとにサブテーブル（16〜18番・45番）へ分割する。
/// </summary>
public class EffectMasterRecord
{
    /// <summary>可読キー。</summary>
    public string EffectKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>説明文。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 保有効果種別（ビット列）。各サブテーブルの行の有無に対応する非正規化キャッシュであり、
    /// 真実の情報源はサブテーブル側。整合性を保つ責務はアプリ側にある。
    /// </summary>
    public EffectType EffectTypes { get; set; }

    /// <summary>
    /// 戦闘終了時に解除するか。書き込み先の判定に使用する。
    /// Player: true のとき chido_battle_effect（戦闘終了時に除去）／false のとき chido_player_effect（永続化）。
    /// Enemy : この値に関わらず常に chido_battle_effect（敵は出現の都度使い捨てのため永続化する意味を持たない）。
    ///
    /// false かつ duration_actions が NULL の組み合わせは禁止（＝真に永久な状態変化を作らない）。
    /// 加算合成される永続デバフが単調増加し、上限なくステータスを蝕むため。
    /// </summary>
    public bool ClearOnBattleEnd { get; set; }
}
