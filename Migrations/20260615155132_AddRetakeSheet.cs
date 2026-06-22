using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RetakeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRetakeSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetakeSheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    FromSpecialty = table.Column<string>(type: "text", nullable: false),
                    ToSpecialty = table.Column<string>(type: "text", nullable: false),
                    TotalHoursTransferred = table.Column<int>(type: "integer", nullable: false),
                    TotalHoursDebt = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetakeSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetakeSheets_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetakeSheetDisciplines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RetakeSheetId = table.Column<int>(type: "integer", nullable: false),
                    DisciplineId = table.Column<int>(type: "integer", nullable: false),
                    HoursByPlan = table.Column<int>(type: "integer", nullable: false),
                    HoursByCertificate = table.Column<int>(type: "integer", nullable: false),
                    HoursDifference = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetakeSheetDisciplines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetakeSheetDisciplines_Disciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "Disciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetakeSheetDisciplines_RetakeSheets_RetakeSheetId",
                        column: x => x.RetakeSheetId,
                        principalTable: "RetakeSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetakeSheetDisciplines_DisciplineId",
                table: "RetakeSheetDisciplines",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_RetakeSheetDisciplines_RetakeSheetId",
                table: "RetakeSheetDisciplines",
                column: "RetakeSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_RetakeSheets_StudentId",
                table: "RetakeSheets",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetakeSheetDisciplines");

            migrationBuilder.DropTable(
                name: "RetakeSheets");
        }
    }
}
