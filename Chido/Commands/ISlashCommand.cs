using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// スラッシュコマンド1本。
///
/// <para>
/// 実装は DI コンテナへ登録し、<see cref="DiscordBotService"/> が名前で振り分ける。
/// static メソッドの辞書をやめたのは、コマンドがリポジトリ・カタログ・
/// <c>DbContext</c> を必要とするようになったため。static のままだと依存を
/// 内部で <c>new</c> するしかなく、テストから差し替える手段が無くなる。
/// </para>
/// </summary>
public interface ISlashCommand
{
    /// <summary>コマンド名（<c>/</c> の後ろ）。振り分けのキーを兼ねる。</summary>
    string Name { get; }

    string Description { get; }

    /// <summary>
    /// 登録用のビルダーを組み立てる。オプションの追加はここで行う。
    /// </summary>
    SlashCommandBuilder Build()
        => new SlashCommandBuilder().WithName(Name).WithDescription(Description);

    Task ExecuteAsync(SocketSlashCommand command);

    /// <summary>
    /// オートコンプリートの候補を返す。<c>[対象]</c> 等を持つコマンドが実装する。
    /// 既定は候補なし。
    /// </summary>
    Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
        => interaction.RespondAsync([]);
}
