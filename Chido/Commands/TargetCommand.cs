using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 次の行動の宛先を決める。
///
/// <b>セッションを生成しない</b>（B-1）。ターンも反撃も発生せず、<c>CurrentTarget</c> を
/// 書き換えるだけで完結する。セッション生成の契機は「ターンが開く行動」に限られる。
/// </summary>
public sealed class TargetCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public override string Name => "target";

    public override string Description => "次の行動で狙う敵を指定します。";

    protected override string Title => "対象指定";

    public override SlashCommandBuilder Build() => WithTarget(base.Build());

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Target);
}
