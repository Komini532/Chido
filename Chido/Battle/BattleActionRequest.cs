using Chido.Core.Battle;
using Chido.Rendering;

namespace Chido.Battle;

/// <summary>プレイヤーが起こした戦闘行為1回分。</summary>
public enum BattleActionKind
{
    /// <summary>通常攻撃。</summary>
    Attack,

    /// <summary>習得済みスキルの発動。</summary>
    Skill,

    /// <summary>防御。自分自身への DRR 付与モーション1つで構成される。</summary>
    Defend,

    /// <summary>アイテムの使用。効果は「特定スキルの発動」に収束する。</summary>
    Use,

    /// <summary>離脱。ターンを消費しない別経路。</summary>
    Escape,

    /// <summary>次の行動の宛先の指定。ターンも反撃も発生しない（B-1）。</summary>
    Target,
}

/// <summary>
/// 1回のコマンド実行の入力。
/// </summary>
/// <param name="TargetInput">
/// <c>[対象]</c> の生入力。オートコンプリートから選ばれた場合は <c>entity_id</c> の文字列、
/// 自由入力の場合は表示名（戦闘システム 9.2・B-12）。
/// </param>
public sealed record BattleActionRequest(
    BattleActionKind Kind,
    ulong GuildId,
    ulong ChannelId,
    ulong UserId,
    string UserName,
    string? SkillKey = null,
    string? ItemKey = null,
    string? TargetInput = null);

/// <summary>
/// 1回のコマンド実行の結果。
/// </summary>
/// <param name="Accepted">
/// 行動が成立したか。偽の場合はターン・TP・反撃・状態変化の減衰のいずれも発生しておらず、
/// <c>CurrentTarget</c> も書き換わっていない（戦闘システム 4.2）。
/// </param>
/// <param name="Message">
/// 描画済みのメッセージ。不成立の場合も理由を <c>Trailing</c> に載せて返す。
/// </param>
/// <param name="EndReason">セッションが終了した場合の理由。</param>
public sealed record BattleActionOutcome(
    bool Accepted,
    RenderedBattleMessage Message,
    BattleEndReason? EndReason = null);
