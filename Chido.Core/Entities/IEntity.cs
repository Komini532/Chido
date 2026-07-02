using System;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Entities;

public interface IEntity
{
    Guid   Id   { get; } // バトル中の対象識別 (DamageContext.AttackerId 等で使用)
    string Name { get; } // バトルログ・Discord表示用

    BigInteger MaxLife     { get; }
    BigInteger CurrentLife { get; }
    BigInteger PAtk        { get; }
    BigInteger PDef        { get; }
    BigInteger MAtk        { get; }
    BigInteger MDef        { get; }

    int     Speed   { get; } // 行動順に影響
    Ratio   Luck    { get; } // アイテムドロップ率に影響
    Element Element { get; } // 属性 (最大2つ想定)。ダメージ計算の属性相性判定に使用

    bool IsAlive { get; }

    // プレイヤー/敵で処理を共通化するためインターフェイスに定義
    BigInteger TakeDamage(BigInteger rawDamage);
    BigInteger Heal(BigInteger amount);
}
