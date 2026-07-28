using Chido.Core.Battle.Damage;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;

namespace Chido.Data.Entities;

/// <summary>
/// chido_enemy_master: 敵マスタ。
/// Luck は基本0%（プレイヤー・敵共通）で変動要因が装備効果のみのため、対応列を持たない。
/// </summary>
public class EnemyMasterRecord
{
    /// <summary>可読キー。chido_battle_enemy.master_key から参照される。</summary>
    public string EnemyKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>敵画像URL。Discord埋め込みに使用。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 個体の希少度。表示専用の値であり、敵の出現抽選には使用しない
    /// （抽選のレアリティは chido_enemy_group_master.rarity が持つ）。
    /// </summary>
    public Rarity Rarity { get; set; }

    /// <summary>
    /// 敵本体の属性（ビット列）。0 = 属性なし。
    /// 実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」で算出される。
    /// プレイヤーの本体属性は常に 0（装備由来のみ）であるため、対応する列を持たない。
    /// </summary>
    public Element Elements { get; set; }

    /// <summary>
    /// HP Shape（種族値に相当する正規化されたステータス倍率）。1.00 を 100 として格納する。
    /// permyriad ではないため Ratio の対象外。基礎ステータス = レベル × Scale × Shape。
    /// </summary>
    public ushort HpShape { get; set; }

    /// <summary>物理攻撃力 Shape（100 = 等倍）。</summary>
    public ushort PAtkShape { get; set; }

    /// <summary>物理防御力 Shape（100 = 等倍）。</summary>
    public ushort PDefShape { get; set; }

    /// <summary>魔法攻撃力 Shape（100 = 等倍）。</summary>
    public ushort MAtkShape { get; set; }

    /// <summary>魔法防御力 Shape（100 = 等倍）。</summary>
    public ushort MDefShape { get; set; }

    /// <summary>
    /// 強さ倍率。戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正 × 状態変化補正。
    /// ボスとして出現させる場合などに 2倍等を設定する。プレイヤーは常に等倍。
    /// </summary>
    public Ratio StrengthRate { get; set; }

    /// <summary>経験値倍率。strength_rate とは独立した値。</summary>
    public Ratio ExpRate { get; set; }

    /// <summary>
    /// 素早さ。Scale × Shape の枠組みには含まれない固定値（プレイヤーは基本500）。
    /// 変動要因は装備効果のみ（強さ倍率・状態変化補正の影響を受けない）。
    /// </summary>
    public ushort Speed { get; set; }

    /// <summary>
    /// 出現時の初期TP（0〜1000）。chido_battle_participant.current_tp の初期値。
    /// プレイヤーは常に0で初期化されるためこの非対称は意図的。
    /// 初手から require_tp&gt;0 のスキルを撃たせたい敵に、その分の初期値を持たせる。
    /// </summary>
    public ushort InitialTp { get; set; }

    /// <summary>行動パターン（スキルの選択規則）。</summary>
    public ActionPatternType ActionPatternType { get; set; }

    /// <summary>
    /// 味方対象モーションの対象選択規則（種族単位）。action_pattern_type と対をなす。
    /// 敵には [対象] 入力も CurrentTarget の流用もないため、target_rule=味方 のモーションを本列で解決する。
    /// </summary>
    public AllyTargetRule AllyTargetRule { get; set; }
}
