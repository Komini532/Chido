using System.Globalization;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chido.Data.Conversions;

/// <summary>
/// BigInteger ⇔ 10進整数文字列。VARCHAR(100) 列で使う。
///
/// <para>
/// <b>DECIMAL 列は使えない。EF Core の層ではなくコネクタの層で塞がっている。</b>
/// MySqlConnector は DECIMAL 列を <c>Utf8Parser.TryParse(data, out decimal)</c> で読むため
/// （ColumnReaders/DecimalColumnReader.cs）、<see cref="decimal"/> の上限である 28〜29桁を超える値は
/// 読み出し時に FormatException になる。<c>GetString(ordinal)</c> も生バイトを読み直さず
/// <c>(string)GetValue(ordinal)</c> とキャストするだけ（Core/Row.cs）なので、文字列として逃がすこともできない。
/// SELECT ごとに <c>CAST(col AS CHAR)</c> を書く以外の回避手段がなく、
/// これは MySqlConnector を使う限り Pomelo・Dapper・生の ADO.NET のいずれでも同じである。
/// </para>
/// <para>
/// 書き込み側にも別の壁がある。本コンバータの provider 型は string であり、
/// EF Core 8 + Pomelo 8 には「CLR string ／ 格納型 DECIMAL」の型マッピングが存在しない。
/// この組み合わせを指定するとモデル確定時（ElementMappingConvention）に NullReferenceException で落ち、
/// マイグレーション生成も実行時のDB接続もできなくなる。
/// string が IEnumerable&lt;char&gt; であるためプリミティブコレクションと誤認される経路に入るのが原因。
/// カスタム型マッピング（IRelationalTypeMappingSourcePlugin）でこちらは塞げるが、
/// 上記の読み出し側は塞げないため、DECIMAL への復帰そのものが成立しない。
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
/// そもそも DECIMAL(65,0) は 65桁という上限自体がインフレ型のゲーム性に合わない
/// （ConverterTests は意図的に81桁の往復を固定している）。
/// </para>
/// <para>
/// このため BigInteger を格納する列はすべて VARCHAR(100) としている。
/// SQL側で数値順のソートが必要な列（chido_battle_status.exp、chido_player_currency.amount）は、
/// 桁数を持つストアド生成列との複合インデックスで数値順を得る。
/// 並べ替えは必ず Chido.Data.Queries.RankingQueries 経由で行うこと。
/// </para>
/// </summary>
public sealed class BigIntegerToStringConverter()
    : ValueConverter<BigInteger, string>(
        v => BigIntegerText.ToStorage(v),
        v => BigInteger.Parse(v, CultureInfo.InvariantCulture));

/// <summary>
/// BigInteger と格納用文字列の相互変換。null許容版と共有するためコンバータ本体から切り出している。
/// </summary>
internal static class BigIntegerText
{
    /// <summary>格納先 VARCHAR(100) の幅。符号を含めた文字数の上限。</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// 10進整数文字列へ変換する。列幅を超える値は切り詰めずに例外にする。
    /// MySQL は非STRICTモードだと超過分を静かに切り詰めるため、
    /// 「桁が落ちた巨大数値」がそのまま正しい値として流通してしまう事故を防ぐ。
    /// 桁数の生成列によるランキング順序も、値が列幅に収まっていることを前提にしている。
    /// </summary>
    public static string ToStorage(BigInteger value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Length > MaxLength)
        {
            throw new OverflowException(
                $"BigInteger の格納先は VARCHAR({MaxLength}) だが、値は {text.Length} 文字であり収まらない。");
        }

        return text;
    }
}
