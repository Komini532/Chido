namespace Chido.Core.Battle.Effects;

/// <summary>
/// 状態変化の付与要因。grant_source_key が「どのテーブルの可読キーなのか」を示す型タグであり、
/// grant_source_key の値そのものからは導出できない（可読キーの名前空間はテーブルごとに独立しているため、
/// skill_key = 'poison_touch' と equip_key = 'poison_touch' は値だけでは区別できない）。
/// 重複付与の判定キーに本値が必要なのはこのため（戦闘システム 5.4参照）。
/// </summary>
// DB(chido_battle_effect.affect_reason / chido_player_effect.affect_reason: TINYINT UNSIGNED)に
// そのまま永続化されるため、数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと（将来 Equipment 等が追加されうる）。
public enum AffectReason
{
    /// <summary>状態変化付与モーション由来。grant_source_key は skill_key。</summary>
    Skill = 0,

    /// <summary>敵の出現時の初期付与由来。grant_source_key は NULL、granter は自身。</summary>
    Auto = 1,
}
