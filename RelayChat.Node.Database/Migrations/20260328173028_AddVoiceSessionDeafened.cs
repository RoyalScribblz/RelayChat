using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelayChat.Node.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceSessionDeafened : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeafened",
                table: "VoiceSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeafened",
                table: "VoiceSessions");
        }
    }
}
