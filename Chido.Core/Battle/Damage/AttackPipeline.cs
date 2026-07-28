using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Damage.Modifiers;
using Chido.Core.Entities;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 攻撃パイプライン（戦闘システム 5.1）。3本あるパイプラインのうち、被防御係数・クリティカル・
/// 属性相性・DRR のすべてが適用される唯一の経路。
///
/// <code>
/// 1. PreDefense  … 属性補正のみ（有効ATK × 1.3^x）
/// 2. 基礎ダメージ … 有効ATK' + DEF = 0 なら 0、それ以外は max(0, 有効ATK'² ÷ (有効ATK' + DEF))
/// 3. PostDefense … power → クリティカル → DRR係数
/// 4. Flat        … 固定加算（現時点でデータ供給源を持たず常に恒等）
/// 5. Final       … クランプ
/// 6. 最低ダメージ 1 を保証
/// </code>
///
/// <b>入力の有効ATK・有効DEFは 2.3 の戦闘時ステータスである。</b>
/// StatusModifier によるバフ・デバフは既に織り込まれており、パイプライン内で再適用しない
/// （PreDefense の二重適用を避けるための切り分け）。
///
/// <b>命中失敗はパイプラインに入らない。</b> accuracy_rate の判定を外したモーションは
/// ダメージ0を通すのではなく、そもそもここへ到達しない。したがって最低ダメージ1の保証にも
/// 到達せず、TP契機も発火しない。
///
/// <b>このパイプラインは最終ダメージしかクランプしない。</b>
/// 適用先の現在HPは MaxLife を超えうるし、下限は戦闘不能（0）である。
/// </summary>
public static class AttackPipeline
{
    /// <summary>
    /// 攻撃モーション1回分のダメージを算出する。HPへの適用は呼び出し側が行う。
    /// </summary>
    /// <param name="attacker">攻撃者。有効ATKと識別子の供給元。</param>
    /// <param name="target">対象。有効DEFと実効属性、保持中のDRRの供給元。</param>
    /// <param name="attackType">参照する攻撃力・防御力の組（物理／魔法）。</param>
    /// <param name="power">威力。整数%（通常攻撃 = 100）。</param>
    /// <param name="motionElements">
    /// 攻撃モーションの属性。相性判定の攻撃側はスキル属性でも攻撃者の実効属性でもなく、
    /// モーション属性である（戦闘システム 5.3）。
    /// </param>
    /// <param name="isCritical">クリティカルの抽選結果。抽選そのものは行動側が担う。</param>
    /// <param name="sourceName">ログ表示用のスキル名。</param>
    /// <param name="extraModifiers">Flat フェーズ等の拡張。現時点でデータ供給源は無い。</param>
    public static DamageResult Resolve(
        IEntity attacker,
        IEntity target,
        AttackType attackType,
        int power,
        Element motionElements = Element.None,
        bool isCritical = false,
        string? sourceName = null,
        IEnumerable<IDamageModifier>? extraModifiers = null)
    {
        var rawAtk = attackType == AttackType.Physical ? attacker.PAtk : attacker.MAtk;
        var defense = attackType == AttackType.Physical ? target.PDef : target.MDef;

        var builder = new DamageContext.Builder(attacker.Id, attackType, rawAtk);

        // PreDefense は属性補正のみ
        builder.AddModifier(ElementAffinityModifier.Create(motionElements, target.Elements));

        // PostDefense は power → クリティカル → DRR の順（追加順が適用順になる）
        builder.AddModifier(new PowerModifier(power, sourceName));
        if (isCritical) builder.AddModifier(RatioMultiplierModifier.Critical(GameConstants.CriticalMultiplier));
        builder.AddModifier(DamageResistModifier.Create(target.DamageResistRate));

        if (extraModifiers is not null)
        {
            foreach (var modifier in extraModifiers) builder.AddModifier(modifier);
        }

        return Calculate(builder.Build(), defense);
    }

    /// <summary>
    /// 組み立て済みのコンテキストからダメージを算出する。
    /// Modifier を直接制御したい場合の入口（テスト・将来の特殊処理用）。
    /// </summary>
    public static DamageResult Calculate(DamageContext ctx, BigInteger defense)
    {
        // 1. PreDefense: 有効ATKの確定。属性補正がここでATKに乗る
        var effectiveAtk = ApplyPhase(ctx.RawAtk, ModifierPhase.PreDefense, ctx);

        // 2. 基礎ダメージ
        var baseDamage = BaseDamageFormula.Calculate(effectiveAtk, defense);

        // 3〜5. PostDefense → Flat → Final
        var damage = ApplyPhase(baseDamage, ModifierPhase.PostDefense, ctx);
        damage = ApplyPhase(damage, ModifierPhase.Flat, ctx);
        damage = ApplyPhase(damage, ModifierPhase.Final, ctx);

        // 6. 最低ダメージ1。DRR の合成係数が負に振れた場合もここで吸収される
        damage = BigInteger.Max(GameConstants.MinimumDamage, damage);

        var modifierLog = ctx.Modifiers
            .Where(m => m.LogLabel is not null)
            .Select(m => m.LogLabel!)
            .ToList()
            .AsReadOnly();

        return new DamageResult(
            FinalDamage: damage,
            EffectiveAtk: effectiveAtk,
            Defense: defense,
            BaseDamage: baseDamage,
            AttackType: ctx.AttackType,
            AttackerId: ctx.AttackerId,
            ModifierLog: modifierLog);
    }

    private static BigInteger ApplyPhase(BigInteger current, ModifierPhase phase, DamageContext ctx)
        => ctx.Modifiers
            .Where(m => m.Phase == phase)
            .Aggregate(current, (value, modifier) => modifier.Apply(value, ctx));
}
