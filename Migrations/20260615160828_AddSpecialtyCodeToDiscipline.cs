using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetakeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialtyCodeToDiscipline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecialtyCode",
                table: "Disciplines",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialtyCode",
                table: "Disciplines");
        }
    }
}
