using System;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Entities;

/// <summary>
/// プレイヤー。共通の計算式に対して、Shape・強さ倍率・本体属性のいずれも固定値を与える側になる。
/// 個体差はレベル（経験値由来）と装備だけで表現される。
/// </summary>
public sealed class Player : EntityBase
{
    /// <summary>Discordユーザーの永続ID。chido_player.user_id に対応する。</summary>
    public ulong UserId { get; }

    public override string Name { get; }

    /// <summary>
    /// 経験値。レベルはここから導出し、値としては保持しない。
    /// 表示と計算の双方が同一の導出関数を通るため、あらゆる読み出しで異常値が遮断される。
    /// </summary>
    public BigInteger Exp { get; private set; }

    public override BigInteger Level => LevelCalculator.FromExp(Exp);

    /// <summary>プレイヤーの Shape は全ステータス等倍（戦闘システム 2.3）。</summary>
    protected override StatShape Shape => StatShape.Player;

    /// <summary>プレイヤーの強さ倍率は常に等倍。個別設定を持つのは敵のみ。</summary>
    protected override Ratio StrengthRate => Ratio.Full;

    protected override int BaseSpeed => GameConstants.PlayerBaseSpeed;

    /// <summary>
    /// プレイヤーの本体属性は常に属性なし。属性は装備由来のみであり、
    /// 対応するデータをそもそも持たない（戦闘システム 2.4）。
    /// </summary>
    protected override Element InnateElements => Element.None;

    public Player(ulong userId, string name, BigInteger exp, Guid? entityId = null)
    {
        UserId = userId;
        Name = name;
        Exp = exp;

        // 参加者インスタンスの使い捨てGuid。セッションをまたいで再利用されない
        if (entityId is { } id) Id = id;
    }

    /// <summary>経験値を加算する。レベルは次回の読み出しで自動的に追従する。</summary>
    public void AddExp(BigInteger amount)
    {
        if (amount <= BigInteger.Zero) return;
        Exp += amount;
    }
}
