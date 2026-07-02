using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// Guid ⇔ byte[16]。使い捨てGuidをBINARY(16)列にそのままのバイト列で保存する（NOT NULL列用）。
/// </summary>
public sealed class GuidToBinaryConverter()
    : ValueConverter<Guid, byte[]>(
        v => v.ToByteArray(),
        v => new Guid(v));
