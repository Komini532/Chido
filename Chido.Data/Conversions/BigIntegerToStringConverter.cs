using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// BigInteger ⇔ 10進整数文字列。VARCHAR(100) 列で使う。
///
/// <para>
/// <b>DECIMAL 列には使えない（検証済み）。</b>
/// 設計ドキュメントは「SQL側での比較・ソートが必要な値は DECIMAL(65,0)」と定めているが、
/// 本コンバータの provider 型は string であり、EF Core 8 + Pomelo 8 には
/// 「CLR string ／ 格納型 DECIMAL」の型マッピングが存在しない。
/// この組み合わせを指定するとモデル確定時（ElementMappingConvention）に
/// NullReferenceException で落ち、マイグレーション生成も実行時のDB接続もできなくなる。
/// string が IEnumerable&lt;char&gt; であるためプリミティブコレクションと誤認される経路に入るのが原因。
/// </para>
/// <para>
/// 実測した組み合わせ:
/// <list type="bullet">
///   <item>BigInteger → string ／ VARCHAR(100) … OK</item>
///   <item>BigInteger → string ／ DECIMAL(65,0) … NullReferenceException</item>
///   <item>string プロパティ ／ DECIMAL(65,0)（コンバータなし） … NullReferenceException</item>
///   <item>BigInteger → decimal ／ DECIMAL(65,0) … モデルは通るが .NET の decimal は
///         28〜29桁までしか持てず65桁の値を保持できないため採用不可</item>
/// </list>
/// </para>
/// <para>
/// このため BigInteger を格納する列はすべて VARCHAR(100) としている。
/// 帰結として、これらの列は SQL 側で数値としてソート・比較できない
/// （VARCHAR の照合順序は辞書順であり "9" &gt; "10" となるため ORDER BY は誤った結果を返す）。
/// 現時点でこれが問題になる機能は存在しないが、ランキングを実装する際は
/// カスタムの型マッピング（IRelationalTypeMappingSourcePlugin で string ⇔ DECIMAL を定義する）で
/// DECIMAL へ戻すか、MySQL 8 の生成列とインデックスで対処する必要がある。
/// </para>
/// </summary>
public sealed class BigIntegerToStringConverter()
    : ValueConverter<BigInteger, string>(
        v => v.ToString(),
        v => BigInteger.Parse(v));
