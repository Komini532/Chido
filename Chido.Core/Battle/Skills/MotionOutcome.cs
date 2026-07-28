namespace Chido.Core.Battle.Skills;

/// <summary>
/// モーション1件の再生結果。<see cref="Applied"/> が accuracy_gate_group の「効果適用に到達した」に対応する。
/// </summary>
public enum MotionOutcome
{
    /// <summary>効果適用に到達した。</summary>
    Applied,

    /// <summary>同一グループの先頭が効果適用に到達しなかったためスキップされた（ステップ2）。</summary>
    SkippedByGate,

    /// <summary>対象がモーションの要求する状態でなかったためスキップされた（ステップ3）。</summary>
    SkippedByTargetStatus,

    /// <summary>命中判定（accuracy_rate）を外したためスキップされた（ステップ4）。</summary>
    Missed,

    /// <summary>行動者が Active でなくなったため、このモーション以降が打ち切られた（ステップ1）。</summary>
    ShortCircuited,
}

/// <summary>
/// <see cref="MotionOutcome"/> の判定ヘルパ。
/// </summary>
public static class MotionOutcomeExtensions
{
    /// <summary>
    /// accuracy_gate_group のゲートを開くか。
    ///
    /// 「効果適用に到達」は命中成功と同一ではない。対象状態によるスキップや命中失敗では到達せず、
    /// グループ全体がスキップされる（倒した相手・外した攻撃に毒を乗せない）。
    /// 一方、状態変化の重複付与による拒否や解除モーションの空振りは「モーションは実行された」側に
    /// 分類されるため到達扱いになる（戦闘システム 4.2）。
    /// </summary>
    public static bool OpensGate(this MotionOutcome outcome) => outcome == MotionOutcome.Applied;
}
