using Chido.Battle;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 離脱。ターンを消費しないため、反撃も <c>SlipDamage</c> も状態変化の減衰も起こらない。
///
/// Active・Defeated のいずれの状態からも実行できる（生死を問わない）。
/// 戦闘不能プレイヤーが単一セッション制約の拘束を自力で解く唯一の手段であり、
/// 報酬を放棄する代わりに降りられるという安全弁である（戦闘システム 4.3）。
/// </summary>
public sealed class EscapeCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public override string Name => "escape";

    public override string Description => "戦闘から離脱します。報酬は得られません。";

    protected override string Title => "離脱";

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Escape);
}
