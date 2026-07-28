namespace Chido.Data.Entities;

/// <summary>
/// chido_field_transition_master (41): フィールド遷移先候補。
/// 移動先は候補リストから完全ランダムで抽選するため、重み列を持たない。
///
/// 自己参照行（例: 草原 → 草原）を置くと「そこから動かない」がデータ上の意図として明示される
/// （意図的な行き止まり）。一方、あるフィールドを遷移元とする行が1件も存在しない場合は
/// マスタ不整合とみなし、切替時に草原へフォールバックする。
/// この2つを区別できるのが自己参照行を許す理由である。
/// </summary>
public class FieldTransitionMasterRecord
{
    /// <summary>chido_field_master.field_key を参照。遷移元。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>chido_field_master.field_key を参照。遷移先候補。</summary>
    public string NextFieldKey { get; set; } = string.Empty;
}
