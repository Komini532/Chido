using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// BigInteger? ⇔ 10進整数文字列。NULL に意味を持つ列で使う
/// （chido_skill_master.learnable_level の NULL は「レベルアップでは習得不可」、
/// chido_title_master.condition_value の NULL は「判定値を数値で持たない入手条件」を表す）。
///
/// null許容プロパティに <see cref="BigIntegerToStringConverter"/>（非null用）を当ててはならない。
/// EF Core 8 はその場合に nullable のラッパーを自前で合成しようとして要素型の導出を誤り、
/// モデル確定時（ElementMappingConvention）に NullReferenceException で落ちる。
/// Guid 側で NullableGuidToBinaryConverter を分けているのと同じ理由による。
/// </summary>
public sealed class NullableBigIntegerToStringConverter()
    : ValueConverter<BigInteger?, string?>(
        v => v == null ? null : v.Value.ToString(),
        v => v == null ? null : BigInteger.Parse(v));
