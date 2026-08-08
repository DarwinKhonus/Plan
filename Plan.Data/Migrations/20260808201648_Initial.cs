using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plan.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nastaveni",
                columns: table => new
                {
                    Klic = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Hodnota = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nastaveni", x => x.Klic);
                });

            migrationBuilder.CreateTable(
                name: "Zakazky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nazev = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DatumOd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DatumDo = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    VytvorenoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpravenoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zakazky", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Zakazky_DatumOd",
                table: "Zakazky",
                column: "DatumOd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Nastaveni");

            migrationBuilder.DropTable(
                name: "Zakazky");
        }
    }
}
