using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data.Catalogs;

/// <summary>
/// 状態変化マスタとそのサブテーブル（16〜18番・45番）から <see cref="EffectDefinition"/> を組み立てる。
///
/// <para>
/// 1つの状態変化が複数の効果種別を兼ねうる（マルチネイチャー）ため、各成分は独立に読み出して合成する。
/// マスタ側の <c>effect_types</c> はどのサブテーブルに行があるかの非正規化キャッシュであり、
/// <b>真実の情報源は各成分そのもの</b>であるため、ここでは参照せずサブテーブルの実体から組み立てる。
/// </para>
/// <para>
/// <c>SkillCatalog</c> / <c>DatabaseWorldCatalog</c> と同じく一括読み込みして保持する。
/// </para>
/// </summary>
public sealed class EffectCatalog
{
    private readonly Dictionary<string, EffectDefinition> definitions;

    private EffectCatalog(Dictionary<string, EffectDefinition> definitions)
        => this.definitions = definitions;

    /// <summary><see cref="EffectApplier"/> へそのまま渡せる形。</summary>
    public IReadOnlyDictionary<string, EffectDefinition> Definitions => definitions;

    public static async Task<EffectCatalog> LoadAsync(
        ChidoDbContext db, CancellationToken cancellationToken = default)
    {
        var masters = await db.EffectMasters.ToListAsync(cancellationToken);

        var statusModifiers = (await db.EffectStatusModifierMasters.ToListAsync(cancellationToken))
            .GroupBy(x => x.EffectKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.TargetStatus)
                    .Select(x => new StatusModifierSpec(x.TargetStatus, x.FixedRate))
                    .ToList());

        var slipDamages = (await db.EffectSlipDamageMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => x.EffectKey, x => new SlipDamageSpec(x.Power, x.Elements));

        var disableMoves = (await db.EffectDisableMoveMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => x.EffectKey, x => x.DisableRate);

        var elementGrants = (await db.EffectElementGrantMasters.ToListAsync(cancellationToken))
            .ToDictionary(x => x.EffectKey, x => x.Elements);

        var definitions = masters.ToDictionary(
            master => master.EffectKey,
            master => new EffectDefinition(
                master.EffectKey,
                master.Name,
                master.ClearOnBattleEnd,
                statusModifiers.GetValueOrDefault(master.EffectKey),
                slipDamages.TryGetValue(master.EffectKey, out var slip) ? slip : null,
                disableMoves.TryGetValue(master.EffectKey, out var rate) ? rate : null,
                elementGrants.TryGetValue(master.EffectKey, out var elements) ? elements : Element.None));

        return new EffectCatalog(definitions);
    }

    public EffectDefinition? Find(string effectKey) => definitions.GetValueOrDefault(effectKey);

    /// <summary>表示名。マスタに無ければキーをそのまま返す（描画が落ちないようにするため）。</summary>
    public string NameOf(string effectKey) => Find(effectKey)?.Name ?? effectKey;
}
