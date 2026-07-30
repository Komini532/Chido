using System;
using System.Collections.Generic;
using Chido.Core.Entities;

namespace Chido.Core.World;

/// <summary>
/// 起動時（またはマスタ投入時）の検証（戦闘システム 10.5）。
///
/// <see cref="GroupDraw"/> の草原フォールバックと <see cref="FieldTransition"/> の草原フォールバックは、
/// いずれも草原の存在に依存する。この依存を実行時の例外だけで守ると<b>発覚がプレイヤーの行動時点まで
/// 遅れる</b>ため、起動時に確かめる。経験値の初期値1（正常系）とクランプ（異常系）で採ったのと同じ、
/// 正常系を起動時検証で・異常系を例外で塞ぐ二重防御である。
///
/// 参照する「草原」のフィールドキーは <see cref="GameConstants.GrasslandFieldKey"/> 1箇所に集約されており、
/// <see cref="GroupDraw"/>・<see cref="FieldTransition"/>・初期フィールドの固定と同じ定数を共有する。
/// </summary>
public static class WorldValidation
{
    /// <summary>
    /// 検証して、満たされていない条件をメッセージとして返す。空なら健全。
    /// 例外にせず列挙するのは、起動時に<b>すべての不足をまとめて</b>提示するため
    /// （1つ直すたびに再起動して次の不足が出る、という運用を避ける）。
    /// </summary>
    public static IReadOnlyList<string> Validate(IFieldCatalog catalog)
    {
        var problems = new List<string>();

        if (!catalog.HasField(GameConstants.GrasslandFieldKey))
        {
            problems.Add(
                $"フィールドマスタに草原（{GameConstants.GrasslandFieldKey}）が存在しない。" +
                "組の抽選とフィールド遷移の縮退先であるため、これが無いと両方の安全網が機能しない。");
        }

        if (catalog.GroupsOf(GameConstants.GrasslandFieldKey, Rarity.Common).Count == 0)
        {
            problems.Add(
                $"草原（{GameConstants.GrasslandFieldKey}）に紐づく {Rarity.Common} の組が1件も存在しない。" +
                "組の抽選が候補0件になった際のフォールバック先であるため、最低1件必要。");
        }

        return problems;
    }

    /// <summary>
    /// 検証し、満たされていなければ例外にする。Bot起動時に呼ぶ。
    /// </summary>
    public static void ThrowIfInvalid(IFieldCatalog catalog)
    {
        var problems = Validate(catalog);

        if (problems.Count == 0) return;

        throw new InvalidOperationException(
            "マスタデータの起動時検証に失敗した:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }
}
