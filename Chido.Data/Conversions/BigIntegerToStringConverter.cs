using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// BigInteger ⇔ 10進整数文字列。VARCHAR(100) と DECIMAL(65,0) の両方の列で使う。
///
/// 注意: DECIMAL(65,0)側は「10進整数の文字列パラメータをDECIMAL列に代入する」という
/// MySQLの暗黙変換に依存している。.NETの decimal 型は最大でも28～29桁程度しか
/// 精度を持たず65桁のDECIMALを表現できないため、BigIntegerを文字列経由でやり取りする
/// 本方式を採用している。読み書きの往復（特にSELECT側でのGetString/GetFieldValue<string>の
/// 挙動）は、実際にPomeloパッケージを復元できる環境（Windows開発機）で必ず確認すること。
/// </summary>
public sealed class BigIntegerToStringConverter()
    : ValueConverter<BigInteger, string>(
        v => v.ToString(),
        v => BigInteger.Parse(v));
