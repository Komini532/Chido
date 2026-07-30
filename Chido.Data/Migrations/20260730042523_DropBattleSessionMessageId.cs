using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chido.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropBattleSessionMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "message_id",
                table: "chido_battle_session");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "message_id",
                table: "chido_battle_session",
                type: "bigint unsigned",
                nullable: true,
                comment: "戦闘状況を表示している埋め込みメッセージのID（編集対象）");
        }
    }
}
