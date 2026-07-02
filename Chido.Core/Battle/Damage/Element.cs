using System;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 属性 (10種、固定)。エンティティ・スキル本体が持つ属性は最大2つまでという運用ルールだが、
/// これはコンテンツ設計上の制約であり型としては特に強制しない。
/// None (=0) は「属性未設定」を表すフラグ値であり、10種のうちの1つである無属性 (Neutral) とは別概念。
/// </summary>
[Flags]
public enum Element
{
    None    = 0,
    Fire    = 1 << 0, // 火
    Water   = 1 << 1, // 水
    Grass   = 1 << 2, // 草
    Earth   = 1 << 3, // 土 (大地)
    Sky     = 1 << 4, // 空 (飛行)
    Thunder = 1 << 5, // 雷
    Ice     = 1 << 6, // 氷
    Light   = 1 << 7, // 光
    Dark    = 1 << 8, // 闇
    Neutral = 1 << 9, // 無
}
