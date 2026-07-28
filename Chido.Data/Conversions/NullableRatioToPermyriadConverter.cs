using Chido.Core.Stats;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// Ratio? ⇔ permyriad の int?。NULL に意味を持つ列で使う
/// （chido_effect_status_modifier_master.fixed_rate の NULL は「不定値＝インスタンス側が実値を持つ」を表す）。
/// </summary>
public sealed class NullableRatioToPermyriadConverter()
    : ValueConverter<Ratio?, int?>(
        v => v == null ? null : v.Value.Permyriad,
        v => v == null ? null : Ratio.FromPermyriad(v.Value));
