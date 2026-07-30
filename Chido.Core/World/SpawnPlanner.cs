using System;
using System.Numerics;
using Chido.Core.Battle;
using Chido.Core.Entities;

namespace Chido.Core.World;

/// <summary>
/// 次の敵をどう決めるかの分岐（戦闘システム 10.3）。
///
/// | 終了理由 | 累積敵レベル | フィールド切替判定 | 組の選択 |
/// |---|---|---|---|
/// | PlayerVictory | <b>+1（先に行う）</b> | 行う（mod 2500） | 切替<b>後</b>フィールドで DrawGroup（通常抽選） |
/// | PlayerEscaped（前組が Common/Uncommon） | 変化なし | 発火しえない | <b>前組と同一の group_key</b>（DrawGroup を経由しない） |
/// | PlayerEscaped（前組が Rare/Mythic/Hidden） | 変化なし | 発火しえない | DrawGroup(現在フィールド, forced_rarity = Common) |
/// | EnemyEscaped | 変化なし | 発火しえない | DrawGroup(現在フィールド, forced_rarity = Common) |
/// | ChannelMissing | — | — | チャンネルの永続状態ごと削除（本型は呼ばれない） |
///
/// <b>フィールド切替は PlayerVictory でのみ起こる。</b>切替判定は
/// 「累積敵レベル mod 2500 == 0」であり、累積敵レベルが変化するのは PlayerVictory だけであるため、
/// 他の終了理由では判定が発火する余地がない。専用カウンターを持たない設計から自動的に導かれる。
///
/// <b>なぜ敵の逃走は常に Common なのか</b>: プレイヤーの逃走は「レア敵から降りた」ことへの
/// ペナルティだが、敵の逃走はプレイヤーの選択の結果ではない。レアリティ分岐を適用すると
/// Common の敵に逃げられた場合に同じ敵が戻ってきて、逃走した敵と再戦するという不自然な状況になる。
/// </summary>
public static class SpawnPlanner
{
    /// <summary>
    /// セッション終了後の次の出現を計画する。副作用は持たず、書き込みは呼び出し側が行う。
    /// </summary>
    /// <param name="previousGroupKey">直前に出現していた組。</param>
    /// <param name="previousRarity">直前の組のレアリティ。<c>PlayerEscaped</c> の分岐に使う。</param>
    public static SpawnPlan PlanNext(
        BattleEndReason reason,
        string currentFieldKey,
        BigInteger cumulativeEnemyLevel,
        string previousGroupKey,
        Rarity previousRarity,
        IFieldCatalog catalog,
        Random rng)
    {
        switch (reason)
        {
            case BattleEndReason.PlayerVictory:
            {
                // 順序が意味を持つ。+1 → 切替判定 → 抽選 の順でなければ、
                // 切替の境目のターンで「切替前のフィールドから抽選する」ことになる
                var level = cumulativeEnemyLevel + BigInteger.One;

                var field = currentFieldKey;
                var fieldDegraded = false;
                var fieldChanged = false;

                if (IsFieldTransitionDue(level))
                {
                    var transition = FieldTransition.Next(catalog, currentFieldKey, rng);
                    field = transition.FieldKey;
                    fieldDegraded = transition.Degraded;
                    fieldChanged = true;
                }

                var draw = GroupDraw.Draw(catalog, field, rng);

                return new SpawnPlan(
                    level, field, draw.GroupKey, draw.Rarity,
                    FieldChanged: fieldChanged,
                    GroupDegraded: draw.Degraded,
                    FieldDegraded: fieldDegraded);
            }

            case BattleEndReason.PlayerEscaped when IsLowRarity(previousRarity):
            {
                // 前組と同一の group_key を直接参照する。DrawGroup を経由しないため、
                // フィールドへの紐づけの有無に依存せず、草原フォールバックの対象外になる。
                // 「同一の組」でもインスタンスは再生成される（HP全快・装備再抽選・auto効果再適用）
                return Unchanged(currentFieldKey, cumulativeEnemyLevel, previousGroupKey, previousRarity);
            }

            case BattleEndReason.PlayerEscaped:
            case BattleEndReason.EnemyEscaped:
            {
                // レア以上から降りた場合は Common へ落とす（Uncommon は対象外）。
                // 敵の逃走にはレアリティ分岐が無く、常に Common
                var draw = GroupDraw.Draw(catalog, currentFieldKey, rng, forcedRarity: Rarity.Common);

                return new SpawnPlan(
                    cumulativeEnemyLevel, currentFieldKey, draw.GroupKey, draw.Rarity,
                    FieldChanged: false,
                    GroupDegraded: draw.Degraded,
                    FieldDegraded: false);
            }

            case BattleEndReason.ChannelMissing:
                throw new InvalidOperationException(
                    "チャンネル消失時は次の敵を出さず、チャンネルの永続状態ごと削除する（戦闘システム 6.3）。");

            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "未知の終了理由。");
        }
    }

    /// <summary>
    /// 戦闘チャンネルの初期化時の出現（戦闘システム 10.5）。
    ///
    /// 組の抽選と生成は <c>PlayerVictory</c> 時と<b>同一のロジック</b>だが、
    /// <b>レベル加算とフィールド切替判定は行わない</b>（累積敵レベルは1で固定、フィールドは草原固定）。
    /// </summary>
    public static SpawnPlan PlanInitial(IFieldCatalog catalog, Random rng)
    {
        var draw = GroupDraw.Draw(catalog, GameConstants.GrasslandFieldKey, rng);

        return new SpawnPlan(
            GameConstants.InitialCumulativeEnemyLevel,
            GameConstants.GrasslandFieldKey,
            draw.GroupKey,
            draw.Rarity,
            FieldChanged: false,
            GroupDegraded: draw.Degraded,
            FieldDegraded: false);
    }

    /// <summary>
    /// フィールド切替の契機か。累積敵レベルが切替周期の倍数に達したとき。
    /// 切替専用のカウンターは持たず、累積敵レベルそのものを見る。
    /// </summary>
    public static bool IsFieldTransitionDue(BigInteger cumulativeEnemyLevel)
        => cumulativeEnemyLevel % GameConstants.FieldTransitionPeriod == BigInteger.Zero;

    /// <summary>
    /// 逃走時に同一の組が再出現するレアリティ帯（Common / Uncommon）。
    /// Rare 以上から降りた場合はペナルティとして Common へ落ちる。
    /// </summary>
    private static bool IsLowRarity(Rarity rarity)
        => rarity is Rarity.Common or Rarity.Uncommon;

    private static SpawnPlan Unchanged(
        string fieldKey, BigInteger level, string groupKey, Rarity rarity)
        => new(level, fieldKey, groupKey, rarity,
            FieldChanged: false, GroupDegraded: false, FieldDegraded: false);
}

/// <summary>
/// 次の出現の計画。書き込みは呼び出し側がチャンネル行ロック下で行う。
/// </summary>
/// <param name="CumulativeEnemyLevel">出現する敵のレベル。組の全メンバー共通。</param>
/// <param name="FieldKey">切替後のフィールド。切替が起きなければ現在フィールドのまま。</param>
/// <param name="Rarity">確定した組のレアリティ。報酬の根拠になる。</param>
/// <param name="FieldChanged">フィールド切替が起きたか。</param>
/// <param name="GroupDegraded">組の抽選が草原の Common へ縮退したか。真なら通知する。</param>
/// <param name="FieldDegraded">遷移先候補が0件で草原へ縮退したか。真なら通知する。</param>
public readonly record struct SpawnPlan(
    BigInteger CumulativeEnemyLevel,
    string FieldKey,
    string GroupKey,
    Rarity Rarity,
    bool FieldChanged,
    bool GroupDegraded,
    bool FieldDegraded);
