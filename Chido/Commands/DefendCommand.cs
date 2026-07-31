using Chido.Battle;
using Discord.WebSocket;

namespace Chido.Commands;

/// <summary>
/// 防御。自分自身への DRR（ダメージ軽減率）付与モーション1つで構成され、反撃モーションを含まない。
///
/// 軽減の実体はダメージパイプライン側にあり（戦闘システム 5.1）、このコマンドは
/// 状態変化を1つ付与するだけの通常のスキル発動として処理される。
/// <c>[対象]</c> を取らないのは、対象が常に自分自身であるため。
/// </summary>
public sealed class DefendCommand(BattleService battles, BattleQueries queries)
    : BattleCommandBase(battles, queries)
{
    public override string Name => "defend";

    public override string Description => "身を守り、受けるダメージを軽減します。";

    protected override string Title => "防御";

    protected override BattleActionRequest BuildRequest(SocketSlashCommand command)
        => NewRequest(command, BattleActionKind.Defend);
}
