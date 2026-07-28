namespace Chido.Core.Battle.Effects;

/// <summary>
/// 状態変化インスタンスの保持スコープ。
///
/// 書き込み先は付与時点の <c>entity_type × clear_on_battle_end</c> でその場で確定し、
/// 残り有効行動数とは独立である。付与時に決まるため、戦闘終了時に一方から他方へ
/// 行を移し替える処理は発生しない。
/// </summary>
public enum EffectScope
{
    /// <summary>
    /// 戦闘内スコープ（chido_battle_effect）。戦闘終了時に除去される。
    /// clear_on_battle_end=1 の効果（Player/Enemy問わず）と、Enemy の全ての効果がここに入る
    /// （敵は出現の都度使い捨てのインスタンスであり永続化する意味を持たないため）。
    /// </summary>
    Battle,

    /// <summary>
    /// 永続スコープ（chido_player_effect）。Player の clear_on_battle_end=0 の効果のみ。
    /// 戦闘の境界では減衰も消滅もせず、残り有効行動数だけが終わりを保証する。
    /// </summary>
    Player,
}
