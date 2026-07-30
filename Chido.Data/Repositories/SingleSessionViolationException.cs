namespace Chido.Data.Repositories;

/// <summary>
/// 単一セッション制約の違反（1プレイヤーが同時に参加できるセッションは1つ）。
///
/// <para>
/// 専用の型にしているのは、呼び出し側がこれを<b>正常系の拒否</b>として扱うため。
/// <see cref="InvalidOperationException"/> のまま捕まえると、マスタの不整合や
/// 実装の不具合まで「別の戦闘に参加しています」という無関係な案内に化けて、
/// 本当の失敗が利用者にもログにも現れなくなる。
/// </para>
/// </summary>
public sealed class SingleSessionViolationException(ulong userId, Guid sessionId)
    : InvalidOperationException(
        $"プレイヤー {userId} は既に別のセッション {sessionId} に参加している。")
{
    public ulong UserId { get; } = userId;

    /// <summary>既に参加しているセッション。</summary>
    public Guid SessionId { get; } = sessionId;
}
