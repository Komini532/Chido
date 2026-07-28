using System;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Entities;

/// <summary>
/// 戦闘に参加する実体。プレイヤーと敵は共通の戦闘システム・共通の計算式で扱われるため、
/// ダメージ計算やターン解決はこのインターフェイス越しに書かれる。
/// </summary>
public interface IEntity
{
    /// <summary>バトル中の対象識別（DamageContext.AttackerId 等で使用）。</summary>
    Guid Id { get; }

    /// <summary>バトルログ・Discord表示用。</summary>
    string Name { get; }

    /// <summary>
    /// レベル。プレイヤーは経験値から導出し（LevelCalculator）、敵は累積敵レベルから直接与えられる。
    /// 全ステータスの基礎値がこの値から算出される。
    /// </summary>
    BigInteger Level { get; }

    /// <summary>最大HP。レベル・装備・状態変化から都度算出される。</summary>
    BigInteger MaxLife { get; }

    /// <summary>
    /// 現在HP。<see cref="MaxLife"/> を超えうる（オーバーヒールを許容し、クランプは一切行わない）。
    /// </summary>
    BigInteger CurrentLife { get; }

    BigInteger PAtk { get; }
    BigInteger PDef { get; }
    BigInteger MAtk { get; }
    BigInteger MDef { get; }

    /// <summary>行動順に影響。Scale × Shape の枠組みの外にあり、変動要因は装備効果のみ。</summary>
    int Speed { get; }

    /// <summary>アイテムドロップ率に影響。基本0%で、変動要因は装備効果のみ。</summary>
    Ratio Luck { get; }

    /// <summary>実効属性 ＝ 本体属性 ∪ 装備属性 ∪ 一時付与属性（戦闘システム 2.4）。</summary>
    Element Elements { get; }

    /// <summary>
    /// ダメージ軽減率（DRR）。併存する状態変化インスタンスの加算合成 Σr。
    /// ステータスの乗算レイヤーには入らず、(10000 - Σr) ÷ 10000 の係数として
    /// ダメージパイプラインの PostDefense へ供給される（戦闘システム 5.1・5.4）。
    /// </summary>
    Ratio DamageResistRate { get; }

    /// <summary>
    /// 現在HPが残っているか。ターン処理の途中で撃破を検知するための述語。
    /// 参加者としての戦闘不能・離脱の一次情報は BattleParticipant.Status が持つ。
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// ダメージを適用し、<b>実効ダメージ</b>（＝ min(damage, 適用直前の現在HP)）を返す。
    /// 戻り値は与ダメージ帰属・被攻撃TP・報酬ゲートが共通で参照する基準量（戦闘システム 6.2）。
    /// </summary>
    BigInteger TakeDamage(BigInteger damage);

    /// <summary>回復を適用し、実際に増加した量を返す。<see cref="MaxLife"/> でのクランプは行わない。</summary>
    BigInteger Heal(BigInteger amount);
}
