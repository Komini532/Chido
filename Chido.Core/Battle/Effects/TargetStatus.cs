namespace Chido.Core.Battle.Effects;

/// <summary>
/// StatusModifier が変動させる対象ステータス。
///
/// 【合成の意味が2系統に分かれる点に注意】
/// MaxLife / PAtk / PDef / MAtk / MDef を指す行は、レイヤー内加算の結果 (1 + Σr) を
/// 「状態変化補正倍率」として乗算レイヤーへ供給する（戦闘システム 2.3）。
/// 一方 DamageResistRate を指す行は、加算結果 Σr を (10000 - Σr) / 10000 の係数として
/// ダメージパイプラインの PostDefense へ供給し、乗算レイヤーには一切入らない（同 5.1・5.4）。
/// 同じ rate 列を読みながら、本列がどちらを指すかで合成の意味が変わる。
/// </summary>
// DB(chido_effect_status_modifier_master.target_status: TINYINT UNSIGNED)にそのまま永続化されるため、
// 数値を明示している。今後の変更は末尾への追加のみとし、
// 既存メンバーの並び替え・削除は行わないこと。
//
// 値の割り当ては chido-database-design.md の 16番の列挙順
// （HP / 物理攻撃 / 物理防御 / 魔法攻撃 / 魔法防御 / 素早さ / 運 / ダメージ軽減率）に従う。
// Speed と Luck は現時点では変動対象外だが（変動要因は装備効果のみ。戦闘システム 2.3）、
// 「対象ステータスを決め打ちした設計にしない」という方針に沿って番号を先に確保している。
public enum TargetStatus
{
    MaxLife = 0,
    PAtk    = 1,
    PDef    = 2,
    MAtk    = 3,
    MDef    = 4,

    /// <summary>将来拡張用に番号を確保。現時点では状態変化による変動対象外（戦闘システム 2.3）。</summary>
    Speed = 5,

    /// <summary>将来拡張用に番号を確保。現時点では状態変化による変動対象外（戦闘システム 2.3）。</summary>
    Luck = 6,

    /// <summary>
    /// ダメージ軽減率（DRR）。防御（Defend）が付与する。DEF への補正ではなく最終ダメージへの
    /// 乗算係数として表現することで、数値インフレに対して意味が一定に保たれる（戦闘システム 5.1）。
    /// </summary>
    DamageResistRate = 7,
}
