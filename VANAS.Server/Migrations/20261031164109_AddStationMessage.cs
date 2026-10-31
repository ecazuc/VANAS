using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VANAS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddStationMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingMessage",
                table: "Stations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingMessageAt",
                table: "Stations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingMessage",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "PendingMessageAt",
                table: "Stations");
        }
    }
}
