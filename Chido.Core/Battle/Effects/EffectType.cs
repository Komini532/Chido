using System;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 状態変化が保有する効果種別。1つの状態変化が複数の種別を兼ねうる（マルチネイチャー）ため
/// ビット列で保持する。各サブテーブルに行が存在するかの非正規化キャッシュであり、
/// 真実の情報源はサブテーブル側。整合性の維持はアプリ側の責務（戦闘システム 5.4参照）。
/// </summary>
// DB(chido_effect_master.effect_types: INT UNSIGNED)にそのまま永続化されるため、値を明示している。
// 今後の変更は末尾への追加のみとし、既存メンバーの並び替え・削除は行わないこと。
[Flags]
public enum EffectType
{
    None = 0,

    /// <summary>
    /// ステータス変動（%補正）。ダメージ軽減率（DRR）も本種別に編入されている。
    /// DRR は専用サブタイプを設けず TargetStatus の一値として表現する（戦闘システム 5.4参照）。
    /// </summary>
    StatusModifier = 1 << 0,

    /// <summary>継続ダメージ。補正値ではなく独立したダメージ発生源であり、各インスタンスがそれぞれ発動する。</summary>
    SlipDamage = 1 << 1,

    /// <summary>行動不能。付与時に固定せず、保持者が行動しようとするたびに disable_rate を引く。</summary>
    DisableMove = 1 << 2,

    /// <summary>一時的な属性付与。%補正ではなくビット加算のため他の3種と性質が異なる。</summary>
    ElementGrant = 1 << 3,
}
