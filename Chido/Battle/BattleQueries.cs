using Chido.Core.Battle;
using Chido.Data;
using Chido.Data.Loaders;
using Chido.Data.Repositories;
using Chido.Targeting;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace Chido.Battle;

/// <summary>
/// オートコンプリートのための読み出し。
///
/// <para>
/// <b>ロックを取らない。</b>Discord のオートコンプリートは入力のたびに飛んでくるうえ、
/// 応答が遅れると候補が出ないだけで済む。ここでチャンネル行を取ると、入力中のユーザーが
/// そのチャンネルの全戦闘行動を待たせることになり、7.3 の直列化の代償が
/// 「誰も行動していないのに待たされる」形で表に出る。
/// </para>
/// <para>
/// したがって読み出した内容は<b>候補の提示にしか使わない</b>。実際の受理判定は
/// <see cref="BattleService"/> がロック下で読み直した状態に対して行う。
/// 候補を選んでから実行するまでに敵が倒れていれば、そこで正しく弾かれる。
/// </para>
/// </summary>
public sealed class BattleQueries(
    IDbContextFactory<ChidoDbContext> dbFactory,
    GameCatalogs catalogs)
{
    /// <summary>
    /// <c>[対象]</c> の候補。値には <c>entity_id</c> を載せ、表示名はラベル側に置く（B-12）。
    /// 同名の敵が組にいる場合はラベルに表示順を添えて区別する。
    /// </summary>
    public async Task<IReadOnlyList<(string Label, string Value)>> TargetChoicesAsync(
        ulong channelId, string input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var record = await new BattleSessionRepository(db).FindActiveAsync(channelId, cancellationToken);
        if (record is null) return [];

        var session = await new BattleStateLoader(db, catalogs.Effects, catalogs.World)
            .LoadAsync(record, cancellationToken);

        var candidates = session.Participants
            .Where(p => p.Status != ParticipantStatus.Escaped)
            .ToList();

        return candidates
            .Select(p => (Label: TargetResolution.LabelOf(p, candidates), Value: p.Entity.Id.ToString()))
            .Where(x => Matches(x.Label, input))
            .ToList();
    }

    /// <summary>習得済みスキルの候補。通常攻撃と防御は含まれない（習得管理の対象外）。</summary>
    public async Task<IReadOnlyList<(string Label, string Value)>> SkillChoicesAsync(
        ulong userId, string input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var learned = await new InventoryRepository(db).LearnedSkillsAsync(userId, cancellationToken);

        return learned
            .Select(key => (Label: catalogs.Skills.Find(key)?.Name ?? key, Value: key))
            .Where(x => Matches(x.Label, input))
            .OrderBy(x => x.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 所持装備の候補。値には<b>インスタンスID</b>を載せる。
    /// 同じ装備を複数所持できる構造であるため、キーでは1つに定まらない。
    /// </summary>
    public async Task<IReadOnlyList<(string Label, string Value)>> EquipmentChoicesAsync(
        ulong userId, string input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var owned = await new EquipmentRepository(db).OwnedAsync(userId, cancellationToken);

        return owned
            .Select(x => (
                Label: x.EquippedPart is null ? x.Name : $"{x.Name}（装着中）",
                Value: x.InstanceId.ToString()))
            .Where(x => Matches(x.Label, input))
            .ToList();
    }

    /// <summary>装備の候補をそのまま応答する。<c>/equip</c> は <c>[対象]</c> を持たない。</summary>
    public async Task RespondWithEquipmentAsync(SocketAutocompleteInteraction interaction)
    {
        var input = interaction.Data.Current.Value?.ToString() ?? string.Empty;

        var choices = await EquipmentChoicesAsync(interaction.User.Id, input);

        await interaction.RespondAsync(
            choices.Take(25).Select(c => new AutocompleteResult(c.Label, c.Value)));
    }

    /// <summary>所持アイテムの候補。所持数を添える。</summary>
    public async Task<IReadOnlyList<(string Label, string Value)>> ItemChoicesAsync(
        ulong userId, string input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var owned = await new InventoryRepository(db).OwnedItemsAsync(userId, cancellationToken);

        return owned
            .Select(x => (Label: $"{x.Name} ×{x.Quantity}", Value: x.ItemKey))
            .Where(x => Matches(x.Label, input))
            .ToList();
    }

    /// <summary>
    /// 入力途中の文字列との照合。前方一致ではなく部分一致にするのは、
    /// オートコンプリートは絞り込みの補助であり、解決の規則（B-12）とは別物であるため。
    /// </summary>
    private static bool Matches(string label, string input)
        => string.IsNullOrWhiteSpace(input)
        || label.Contains(input.Trim(), StringComparison.OrdinalIgnoreCase);
}
