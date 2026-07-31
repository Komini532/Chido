using Chido.Battle;
using Discord;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>通常攻撃。習得手続きなしで常に使える（習得管理の対象外）。</summary>
public sealed class AttackCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public override string Name => "attack";

    public override string Description => "通常攻撃を行います。";

    protected override string Title => "通常攻撃";

    public override SlashCommandBuilder Build() => WithTarget(base.Build());

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Attack);
}
