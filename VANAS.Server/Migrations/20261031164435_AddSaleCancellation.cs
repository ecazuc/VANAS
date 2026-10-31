using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VANAS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByName",
                table: "Sales",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledByName",
                table: "Sales");
        }
    }
}
