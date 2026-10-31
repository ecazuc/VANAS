using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VANAS.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgeAndConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentalConsentAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentalConsentById",
                table: "Users",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentalConsentAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentalConsentById",
                table: "Users");
        }
    }
}
