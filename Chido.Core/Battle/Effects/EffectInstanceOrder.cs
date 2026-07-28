using System;
using System.Collections.Generic;

namespace Chido.Core.Battle.Effects;

/// <summary>
/// 併存する状態変化インスタンスの発動順（戦闘システム 5.4）。
///
/// <see cref="EffectInstance.InstanceId"/> の昇順であり、<c>SlipDamage</c> の発動順と
/// <c>DisableMove</c> の抽選順の両方がこの1つの順序を共有する。
/// instance_id は意味を持たない列だが決定的であるため、最終タイブレークとして十分に機能する。
///
/// <b><see cref="Guid.CompareTo(Guid)"/> は使えない。</b>
/// .NET の Guid 比較は先頭3フィールドを整数として比較するため、
/// 永続化時のバイト列（<c>GuidToBinaryConverter</c> = <see cref="Guid.ToByteArray()"/>）を
/// そのまま並べる MySQL の <c>BINARY(16)</c> の照合順序と一致しない。
/// 両者がずれると「とどめのインスタンス」の帰属がメモリ上と SQL の <c>ORDER BY</c> で食い違い、
/// 与ダメージ帰属・被攻撃TP・報酬ゲートの決定性が失われる。
/// </summary>
public sealed class EffectInstanceOrder : IComparer<EffectInstance>
{
    public static readonly EffectInstanceOrder Instance = new();

    private EffectInstanceOrder() { }

    public int Compare(EffectInstance? x, EffectInstance? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        return CompareInstanceId(x.InstanceId, y.InstanceId);
    }

    /// <summary>
    /// 格納バイト列を符号なし辞書順で比較する（MySQL の BINARY 比較と同一）。
    /// </summary>
    public static int CompareInstanceId(Guid x, Guid y)
    {
        Span<byte> left = stackalloc byte[16];
        Span<byte> right = stackalloc byte[16];
        x.TryWriteBytes(left);
        y.TryWriteBytes(right);

        return left.SequenceCompareTo(right);
    }
}
