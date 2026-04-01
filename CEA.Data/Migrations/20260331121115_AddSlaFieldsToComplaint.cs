using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaFieldsToComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BreachReason",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Complaints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSlaBreached",
                table: "Complaints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionTimeMinutes",
                table: "Complaints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionType",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseTimeMinutes",
                table: "Complaints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreachReason",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "IsSlaBreached",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResolutionTimeMinutes",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResolutionType",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResponseTimeMinutes",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "Complaints");
        }
    }
}
