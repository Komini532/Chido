using System.Numerics;
using Chido.Battle;
using Chido.Rendering;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// プレイヤー単位の<b>永続情報</b>を表示する（戦闘システム 9.1）。
///
/// <para>
/// 現在HP・現在TP・戦闘スコープの状態変化といったセッション情報は扱わない。
/// 恒常的なステータスと、いま参加している戦闘の状況とでは、表示されるべき文脈が異なる。
/// </para>
/// <para>
/// <b>永続スコープの状態変化はここに含まれる。</b>分離の基準は「セッションに属するか否か」
/// であって「状態変化か否か」ではない。戦闘中でないプレイヤーが、自分に何が残り
/// 何行動効いているかを知る手段が他に存在しない。
/// </para>
/// </summary>
public sealed class StatusCommand(PlayerProfileService profiles, GameCatalogs catalogs) : ISlashCommand
{
    public string Name => "status";

    public string Description => "レベル・装備・称号などのプレイヤー情報を表示します。";

    public async Task ExecuteAsync(SocketSlashCommand command)
    {
        await command.DeferAsync();

        var profile = await profiles.LoadAsync(
            command.User.Id, command.User.GlobalName ?? command.User.Username);

        var builder = new EmbedBuilder()
            .WithTitle($"{profile.Name} のステータス")
            .WithColor(Color.DarkBlue)
            .AddField("レベル", $"{profile.Level}（経験値 {profile.Exp}）")
            .AddField("所持金額", profile.Currency.ToString())
            .AddField("ステータス", Stats(profile));

        builder.AddField("装備", Equipment(profile));
        builder.AddField("称号", Titles(profile));

        // 永続スコープの状態変化。残り有効行動数は必ず有限であるため [呪い] (7) の形で出る
        var effects = EffectDisplay.Render(profile.Effects, catalogs.EffectNameOf);
        builder.AddField("状態変化", effects.Count == 0 ? "なし" : string.Join("\n", effects));

        await command.ModifyOriginalResponseAsync(m => m.Embed = builder.Build());
    }

    /// <summary>
    /// ステータスは保持せず参照のたびに算出される（戦闘システム 2.5）。
    /// ここに並ぶのは、レベル・装備・永続効果から読み出しの瞬間に導かれた結果。
    /// </summary>
    private static string Stats(PlayerProfile profile) => string.Join("\n",
        $"HP {profile.MaxLife}",
        $"物理攻撃 {profile.PAtk} / 物理防御 {profile.PDef}",
        $"魔法攻撃 {profile.MAtk} / 魔法防御 {profile.MDef}",
        $"素早さ {profile.Speed} / 運 {profile.Luck}");

    private static string Equipment(PlayerProfile profile)
        => profile.Equipment.Count == 0
            ? "なし"
            : string.Join("\n", profile.Equipment.Select(x => $"{PartName(x.Part)}: {x.Equipment.Name}"));

    private static string Titles(PlayerProfile profile)
        => profile.Titles.Count == 0
            ? "なし"
            : string.Join(" ", profile.Titles.Select(x => $"{x.Emoji}{x.Name}"));

    internal static string PartName(Core.Equipment.EquipPart part) => part switch
    {
        Core.Equipment.EquipPart.Weapon => "武器",
        Core.Equipment.EquipPart.Head => "頭",
        Core.Equipment.EquipPart.Chest => "胴",
        Core.Equipment.EquipPart.Legs => "脚",
        Core.Equipment.EquipPart.Accessory1 => "装飾",

        _ => part.ToString(),
    };
}
