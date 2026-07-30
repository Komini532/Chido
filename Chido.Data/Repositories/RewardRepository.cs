using System.Numerics;
using Chido.Core.Rewards;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Repositories;

/// <summary>
/// 撃破報酬の適用と、その入力となるマスタの読み出し（戦闘システム 6.2・10.2）。
///
/// <b>チャンネル行②のロック下で、撃破を含む処理と同一トランザクションで呼ぶこと。</b>
/// 報酬の適用が別トランザクションに分かれると、途中で失敗したときに
/// 「敵は倒れたが報酬が入っていない」状態が残る。
/// </summary>
public sealed class RewardRepository(ChidoDbContext db)
{
    /// <summary>
    /// 撃破した組から報酬の入力を組み立てる。
    ///
    /// アイテム・通貨のマスタは撃破した敵の<b>種族キー</b>で引き、装備のドロップ候補は
    /// <b>出現時に確定した装備</b>（<c>chido_battle_enemy_equipment</c>）で引く。
    /// 装備マスタの全候補ではないため、出現時に身につけていなかった装備は落ちない。
    /// </summary>
    /// <param name="players">参加者の貢献。呼び出し側が台帳から組み立てる。</param>
    /// <param name="enemyLevel">組のレベル。全メンバー共通。</param>
    /// <param name="spawnMaxLifeSum">組の全メンバーの出現時MaxLifeの合計。</param>
    /// <param name="defeatedEnemyIds">撃破した敵の出現インスタンスID。</param>
    public async Task<RewardContext> BuildContextAsync(
        IReadOnlyList<PlayerContribution> players,
        BigInteger enemyLevel,
        BigInteger spawnMaxLifeSum,
        IReadOnlyList<Guid> defeatedEnemyIds,
        CancellationToken cancellationToken = default)
    {
        var enemies = await db.BattleEnemies
            .Where(x => defeatedEnemyIds.Contains(x.EnemyId))
            .ToListAsync(cancellationToken);

        var enemyKeys = enemies.Select(x => x.MasterKey).Distinct().ToList();

        // 組の基礎経験値はメンバーの合算。chido_enemy_group_master は exp_rate を持たず、
        // exp_rate は個体側にしかない。全メンバーはレベルが共通であるため合算で足りる。
        // 同一種族が複数体いる場合はその数だけ加算する必要があるため、Distinct は使えない
        var expRates = await db.EnemyMasters
            .Where(x => enemyKeys.Contains(x.EnemyKey))
            .ToDictionaryAsync(x => x.EnemyKey, x => x.ExpRate, cancellationToken);

        var expRateSum = enemies.Aggregate(
            BigInteger.Zero,
            (acc, e) => acc + (expRates.TryGetValue(e.MasterKey, out var rate) ? rate.Permyriad : 0));

        // 通貨は固定値（抽選なし）。撃破した敵ごとに合算する
        var currencyRates = await db.EnemyCurrencyMasters
            .Where(x => enemyKeys.Contains(x.EnemyKey))
            .ToDictionaryAsync(x => x.EnemyKey, x => x.DropAmount, cancellationToken);

        var currencyTotal = enemies.Aggregate(
            BigInteger.Zero,
            (acc, e) => acc + (currencyRates.TryGetValue(e.MasterKey, out var amount) ? amount : 0));

        var loots = (await db.EnemyLootsMasters
                .Where(x => enemyKeys.Contains(x.EnemyKey))
                .ToListAsync(cancellationToken))
            // 同一種族が複数体いれば、その数だけドロップ判定の機会がある
            .SelectMany(loot => enemies
                .Where(e => e.MasterKey == loot.EnemyKey)
                .Select(_ => new LootOption(loot.ItemKey, loot.Quantity, loot.DropRate)))
            .ToList();

        var equipmentDrops = await LoadEquipmentDropsAsync(defeatedEnemyIds, cancellationToken);

        return new RewardContext(
            players, enemyLevel, expRateSum, spawnMaxLifeSum, currencyTotal, loots, equipmentDrops);
    }

    /// <summary>
    /// 出現時に確定した装備のドロップ候補。ドロップ率は敵マスタ側
    /// （<c>chido_enemy_equipment_master.drop_rate</c>）が持つ。
    /// </summary>
    private async Task<List<EquipmentDropOption>> LoadEquipmentDropsAsync(
        IReadOnlyList<Guid> defeatedEnemyIds, CancellationToken cancellationToken)
    {
        var worn = await db.BattleEnemyEquipments
            .Where(x => defeatedEnemyIds.Contains(x.EnemyId))
            .ToListAsync(cancellationToken);

        if (worn.Count == 0) return [];

        var enemyKeys = await db.BattleEnemies
            .Where(x => defeatedEnemyIds.Contains(x.EnemyId))
            .ToDictionaryAsync(x => x.EnemyId, x => x.MasterKey, cancellationToken);

        var rates = (await db.EnemyEquipmentMasters
                .Where(x => enemyKeys.Values.Contains(x.EnemyKey))
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.EnemyKey, x.EquipKey), x => x.DropRate);

        return worn
            .Where(w => enemyKeys.ContainsKey(w.EnemyId))
            .Select(w => (w.EquipKey, Rate: rates.GetValueOrDefault((enemyKeys[w.EnemyId], w.EquipKey))))
            .Select(x => new EquipmentDropOption(x.EquipKey, x.Rate))
            .ToList();
    }

    /// <summary>
    /// 報酬を適用する。
    ///
    /// 装備は<b>内容を複製した新しいインスタンスとして発行する</b>。敵の装備インスタンスを
    /// そのまま所有者移転すると、同一の敵から複数プレイヤーが同じ装備を受け取る場合に破綻する。
    /// </summary>
    public async Task ApplyAsync(
        IReadOnlyList<PlayerReward> rewards, CancellationToken cancellationToken = default)
    {
        var players = new PlayerRepository(db);

        foreach (var reward in rewards)
        {
            await players.AddExpAsync(reward.UserId, reward.Exp, cancellationToken);
            await players.AddCurrencyAsync(reward.UserId, reward.Currency, cancellationToken);

            foreach (var item in reward.Items)
            {
                await AddItemAsync(reward.UserId, item, cancellationToken);
            }

            foreach (var equipKey in reward.Equipment)
            {
                db.PlayerEquipments.Add(new PlayerEquipmentRecord
                {
                    InstanceId = Guid.NewGuid(),
                    UserId = reward.UserId,
                    EquipKey = equipKey,
                });
            }
        }
    }

    /// <summary>所持数を加算する。同じアイテムを既に持っていれば数量だけ増やす。</summary>
    private async Task AddItemAsync(ulong userId, ItemDrop drop, CancellationToken cancellationToken)
    {
        var existing = await db.PlayerItems
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ItemKey == drop.ItemKey, cancellationToken);

        if (existing is null)
        {
            db.PlayerItems.Add(new PlayerItemRecord
            {
                UserId = userId,
                ItemKey = drop.ItemKey,
                Quantity = drop.Quantity,
            });
            return;
        }

        existing.Quantity += drop.Quantity;
    }

    /// <summary>
    /// 称号の判定と付与。<b>報酬を適用した後</b>の状態に対して行う
    /// （撃破で得た経験値・通貨・アイテムがそのターンの条件を満たしうるため）。
    /// </summary>
    /// <param name="defeatedEnemyKeys">その戦闘で撃破した敵の種族キー。</param>
    /// <returns>新たに獲得した称号キー。</returns>
    public async Task<IReadOnlyList<string>> GrantTitlesAsync(
        ulong userId,
        BigInteger level,
        IReadOnlySet<string> defeatedEnemyKeys,
        CancellationToken cancellationToken = default)
    {
        var titles = (await db.TitleMasters.ToListAsync(cancellationToken))
            .Select(x => new TitleCondition(x.TitleKey, x.AcquisitionType, x.ConditionKey, x.ConditionValue))
            .ToList();

        if (titles.Count == 0) return [];

        var owned = (await db.PlayerTitles
                .Where(x => x.UserId == userId)
                .Select(x => x.TitleKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var ownedItems = (await db.PlayerItems
                .Where(x => x.UserId == userId && x.Quantity > 0)
                .Select(x => x.ItemKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var currency = await new PlayerRepository(db).GetCurrencyAsync(userId, cancellationToken);

        var earned = TitleEvaluator.Evaluate(
            titles, owned, new TitleProgress(ownedItems, defeatedEnemyKeys, level, currency));

        foreach (var titleKey in earned)
        {
            db.PlayerTitles.Add(new PlayerTitleRecord { UserId = userId, TitleKey = titleKey });
        }

        return earned;
    }
}
