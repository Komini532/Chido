using Chido.Core.Battle.Effects;
using Chido.Core.World;
using Chido.Data;
using Chido.Data.Catalogs;
using Chido.Data.World;
using Microsoft.EntityFrameworkCore;

namespace Chido.Battle;

/// <summary>
/// 起動時に一括で読み込むマスタ一式。
///
/// <para>
/// <b>戦闘中にマスタは変化しない。</b>スキル・状態変化・敵・フィールドのいずれも、
/// 参照はチャンネル行ロック下の同一トランザクションで行われるため、都度クエリを発行すると
/// ロック保持時間がそのまま伸び、7.3 の直列化の待ち時間に上乗せされる。
/// マスタを投入・変更したら <see cref="ReloadAsync"/> を呼ぶ。
/// </para>
/// <para>
/// 読み込み済みのインスタンスは差し替え時にまとめて入れ替える。個別に差し替えると、
/// 「スキルは新しいがそのスキルが参照する効果は古い」という中間状態が観測されうる。
/// </para>
/// </summary>
public sealed class GameCatalogs(IDbContextFactory<ChidoDbContext> dbFactory)
{
    private Snapshot? snapshot;

    public SkillCatalog Skills => Current.Skills;

    public EffectCatalog Effects => Current.Effects;

    public DatabaseWorldCatalog World => Current.World;

    /// <summary>
    /// 状態変化の表示名。マスタに無いキーはキーをそのまま返す（描画が落ちないようにするため）。
    /// </summary>
    public Func<string, string> EffectNameOf => Current.Effects.NameOf;

    /// <summary>付与・解除の実行主体。マスタ辞書を束ねただけの薄い型。</summary>
    public EffectApplier NewApplier() => new(Current.Effects.Definitions);

    private Snapshot Current => snapshot
        ?? throw new InvalidOperationException(
            "マスタが読み込まれていない。起動時に GameCatalogs.ReloadAsync を呼ぶこと。");

    /// <summary>
    /// マスタを読み込み直し、<b>起動時検証</b>（戦闘システム 10.5）を通す。
    ///
    /// 草原フォールバックは <c>DrawGroup</c> と <c>NextField</c> の両方が依存しているため、
    /// 草原とその <c>Common</c> の組が欠けていると、抽選のたびに例外へ落ちる。
    /// これを実行時の例外だけで守ると発覚がプレイヤーの行動時点まで遅れるため、ここで止める。
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var skills = await SkillCatalog.LoadAsync(db, cancellationToken);
        var effects = await EffectCatalog.LoadAsync(db, cancellationToken);
        var world = await DatabaseWorldCatalog.LoadAsync(db, skills, cancellationToken);

        WorldValidation.ThrowIfInvalid(world);

        snapshot = new Snapshot(skills, effects, world);
    }

    private sealed record Snapshot(
        SkillCatalog Skills, EffectCatalog Effects, DatabaseWorldCatalog World);
}
