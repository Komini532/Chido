using Chido.Data.Conversions;

namespace Chido.Data.Configurations;

/// <summary>
/// 値コンバータの共有インスタンス。
/// 同じ変換が49テーブルにわたって繰り返し現れるため、構成ごとに new せず1箇所に集約する。
/// 型名（Guid / Ratio / BigInteger）と衝突しないよう、格納先の表現を名前に採っている。
/// </summary>
internal static class Converters
{
    /// <summary>
    /// BigInteger ⇔ 10進整数文字列。格納先は必ず VARCHAR(100)。
    /// DECIMAL 列が使えない理由（EF Core ではなく MySqlConnector 側の制約）は BigIntegerToStringConverter を参照。
    /// SQL側で数値順のソートが必要な列は Chido.Data.Queries.RankingQueries を経由すること。
    /// </summary>
    public static readonly BigIntegerToStringConverter Numeric = new();

    /// <summary>BigInteger? ⇔ 10進整数文字列（NULL許容列用）。非null用を流用してはならない。</summary>
    public static readonly NullableBigIntegerToStringConverter NullableNumeric = new();

    /// <summary>Guid ⇔ byte[16]（NOT NULL列用）。</summary>
    public static readonly GuidToBinaryConverter Binary = new();

    /// <summary>Guid? ⇔ byte[16]（NULL許容列用）。</summary>
    public static readonly NullableGuidToBinaryConverter NullableBinary = new();

    /// <summary>Ratio ⇔ permyriad の int。</summary>
    public static readonly RatioToPermyriadConverter Permyriad = new();

    /// <summary>Ratio? ⇔ permyriad の int?。</summary>
    public static readonly NullableRatioToPermyriadConverter NullablePermyriad = new();
}
