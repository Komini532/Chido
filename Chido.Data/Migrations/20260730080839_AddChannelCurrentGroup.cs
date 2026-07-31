using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chido.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCurrentGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "current_group_key",
                table: "chido_channel_state",
                type: "VARCHAR(64)",
                nullable: true,
                comment: "chido_enemy_group_master.group_key を参照。現在出現中の組。NULL=未抽選（初期化直後）。PlayerEscaped かつ前組が Common/Uncommon の場合は同一の group_key が再出現するため、次の出現の計画に必須（戦闘システム 10.3）。出現中の敵の集合からは逆引きできない")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte>(
                name: "current_rarity",
                table: "chido_channel_state",
                type: "TINYINT UNSIGNED",
                nullable: true,
                comment: "現在出現中の組のレアリティ。NULL=未抽選（初期化直後）。PlayerEscaped のレアリティ分岐（Rare 以上から降りたら Common へ落とす）と撃破報酬の根拠。chido_field_enemy_group_master からは同じ組が複数のフィールド・レアリティに登録されうるため逆引きできない");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_group_key",
                table: "chido_channel_state");

            migrationBuilder.DropColumn(
                name: "current_rarity",
                table: "chido_channel_state");
        }
    }
}
