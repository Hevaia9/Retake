using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetakeSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRetakeSheetModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssessmentTypeByCertificate",
                table: "RetakeSheetDisciplines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentTypeByPlan",
                table: "RetakeSheetDisciplines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "RetakeSheetDisciplines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentTypeByCertificate",
                table: "RetakeSheetDisciplines");

            migrationBuilder.DropColumn(
                name: "AssessmentTypeByPlan",
                table: "RetakeSheetDisciplines");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "RetakeSheetDisciplines");
        }
    }
}
