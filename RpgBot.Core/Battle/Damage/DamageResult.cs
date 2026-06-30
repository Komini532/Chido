using System;
using System.Collections.Generic;
using System.Numerics;

namespace RpgBot.Core.Battle.Damage;

public sealed record DamageResult(
    BigInteger            FinalDamage,
    BigInteger            BaseDefense,       // 貫通前の素の防御値 (ログ用)
    BigInteger            EffectiveDefense,  // 貫通後の有効防御値 (ログ用)
    BigInteger            PreModifierDamage, // Modifier 適用前のダメージ (ログ用)
    AttackType            AttackType,
    Guid                  AttackerId,
    IReadOnlyList<string> ModifierLog        // LogLabel が null でない Modifier のラベル列
);
