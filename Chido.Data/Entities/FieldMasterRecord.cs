namespace Chido.Data.Entities;

/// <summary>
/// chido_field_master (39): フィールドマスタ。
/// 最初のフィールドは「草原」固定だが、chido_channel_state.current_field_key の DEFAULT 値としては
/// 表現せず、アプリ側の定数（GameConstants.GrasslandFieldKey）として解決する。
///
/// フィールド属性（移動先抽選にプレイヤーの意思を薄く反映するロジック）に対応するカラムは、
/// 当該仕様が将来のアップデート項目として保留されているため持たせない。
/// </summary>
public class FieldMasterRecord
{
    /// <summary>可読キー（例: 'grassland'）。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>表示名（例: '草原'）。</summary>
    public string Name { get; set; } = string.Empty;
}
