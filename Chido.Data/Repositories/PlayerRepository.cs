using System.Numerics;
using Chido.Core;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// プレイヤーの永続状態。<c>chido_player</c> はプレイヤーに関するロックアンカーを兼ねる。
/// </summary>
public sealed class PlayerRepository(ChidoDbContext db)
{
    /// <summary>
    /// プレイヤーの初期行一式を用意する。既に存在すれば表示名だけ更新する。
    ///
    /// <b>ロックスコープの外側で呼ぶこと。</b>アンカー行の作成をスコープ内で行うと、
    /// 既存行に対する重複キー検査で共有ロックが乗り、同一行を狙う2トランザクションが
    /// S ロックを持ち合ったまま X への昇格を待ち合うデッドロックになる。
    /// 行は物理削除されないため、一度作られた後に消えることはない。
    ///
    /// 経験値の初期値は <see cref="GameConstants.InitialExp"/>（= 1）。0 だと
    /// <c>level = √exp = 0</c> となり、基礎ステータス = レベル × Scale × Shape により
    /// 全ステータスが0になって成立しない（戦闘システム 2.3）。
    /// </summary>
    public async Task EnsureAsync(
        ulong userId, string? userName = null, CancellationToken cancellationToken = default)
    {
        var player = await db.Players.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (player is null)
        {
            db.Players.Add(new PlayerRecord { UserId = userId, UserName = userName });
        }
        else if (userName is not null && player.UserName != userName)
        {
            player.UserName = userName;
        }

        if (!await db.BattleStatuses.AnyAsync(x => x.UserId == userId, cancellationToken))
        {
            db.BattleStatuses.Add(new BattleStatusRecord
            {
                UserId = userId,
                Exp = GameConstants.InitialExp,
            });
        }

        if (!await db.PlayerCurrencies.AnyAsync(x => x.UserId == userId, cancellationToken))
        {
            db.PlayerCurrencies.Add(new PlayerCurrencyRecord { UserId = userId, Amount = BigInteger.Zero });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>経験値を取得する。<see cref="EnsureAsync"/> 済みであることが前提。</summary>
    public async Task<BigInteger> GetExpAsync(ulong userId, CancellationToken cancellationToken = default)
        => (await db.BattleStatuses.FirstAsync(x => x.UserId == userId, cancellationToken)).Exp;

    /// <summary>経験値を加算する。ロックスコープ内で呼ぶこと。</summary>
    public async Task AddExpAsync(
        ulong userId, BigInteger amount, CancellationToken cancellationToken = default)
    {
        var status = await db.BattleStatuses.FirstAsync(x => x.UserId == userId, cancellationToken);

        status.Exp += amount;
    }

    /// <summary>所持金額を取得する。</summary>
    public async Task<BigInteger> GetCurrencyAsync(ulong userId, CancellationToken cancellationToken = default)
        => (await db.PlayerCurrencies.FirstAsync(x => x.UserId == userId, cancellationToken)).Amount;

    /// <summary>
    /// 所持金額を加減算する。
    ///
    /// <b><c>UPDATE ... SET amount = amount ± X</c> は使えない。</b>金額は10進整数文字列として
    /// 格納されているため、SQL側での算術は成立しない。読み出して <see cref="BigInteger"/> で
    /// 計算し書き戻す。同時更新の直列化は正準ロック順序のアンカー（<c>chido_player.user_id</c>）が担う。
    /// </summary>
    public async Task AddCurrencyAsync(
        ulong userId, BigInteger amount, CancellationToken cancellationToken = default)
    {
        var currency = await db.PlayerCurrencies.FirstAsync(x => x.UserId == userId, cancellationToken);

        currency.Amount += amount;
    }
}
