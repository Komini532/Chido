using System;
using System.Collections.Generic;
using System.Numerics;

namespace Chido.Core.Battle.Damage;

/// <summary>
/// 1回のダメージ算出の入力。Modifier はここに積み上げられ、フェーズごとに連鎖適用される。
///
/// <b>防御貫通率（DefensePenetration）は廃止された</b>（戦闘システム 5.1）。
/// 威力の一次元性を壊し、かつ StatusModifier の DEF デバフで表現可能な冗長機能であったため。
/// </summary>
public sealed class DamageContext
{
    /// <summary>攻撃者を識別する Id。</summary>
    public Guid AttackerId { get; }

    /// <summary>物理 or 魔法。参照する攻撃力・防御力の組を選択する。</summary>
    public AttackType AttackType { get; }

    /// <summary>
    /// 有効ATK（2.3 の戦闘時ステータス。StatusModifier 込み）。
    /// スリップの場合は付与時点のスナップショットATK。
    /// </summary>
    public BigInteger RawAtk { get; }

    private readonly IReadOnlyList<IDamageModifier> _modifiers;
    public IReadOnlyList<IDamageModifier> Modifiers => _modifiers;

    private DamageContext(
        Guid attackerId,
        AttackType attackType,
        BigInteger rawAtk,
        List<IDamageModifier> modifiers)
    {
        AttackerId = attackerId;
        AttackType = attackType;
        RawAtk = rawAtk;
        _modifiers = modifiers.AsReadOnly();
    }

    public sealed class Builder
    {
        private readonly Guid _attackerId;
        private readonly AttackType _attackType;
        private readonly BigInteger _rawAtk;
        private readonly List<IDamageModifier> _modifiers = [];

        public Builder(Guid attackerId, AttackType attackType, BigInteger rawAtk)
        {
            _attackerId = attackerId;
            _attackType = attackType;
            _rawAtk = rawAtk;
        }

        /// <summary>
        /// Modifier を追加する。<b>追加順が適用順になる</b>。
        /// 同一フェーズ内の順序は仕様で定められているため（PostDefense は power → クリティカル → DRR）、
        /// パイプライン側がその順に積む責務を負う。
        /// </summary>
        public Builder AddModifier(IDamageModifier? modifier)
        {
            if (modifier is not null) _modifiers.Add(modifier);
            return this;
        }

        public DamageContext Build() => new(_attackerId, _attackType, _rawAtk, [.. _modifiers]);
    }
}
