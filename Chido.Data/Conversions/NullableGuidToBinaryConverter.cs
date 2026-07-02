using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// Guid? ⇔ byte[]?。BINARY(16)のNULL許容列（enemy_id, target_id等）用。
/// </summary>
public sealed class NullableGuidToBinaryConverter()
    : ValueConverter<Guid?, byte[]?>(
        v => v.HasValue ? v.Value.ToByteArray() : null,
        v => v != null ? new Guid(v) : null);
