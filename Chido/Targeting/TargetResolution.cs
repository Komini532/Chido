using Chido.Core.Battle;

namespace Chido.Targeting;

/// <summary>
/// コマンドの <c>[対象]</c> を参加者へ解決する（戦闘システム 9.2・B-12）。
///
/// <para>
/// <c>[対象]</c> は<b>オートコンプリート付きの任意入力文字列</b>である。固定の選択式にしないのは、
/// 同時に複数体出現しうる敵の中から柔軟に指定できるようにするため。
/// </para>
/// <para>
/// <b>オートコンプリートの <c>value</c> には <c>entity_id</c> を載せる</b>（表示名は <c>label</c> 側）。
/// 候補から選んだ場合はGuidがそのまま届くため、同名の敵が組にいても一意に決まる。
/// 自由入力された場合のみ表示名で照合する。
/// </para>
/// <para>
/// 引数は1つだけだが、解決された参加者の <c>entity_type</c> によって役割が決まる。
/// 敵なら <c>CurrentTarget</c> の更新、味方（自分を含む）なら <c>target_rule = 味方</c> の
/// モーションの対象になる。したがって敵対象と味方対象の両方を持つスキルでも引数は増えない。
/// </para>
/// </summary>
public static class TargetResolution
{
    /// <summary>
    /// 入力を参加者へ解決する。
    ///
    /// 解決順は <b>entity_id 直指定 → 表示名の完全一致 → 前方一致</b>。
    /// <b>複数該当・該当なしはいずれも解決不能とする</b>（曖昧なまま片方を選ぶと、
    /// プレイヤーの意図と違う相手を攻撃したことに後から気づく形になるため）。
    /// 解決不能のとき <c>CurrentTarget</c> は書き換えない。
    /// </summary>
    /// <param name="input">コマンド引数。空・null は「指定なし」。</param>
    /// <param name="candidates">解決対象。Active な参加者に限って渡すこと。</param>
    public static TargetResolutionResult Resolve(
        string? input, IReadOnlyList<BattleParticipant> candidates)
    {
        if (string.IsNullOrWhiteSpace(input)) return TargetResolutionResult.NotSpecified();

        var trimmed = input.Trim();

        // オートコンプリートから選ばれた場合はGuidがそのまま届く
        if (Guid.TryParse(trimmed, out var entityId))
        {
            var byId = candidates.FirstOrDefault(p => p.Entity.Id == entityId);

            return byId is null
                ? TargetResolutionResult.Unresolved(trimmed)
                : TargetResolutionResult.Resolved(byId);
        }

        var exact = candidates
            .Where(p => string.Equals(p.Entity.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1) return TargetResolutionResult.Resolved(exact[0]);
        if (exact.Count > 1) return TargetResolutionResult.Ambiguous(trimmed);

        var prefix = candidates
            .Where(p => p.Entity.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (prefix.Count == 1) return TargetResolutionResult.Resolved(prefix[0]);
        if (prefix.Count > 1) return TargetResolutionResult.Ambiguous(trimmed);

        return TargetResolutionResult.Unresolved(trimmed);
    }

    /// <summary>
    /// オートコンプリートの候補ラベル。
    ///
    /// <b>同名の敵は表示順を添えて区別する。</b>組に同じ種族が複数いるのは通常の構成であり、
    /// 名前だけでは選択肢が見分けられない。表示順は <c>member_index</c> の恒等複製であり、
    /// 埋め込み上の並び順とも一致するため、プレイヤーから見て対応が取りやすい。
    /// </summary>
    public static string LabelOf(
        BattleParticipant participant, IReadOnlyList<BattleParticipant> candidates)
    {
        var sameName = candidates.Count(
            p => string.Equals(p.Entity.Name, participant.Entity.Name, StringComparison.Ordinal));

        return sameName <= 1
            ? participant.Entity.Name
            : $"{participant.Entity.Name} #{participant.DisplayOrder + 1}";
    }
}

/// <summary>解決の結果。</summary>
public readonly record struct TargetResolutionResult(
    TargetResolutionStatus Status, BattleParticipant? Participant, string? Input)
{
    public static TargetResolutionResult NotSpecified()
        => new(TargetResolutionStatus.NotSpecified, null, null);

    public static TargetResolutionResult Resolved(BattleParticipant participant)
        => new(TargetResolutionStatus.Resolved, participant, null);

    public static TargetResolutionResult Ambiguous(string input)
        => new(TargetResolutionStatus.Ambiguous, null, input);

    public static TargetResolutionResult Unresolved(string input)
        => new(TargetResolutionStatus.Unresolved, null, input);

    /// <summary>解決できなかった場合の通知文。解決できた・指定なしの場合は null。</summary>
    public string? Message => Status switch
    {
        TargetResolutionStatus.Ambiguous =>
            $"「{Input}」に一致する対象が複数あります。候補から選ぶか、より正確に指定してください。",

        TargetResolutionStatus.Unresolved =>
            $"「{Input}」に一致する対象が見つかりませんでした。",

        _ => null,
    };
}

public enum TargetResolutionStatus
{
    /// <summary>指定なし。対象解決は既定（敵なら CurrentTarget、味方なら行動者自身）へ委ねる。</summary>
    NotSpecified,

    Resolved,

    /// <summary>複数該当。片方を選ばず解決不能として扱う。</summary>
    Ambiguous,

    Unresolved,
}
