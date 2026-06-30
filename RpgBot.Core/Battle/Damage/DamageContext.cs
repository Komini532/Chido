using System;
using System.Collections.Generic;
using System.Numerics;
using RpgBot.Core.Entities;
using RpgBot.Core.Stats;

namespace RpgBot.Core.Battle.Damage;

public sealed class DamageContext
{
    public Guid       AttackerId { get; }            // 攻撃者を識別する Id
    public AttackType AttackType { get; }            // 物理 or 魔法
    public BigInteger RawAtk     { get; }             // 攻撃者攻撃力

    /// <summary>
    /// 防御貫通率。
    ///   0%   = 貫通なし (デフォルト)
    ///  30%   = DEF を 70% に圧縮
    /// 100%   = 防御完全無視
    /// </summary>
    public Ratio DefensePenetration { get; }

    // ダメージ補正効果、状態変化による補正などはここに乗せる
    private readonly IReadOnlyList<IDamageModifier> _modifiers;
    public  IReadOnlyList<IDamageModifier> Modifiers => _modifiers;

    private DamageContext(
        Guid attackerId,
        AttackType attackType,
        BigInteger rawAtk,
        Ratio defensePenetration,
        List<IDamageModifier> modifiers)
    {
        AttackerId         = attackerId;
        AttackType         = attackType;
        RawAtk             = rawAtk;
        DefensePenetration = defensePenetration;
        _modifiers         = modifiers.AsReadOnly();
    }

    // ---------------------------------------------------------------
    public sealed class Builder
    {
        private readonly Guid                  _attackerId;
        private readonly AttackType            _attackType;
        private readonly BigInteger            _rawAtk;
        private          Ratio                 _defensePenetration = Ratio.Zero;
        private readonly List<IDamageModifier> _modifiers          = [];

        public Builder(Guid attackerId, AttackType attackType, BigInteger rawAtk)
        {
            _attackerId = attackerId;
            _attackType = attackType;
            _rawAtk     = rawAtk;
        }

        /// <summary>IEntity から直接生成するショートカット</summary>
        public static Builder FromEntity(IEntity attacker, AttackType attackType)
        {
            var rawAtk = attackType == AttackType.Physical ? attacker.PAtk : attacker.MAtk;
            return new Builder(attacker.Id, attackType, rawAtk);
        }

        public Builder WithDefensePenetration(Ratio penetration)
        {
            _defensePenetration = penetration;
            return this;
        }

        public Builder AddModifier(IDamageModifier modifier)
        {
            _modifiers.Add(modifier);
            return this;
        }

        public DamageContext Build()
            => new(_attackerId, _attackType, _rawAtk, _defensePenetration, [.._modifiers]);
    }
}
