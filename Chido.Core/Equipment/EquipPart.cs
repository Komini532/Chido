using System;

namespace Chido.Core.Equipment;

/// <summary>
/// 装備可能パーツ。1つの装備が複数スロットのいずれかを選んで装着できる（択一の候補提示）ことを
/// 許容するためビット列で保持する。したがって物理カラム（chido_player_equipment_slot の各列）と
/// 1対1に対応する保証はなく、装備がどのスロットに属するかは本値を制約条件としてアプリ側が解決する。
///
/// プレイヤーと敵は完全に対称なスロット構造を持つ（戦闘システム 2.5参照）。
/// </summary>
// DB(chido_equipment_master.equip_parts: INT UNSIGNED)にそのまま永続化されるため、値を明示している。
// 今後の変更は末尾への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
[Flags]
public enum EquipPart
{
    None   = 0,
    Weapon = 1 << 0,
    Head   = 1 << 1,
    Chest  = 1 << 2,
    Legs   = 1 << 3,

    /// <summary>
    /// アクセサリ枠1。将来の複数化を見越した番号付き。追加時は Accessory2 を末尾に足し、
    /// プレイヤー側・敵側のスロットテーブルへ同時に列を追加する運用とする。
    /// </summary>
    Accessory1 = 1 << 4,
}
