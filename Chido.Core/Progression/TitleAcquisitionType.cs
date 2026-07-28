namespace Chido.Core.Progression;

/// <summary>
/// 称号の入手条件種別。判定値の参照先が本値により分岐する
/// （condition_key を見るか condition_value を見るか）。
///
/// 「プレイヤーと敵が同時に戦闘不能になる」のような特殊な取得条件は、本列挙に値を追加したうえで
/// 両判定値カラムを NULL のまま扱い、ハードコード実装として個別に対応する想定。
/// </summary>
// DB(chido_title_master.acquisition_type: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum TitleAcquisitionType
{
    /// <summary>特定アイテムの獲得。condition_key は item_key。</summary>
    ItemObtained = 0,

    /// <summary>特定の敵の撃破。condition_key は enemy_key。</summary>
    EnemyDefeated = 1,

    /// <summary>レベル到達。condition_value がレベル閾値。</summary>
    LevelReached = 2,

    /// <summary>所持金額到達。condition_value が金額閾値。</summary>
    CurrencyReached = 3,
}
