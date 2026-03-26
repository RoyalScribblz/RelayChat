using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelayChat.Node.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerMemberships",
                columns: table => new
                {
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMemberships", x => new { x.ServerId, x.UserId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerMemberships");
        }
    }
}
