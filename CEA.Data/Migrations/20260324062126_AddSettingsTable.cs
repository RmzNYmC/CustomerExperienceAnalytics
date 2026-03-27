using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CEA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Surveys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SurveyResponses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Questions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "QuestionOptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Complaints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ComplaintNotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Answers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "UserGroups",
                keyColumn: "Id",
                keyValue: 1,
                column: "DeletedAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserGroups");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Surveys");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SurveyResponses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "QuestionOptions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ComplaintNotes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Answers");
        }
    }
}
