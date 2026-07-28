using System;
using System.Collections.Generic;
using System.Numerics;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// ダメージパイプラインの結果。攻撃・スリップの双方が返す。
/// </summary>
/// <param name="FinalDamage">最低1を保証したあとの最終ダメージ。実際にHPへ適用する量。</param>
/// <param name="EffectiveAtk">属性補正を適用したあとの有効ATK（ログ用）。</param>
/// <param name="Defense">参照した対象の防御力（ログ用）。防御貫通は廃止されたため素の値と一致する。</param>
/// <param name="BaseDamage">PostDefense 適用前の基礎ダメージ（ログ用）。</param>
/// <param name="ModifierLog">LogLabel が null でない Modifier のラベル列。</param>
public sealed record DamageResult(
    BigInteger FinalDamage,
    BigInteger EffectiveAtk,
    BigInteger Defense,
    BigInteger BaseDamage,
    AttackType AttackType,
    Guid AttackerId,
    IReadOnlyList<string> ModifierLog);
