namespace Chido.Core.Entities.Enemies;

/// <summary>
/// 敵が target_rule = 味方 のモーションを実行する場合の対象選択規則（敵の種族単位）。
/// スキルの選択規則である <see cref="ActionPatternType"/> と対をなす。
///
/// 敵にはプレイヤーのような [対象] 入力も CurrentTarget の流用も無いため、別の規則で解決する。
/// 候補集合は自軍側の Active な参加者であり、実行者自身を含む（プレイヤー側と対称。戦闘システム 4.2参照）。
///
/// 規則が敵単位である帰結として、1体の敵が持つ複数の味方対象スキルはすべて同じ規則で対象を選ぶ。
/// 「取り巻きを回復しつつ自分だけを強化」は、強化側モーションを target_rule = 自分自身 に
/// することで表現する（target_rule がモーション単位であることが逃げ道になっている）。
/// </summary>
// DB(chido_enemy_master.ally_target_rule: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。番号は族ごとに範囲を予約しており、欠番は詰めない。
// 今後の変更は予約帯への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
public enum AllyTargetRule
{
    // --- ランダム系（0-9。2-9 は予約） ---

    /// <summary>候補（自分を含む）から完全ランダムに1体。</summary>
    PureRandom = 0,

    /// <summary>候補から自分を除いた集合からランダム。空なら自分（単独時のフォールバックが要るのは本値のみ）。</summary>
    RandomExceptSelf = 1,

    // --- 固定対象系（10-19。13-19 は予約） ---
    // display_order の値で定義する（display_order は組の member_index の恒等複製で0起点であるため）。
    // 「ボスとその取り巻き」ではボスが member_index = 0 となる想定で、DisplayOrder0 が「ボスを狙う」に対応する。

    /// <summary>display_order = 0 の味方。将来実装。</summary>
    DisplayOrder0 = 10,

    /// <summary>display_order = 1 の味方。将来実装。</summary>
    DisplayOrder1 = 11,

    /// <summary>display_order = 2 の味方。将来実装。</summary>
    DisplayOrder2 = 12,

    // --- 情報参照系（20-29。25-29 は予約） ---

    /// <summary>物理攻撃力が最大の味方。将来実装。</summary>
    HighestPAtk = 20,

    /// <summary>魔法攻撃力が最大の味方。将来実装。</summary>
    HighestMAtk = 21,

    /// <summary>物理防御力が最大の味方。将来実装。</summary>
    HighestPDef = 22,

    /// <summary>魔法防御力が最大の味方。将来実装。</summary>
    HighestMDef = 23,

    /// <summary>
    /// CurrentLife ÷ MaxLife が最小の集合からランダム。
    /// 比較は除算せず交差乗算で行う（a.CurrentLife × b.MaxLife &lt; b.CurrentLife × a.MaxLife）。
    /// MaxLife &gt; 0 が常に保証されるため成立し、浮動小数点を通さずオーバーヒールも自然に扱える。
    /// 候補集合は Active で定義したうえで、内部で追加条件 CurrentLife &gt; 0 を持つ
    /// （「HP0 だが Active」が生じた場合に回復対象が瀕死者へ固定される退化を防ぐフェイルセーフ）。
    /// </summary>
    LowestLifeRatio = 24,
}

/// <summary>
/// <see cref="AllyTargetRule"/> のうち、現行フェーズで実装済みの規則を判定する。
/// マスタデータの検証と実行時のフォールバック判定が同じ1箇所を参照するために置いている。
/// </summary>
public static class AllyTargetRuleExtensions
{
    /// <summary>現行実装は PureRandom / RandomExceptSelf / LowestLifeRatio の3規則のみ。他は予約値。</summary>
    public static bool IsImplemented(this AllyTargetRule rule) => rule is
        AllyTargetRule.PureRandom or
        AllyTargetRule.RandomExceptSelf or
        AllyTargetRule.LowestLifeRatio;
}
