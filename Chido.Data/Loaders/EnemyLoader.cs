using System.Numerics;
using Chido.Core.Entities.Enemies;
using Chido.Core.Stats;
using Chido.Data.Entities;
using Chido.Data.World;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Loaders;

/// <summary>
/// 出現インスタンス（<c>chido_battle_enemy</c>）から <see cref="Enemy"/> を復元する。
///
/// <para>
/// 復元するのは<b>出現時に確定した内容</b>だけ。種族キーとレベルは行が持ち、装備は
/// <c>chido_battle_enemy_equipment</c> に記録されている。敵の装備は出現時に確定して
/// セッション中に変化しないため（戦闘システム 2.5）、記録を読み直せば必ず同じ内容になる。
/// </para>
/// <para>
/// <b>現在HPと状態変化はここでは載せない。</b>どちらもセッションに属する値であり、
/// 参加者行と <c>chido_battle_effect</c> が真値を持つ。組の出現直後で参加者行がまだ無い
/// 期間もあるため、それらの適用は呼び出し側が文脈に応じて行う。
/// </para>
/// </summary>
public sealed class EnemyLoader(ChidoDbContext db, DatabaseWorldCatalog world)
{
    /// <summary>
    /// 出現インスタンス群を復元する。
    /// </summary>
    /// <param name="entityIds">
    /// 参加者行の <c>entity_id</c>（敵の出現インスタンスIDとは別物）。
    /// 与えると実体の識別子をそれに揃える。セッションに属さない敵では省略する。
    /// </param>
    public async Task<IReadOnlyList<Enemy>> LoadAsync(
        IReadOnlyList<Guid> enemyIds,
        IReadOnlyDictionary<Guid, Guid>? entityIds = null,
        CancellationToken cancellationToken = default)
    {
        if (enemyIds.Count == 0) return [];

        var records = (await db.BattleEnemies
                .Where(x => enemyIds.Contains(x.EnemyId))
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.EnemyId);

        var equipment = await LoadEquipmentAsync(enemyIds, cancellationToken);

        var result = new List<Enemy>();

        // 呼び出し側が渡した順序を保つ。敵の表示順は spawn_index / display_order で決まっており、
        // ここで並べ替えるとその根拠が2箇所に分かれる
        foreach (var enemyId in enemyIds)
        {
            if (!records.TryGetValue(enemyId, out var record)) continue;

            var enemy = world.CreateEnemy(
                record.MasterKey, record.Level, entityIds?.GetValueOrDefault(enemyId));

            enemy.SetEquipment(equipment.GetValueOrDefault(enemyId) ?? []);

            result.Add(enemy);
        }

        return result;
    }

    /// <summary>
    /// 出現時に装着した装備を <see cref="EquipmentBonus"/> へ変換する。
    /// 部位は記録されているが補正の合成には関与しない（レイヤー内は加算合成であり、
    /// どの部位に入っているかは結果を変えない）ため、スロット表は読まなくてよい。
    /// </summary>
    private async Task<Dictionary<Guid, List<EquipmentBonus>>> LoadEquipmentAsync(
        IReadOnlyList<Guid> enemyIds, CancellationToken cancellationToken)
    {
        var worn = await db.BattleEnemyEquipments
            .Where(x => enemyIds.Contains(x.EnemyId))
            .ToListAsync(cancellationToken);

        if (worn.Count == 0) return [];

        var equipKeys = worn.Select(x => x.EquipKey).Distinct().ToList();

        var masters = await db.EquipmentMasters
            .Where(x => equipKeys.Contains(x.EquipKey))
            .ToDictionaryAsync(x => x.EquipKey, cancellationToken);

        return worn
            .Where(x => masters.ContainsKey(x.EquipKey))
            .GroupBy(x => x.EnemyId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => ToBonus(masters[x.EquipKey])).ToList());
    }

    /// <summary>
    /// 出現時MaxLifeの合計（報酬の分母に使う）。
    ///
    /// <b>状態変化補正を含まない「出現時の」値である</b>ため、レベル・敵マスタ・装備から
    /// 決定的に再現できる。撃破後に敵の状態変化が消えていても同じ値が得られる。
    /// </summary>
    public async Task<BigInteger> SpawnMaxLifeSumAsync(
        IReadOnlyList<Guid> enemyIds, CancellationToken cancellationToken = default)
    {
        var enemies = await LoadAsync(enemyIds, cancellationToken: cancellationToken);

        return enemies.Aggregate(BigInteger.Zero, (acc, e) => acc + e.MaxLife);
    }

    private static EquipmentBonus ToBonus(EquipmentMasterRecord master)
        => new(
            master.ProgressionValue,
            master.Rarity,
            master.HpRate,
            master.PAtkRate,
            master.PDefRate,
            master.MAtkRate,
            master.MDefRate,
            master.SpeedBonus,
            master.LuckBonusRate,
            master.Elements);
}
