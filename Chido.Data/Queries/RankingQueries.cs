using Chido.Data.Entities;

namespace Chido.Data.Queries;

/// <summary>
/// 巨大数値カラム（BigInteger）のランキング順序。
///
/// <para>
/// exp / amount は10進整数文字列として VARCHAR に格納されているため、素の <c>ORDER BY exp</c> は
/// 辞書順になり <c>"9" &gt; "10"</c> という誤った結果を返す。
/// 非負の正準10進文字列では <c>(桁数, 辞書順)</c> が数値順と完全に一致するため、
/// 桁数の生成列（exp_len / amount_len）を第1ソートキーに置くことで正しい数値順を得る。
/// </para>
/// <para>
/// <b>これらのカラムを直接 <c>OrderBy(x =&gt; x.Exp)</c> のように並べ替えてはならない。</b>
/// 桁数の項を書き忘れても例外にはならず、静かに誤った順序を返すため、
/// 並び順の知識は本クラスの1箇所に閉じ込めている。並べ替えが必要になったら本クラスにメソッドを足すこと。
/// </para>
/// <para>
/// 不変条件は「値が非負であり、先頭に余分な 0 が付かず、桁数が列幅（100）に収まること」。
/// <see cref="Conversions.BigIntegerToStringConverter"/> が桁数超過を書き込み時に弾き、
/// <c>BigInteger.ToString()</c> が正準表現（余分な先頭 0 なし）を保証する。
/// 負値を入れると順序が壊れるが、対象2カラムはいずれも設計上 UNSIGNED である。
/// </para>
/// </summary>
public static class RankingQueries
{
    /// <summary>経験値の降順（数値順）。ランキング表示用。</summary>
    public static IOrderedQueryable<BattleStatusRecord> OrderByExpDescending(
        this IQueryable<BattleStatusRecord> source)
        => source.OrderByDescending(x => x.ExpLength).ThenByDescending(x => x.Exp);

    /// <summary>経験値の昇順（数値順）。</summary>
    public static IOrderedQueryable<BattleStatusRecord> OrderByExp(
        this IQueryable<BattleStatusRecord> source)
        => source.OrderBy(x => x.ExpLength).ThenBy(x => x.Exp);

    /// <summary>所持金額の降順（数値順）。ランキング表示用。</summary>
    public static IOrderedQueryable<PlayerCurrencyRecord> OrderByAmountDescending(
        this IQueryable<PlayerCurrencyRecord> source)
        => source.OrderByDescending(x => x.AmountLength).ThenByDescending(x => x.Amount);

    /// <summary>所持金額の昇順（数値順）。</summary>
    public static IOrderedQueryable<PlayerCurrencyRecord> OrderByAmount(
        this IQueryable<PlayerCurrencyRecord> source)
        => source.OrderBy(x => x.AmountLength).ThenBy(x => x.Amount);
}
