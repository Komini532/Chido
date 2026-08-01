using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Seeding;

/// <summary>
/// 初期マスタデータの投入（戦闘システム 10.5）。
///
/// <para>
/// <b>既に存在するキーには一切触れない。</b>「無ければ入れる」だけであり、更新も削除も行わない。
/// マスタはバランス調整で運用側が直接手を入れる対象であるため、投入のたびに定義側の値で
/// 上書きすると、調整した内容が次の起動で黙って巻き戻る。追加分だけが入る挙動にしておけば、
/// 何度実行しても安全であり、新しいコンテンツを足す手段としてもそのまま使える。
/// </para>
/// <para>
/// 依存の順序で入れる（フィールド → 組 → 敵 → スキル → 効果 → 装備 → アイテム）。
/// 明示的な外部キーはスキルモーションのサブタイプ周辺にしか無いが、
/// 参照先が先に入っている状態を保つことで、途中で失敗しても残る状態が読み解ける。
/// </para>
/// </summary>
public static class MasterDataSeeder
{
    /// <summary>
    /// 不足しているマスタ行を投入する。
    /// </summary>
    /// <returns>実際に投入した行数。0 なら既に揃っている。</returns>
    public static async Task<int> SeedAsync(
        ChidoDbContext db, CancellationToken cancellationToken = default)
    {
        var added = 0;

        // フィールドと組
        added += await AddMissingAsync(db, MasterData.Fields, x => x.FieldKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.RarityRates, x => new { x.FieldKey, x.Rarity }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.Transitions, x => new { x.FieldKey, x.NextFieldKey }, cancellationToken);
        added += await AddMissingAsync(db, MasterData.Groups, x => x.GroupKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.GroupMembers, x => new { x.GroupKey, x.MemberIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.FieldGroups, x => new { x.FieldKey, x.Rarity, x.GroupKey }, cancellationToken);

        // 効果はスキルモーション（10c・10d）が effect_key で参照するため、スキルより先に入れる
        added += await AddMissingAsync(db, MasterData.Effects, x => x.EffectKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EffectStatusModifiers,
            x => new { x.EffectKey, x.TargetStatus }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EffectSlipDamages, x => x.EffectKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EffectDisableMoves, x => x.EffectKey, cancellationToken);

        // スキル。親（10番）を入れてからサブタイプ（10a〜10d）を入れる
        added += await AddMissingAsync(db, MasterData.Skills, x => x.SkillKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.Motions, x => new { x.SkillKey, x.MotionIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.AttackMotions, x => new { x.SkillKey, x.MotionIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.HealMotions, x => new { x.SkillKey, x.MotionIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EffectMotions, x => new { x.SkillKey, x.MotionIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.DispelMotions, x => new { x.SkillKey, x.MotionIndex }, cancellationToken);

        // 装備とアイテム。敵の装備候補・ドロップ候補が参照する
        added += await AddMissingAsync(db, MasterData.Equipment, x => x.EquipKey, cancellationToken);
        added += await AddMissingAsync(db, MasterData.Items, x => x.ItemKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.ItemEffects, x => new { x.ItemKey, x.UsageIndex }, cancellationToken);

        // 敵。スキル・効果・装備・アイテムがすべて揃った後
        added += await AddMissingAsync(db, MasterData.Enemies, x => x.EnemyKey, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EnemySkills, x => new { x.EnemyKey, x.SkillKey }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EnemyEffects, x => new { x.EnemyKey, x.EnemyEffectIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EnemyEquipment,
            x => new { x.EnemyKey, x.EnemyEquipmentIndex }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EnemyLoots, x => new { x.EnemyKey, x.ItemKey }, cancellationToken);
        added += await AddMissingAsync(
            db, MasterData.EnemyCurrency, x => x.EnemyKey, cancellationToken);

        added += await AddMissingAsync(db, MasterData.Titles, x => x.TitleKey, cancellationToken);

        if (added > 0) await db.SaveChangesAsync(cancellationToken);

        return added;
    }

    /// <summary>
    /// 既存のキー集合を読み、含まれない行だけを追加する。
    ///
    /// <para>
    /// 判定はメモリ上で行う。マスタは全体でも数百行の規模であり、行ごとに
    /// <c>EXISTS</c> を投げるより表を1度読むほうが安い。キーの取り出しを
    /// 呼び出し側の式に委ねているのは、複合キーの表が多く、
    /// 表ごとに同じ形の関数を書き並べても得るものが無いため。
    /// </para>
    /// </summary>
    private static async Task<int> AddMissingAsync<TEntity, TKey>(
        ChidoDbContext db,
        IReadOnlyList<TEntity> defined,
        Func<TEntity, TKey> keyOf,
        CancellationToken cancellationToken)
        where TEntity : class
        where TKey : notnull
    {
        if (defined.Count == 0) return 0;

        var existing = (await db.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken))
            .Select(keyOf)
            .ToHashSet();

        var missing = defined.Where(x => !existing.Contains(keyOf(x))).ToList();

        if (missing.Count == 0) return 0;

        db.Set<TEntity>().AddRange(missing);

        return missing.Count;
    }
}
