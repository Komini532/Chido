using System;
using System.Numerics;
using Chido.Core.Stats;

namespace Chido.Core.Entities;

public abstract class EntityBase : IEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public abstract string Name { get; }

    public BigInteger MaxLife     { get; protected set; }
    public BigInteger CurrentLife { get; protected set; }
    public BigInteger PAtk        { get; protected set; }
    public BigInteger PDef        { get; protected set; }
    public BigInteger MAtk        { get; protected set; }
    public BigInteger MDef        { get; protected set; }
    public int        Speed       { get; protected set; }
    public Ratio       Luck       { get; protected set; }

    public bool IsAlive => CurrentLife > BigInteger.Zero;

    /// <summary>実ダメージ量を返す。最低 1 保証。</summary>
    public virtual BigInteger TakeDamage(BigInteger rawDamage)
    {
        var actual = BigInteger.Max(BigInteger.One, rawDamage);
        var before = CurrentLife;
        CurrentLife = BigInteger.Max(BigInteger.Zero, CurrentLife - actual);
        return before - CurrentLife;
    }

    /// <summary>実回復量を返す。MaxLife を超えない。</summary>
    public virtual BigInteger Heal(BigInteger amount)
    {
        var headroom = MaxLife - CurrentLife;
        var actual   = BigInteger.Min(amount, headroom);
        CurrentLife += actual;
        return actual;
    }

    /// <summary>現在HP割合 (表示・AI判断用)</summary>
    public Ratio LifeRatio
    {
        get
        {
            if (MaxLife == BigInteger.Zero) return Ratio.Zero;
            // decimal を経由せず BigInteger のまま計算
            return Ratio.FromPermyriad((int)(CurrentLife * 10000 / MaxLife));
        }
    }
}
