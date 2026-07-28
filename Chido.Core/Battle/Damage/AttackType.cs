namespace Chido.Core.Battle.Damage;

/// <summary>
/// 攻撃種別。ダメージ計算パイプラインで参照する攻撃力・防御力（物理／魔法）の組を選択する。
/// </summary>
// DB(chido_skill_motion_attack_master.attack_type 等: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。既存行の意味が変わらないよう、今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
public enum AttackType
{
    Physical = 0,
    Magical  = 1,
    // 「防御完全無視」は導入しない。防御貫通率は威力の一次元性を壊し、かつ StatusModifier の
    // DEF デバフで表現可能な冗長機能であるため廃止された（戦闘システム 5.1参照）。
}
