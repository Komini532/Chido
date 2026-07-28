using Chido.Core.Battle.Effects;

namespace Chido.Core.Stats;

/// <summary>
/// 保持中の状態変化インスタンス1件ぶんのステータス変動。
/// chido_effect_status_modifier_instance の1行（または fixed_rate を持つマスタ行）に対応する。
///
/// <see cref="TargetStatus"/> を列挙値のまま持つのは、対象ステータスを決め打ちした設計にしないため
/// （戦闘システム 2.3 が Speed / Luck の将来解禁を見込んでいる）。
///
/// <b>合成の意味が対象により2系統に分かれる点に注意</b>（戦闘システム 2.3・5.1・5.4）。
/// MaxLife / PAtk / PDef / MAtk / MDef を指す行は、加算合成の結果 (1 + Σr) が
/// 状態変化補正倍率として乗算レイヤーへ入る。一方 DamageResistRate を指す行は
/// Σr を (10000 - Σr) ÷ 10000 の係数としてダメージパイプラインの PostDefense へ供給し、
/// 乗算レイヤーには一切入らない。同じ Rate を読みながら合成先が異なる。
/// </summary>
public readonly record struct StatusModifier(TargetStatus TargetStatus, Ratio Rate);
