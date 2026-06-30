using System;
using System.Numerics;
using RpgBot.Core.Stats;

namespace RpgBot.Core.Entities;

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

    int   Speed { get; } // 行動順に影響
    Ratio Luck  { get; } // アイテムドロップ率に影響

    bool IsAlive { get; }

    // プレイヤー/敵で処理を共通化するためインターフェイスに定義
    BigInteger TakeDamage(BigInteger rawDamage);
    BigInteger Heal(BigInteger amount);
}
