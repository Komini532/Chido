namespace Chido.Core.Stats;

/// <summary>
/// Shape（ポケモンでいう種族値に相当する、正規化されたステータス倍率）。
/// 基礎ステータス = レベル × Scale(ステータス区分) × Shape(ステータス区分)（戦闘システム 2.3）。
///
/// permyriad ではなく <b>100 = 1.00</b> のスケールで保持する（<see cref="StatCalculator.ShapeScale"/>）。
/// 種族値相当の手動設定値であり、100 を等倍として読み書きするほうが人力でのバランス調整に馴染むため、
/// permyriad 統一の例外として意図的に別スケールを採る（DB設計「割合値のスケールと命名規約」）。
/// この理由により <see cref="Ratio"/> には変換しない。
///
/// Speed と Luck はこの Scale × Shape の枠組みに含まれないため、本型は対応する要素を持たない。
/// </summary>
public readonly record struct StatShape(int MaxLife, int PAtk, int PDef, int MAtk, int MDef)
{
    /// <summary>等倍（すべて 1.00）。プレイヤーは常にこれを用いる（戦闘システム 2.3）。</summary>
    public static readonly StatShape Player = new(
        StatCalculator.ShapeScale, StatCalculator.ShapeScale, StatCalculator.ShapeScale,
        StatCalculator.ShapeScale, StatCalculator.ShapeScale);
}
