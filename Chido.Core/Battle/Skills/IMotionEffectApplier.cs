namespace Chido.Core.Battle.Skills;

/// <summary>
/// 状態変化の付与・解除モーションの効果適用。
///
/// 付与のライフサイクル（重複判定・残り有効行動数の減衰・スコープの振り分け）は状態変化の実装で
/// 載るため、モーション再生側からは本インターフェイス越しに呼び出す。
///
/// 未提供（null）の場合、付与・解除モーションは<b>効果を起こさないが「再生された」扱いになる</b>。
/// accuracy_gate_group の「効果適用に到達」の意味論としてはこれが正しい
/// （重複拒否や解除の空振りも到達扱いであり、効果が起きたかどうかとは独立した判定であるため）。
/// </summary>
public interface IMotionEffectApplier
{
    /// <summary>
    /// 状態変化を付与する。重複時は拒否（既存インスタンスの残り有効行動数を延長しない）。
    /// 戻り値は表示用のメッセージ。null なら通知しない。
    /// </summary>
    /// <param name="skillKey">
    /// 付与元のスキル。重複判定キーの grant_source_key になる。
    /// affect_reason は本値が「何のキーであるか」を示す型タグであり、本値からは導出できない。
    /// </param>
    string? Grant(
        BattleParticipant granter, BattleParticipant target, GrantEffectMotion motion, string skillKey);

    /// <summary>
    /// 対象が保持する全スコープから effect_key が一致する状態変化をすべて削除する。
    /// 1件も無い場合も空振りとして通知する。
    /// </summary>
    string? Dispel(BattleParticipant target, DispelEffectMotion motion);
}
