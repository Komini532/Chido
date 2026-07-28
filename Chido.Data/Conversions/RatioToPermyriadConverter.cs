using Chido.Core.Stats;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// Ratio ⇔ permyriad の int。
///
/// 割合が絡む数値はC#側で Ratio に統一して扱う方針（戦闘システム 2.2）に従い、
/// permyriad で格納される列（`_rate` / `_bonus_rate` サフィックス）はすべてこの変換を通す。
/// 列の物理型は用途により SMALLINT UNSIGNED（0〜10000 の非負確率値）と
/// INT（符号ありの補正値）に分かれるが、Ratio 側の内部表現はどちらも int のため変換は共通。
///
/// permyriad ではない割合類（`_shape` は 100 = 1.00、`power` は整数%、`weight` は相対重み）は
/// 意図的に Ratio の対象外であり、この変換を適用してはならない。
/// </summary>
public sealed class RatioToPermyriadConverter()
    : ValueConverter<Ratio, int>(
        v => v.Permyriad,
        v => Ratio.FromPermyriad(v));
