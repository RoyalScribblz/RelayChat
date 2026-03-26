using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RelayChat.Node.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelOrderingAndMemberProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Memberships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Handle",
                table: "Memberships",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Memberships",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Channels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Channels",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "SortOrder",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Channels",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "SortOrder",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "Handle",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Channels");
        }
    }
}
