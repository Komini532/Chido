using System;

namespace Chido.Core.World;

/// <summary>
/// フィールドの遷移（戦闘システム 10.4）。
///
/// <code>
/// NextField(current_field):
///     候補 = chido_field_transition_master の current_field からの遷移先
///     if 候補が空:
///         縮退を通知・記録する
///         return 草原
///     return 候補から完全ランダムに1つ
/// </code>
///
/// <b>行き止まりのフィールドでは草原へ落とす。</b>現在フィールドに留まる案は採らない。
/// 次の判定機会も2500レベル後で同じく候補0件になるため、フィールドシステムが
/// 恒久的に、しかも無言で停止してしまう。草原へ落とせば通常の遷移が再開され自己回復する。
///
/// <b>意図的な行き止まりは自己ループで表現する。</b><c>(草原, 草原)</c> のような自己参照行を置けば
/// 「そこから動かない」がデータ上の意図として明示され、縮退経路（行が無い＝不整合）と区別できる。
/// </summary>
public static class FieldTransition
{
    public static FieldTransitionResult Next(IFieldCatalog catalog, string currentFieldKey, Random rng)
    {
        var candidates = catalog.TransitionsFrom(currentFieldKey);

        if (candidates.Count == 0)
        {
            // 現在フィールドが草原でかつ候補0件なら、フォールバック先が現在地と一致するだけであり
            // 特別扱いは要らない
            return new FieldTransitionResult(GameConstants.GrasslandFieldKey, Degraded: true);
        }

        return new FieldTransitionResult(candidates[rng.Next(candidates.Count)], Degraded: false);
    }
}

/// <param name="Degraded">
/// 遷移先候補が0件で草原へ落ちたか。真なら縮退を通知・記録する
/// （組の抽選の縮退と同一の通知規則）。
/// </param>
public readonly record struct FieldTransitionResult(string FieldKey, bool Degraded);
