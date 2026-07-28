using System.Numerics;
using Chido.Core.Progression;

namespace Chido.Data.Entities;

/// <summary>
/// chido_title_master: 称号マスタ。
/// 入手条件に応じてアプリ側が condition_key／condition_value のいずれかと照らし合わせる。
/// 特殊な取得条件は acquisition_type に新しい値を追加したうえで両判定値カラムを NULL のまま扱い、
/// ハードコード実装として個別に対応する想定。
/// </summary>
public class TitleMasterRecord
{
    /// <summary>可読キー。</summary>
    public string TitleKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 表示用絵文字。Unicode文字、または Discord カスタム絵文字の完成済みタグ文字列
    /// （&lt;:name:id&gt;）をそのまま格納する。
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>入手条件種別。今後拡張予定。</summary>
    public TitleAcquisitionType AcquisitionType { get; set; }

    /// <summary>
    /// 判定値（識別ID形式）。ItemObtained → item_key、EnemyDefeated → enemy_key を参照する
    /// （参照先が acquisition_type により分岐する）。
    /// </summary>
    public string? ConditionKey { get; set; }

    /// <summary>
    /// 判定値（数値）。LevelReached → レベル閾値、CurrencyReached → 所持金額閾値。
    /// 比較対象（exp由来のレベル、chido_player_currency.amount）と型を揃えている。
    /// </summary>
    public BigInteger? ConditionValue { get; set; }
}
