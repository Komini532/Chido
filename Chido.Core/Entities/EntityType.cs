namespace Chido.Core.Entities;

/// <summary>
/// 参加者の種別。プレイヤーと敵は共通の戦闘システム・共通のマスタデータで扱われ、
/// ステータス算出式も同一である（差は Shape・強さ倍率・Speed の基本値のみ。戦闘システム 2.3参照）。
/// </summary>
// DB(chido_battle_participant.entity_type: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。既存行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum EntityType
{
    Player = 0,
    Enemy  = 1,
}
