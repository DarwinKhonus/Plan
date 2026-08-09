using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plan.Data.Migrations
{
    /// <summary>
    /// Termín zakázky se stěhuje z dvojice sloupců na tabulku úseků, aby šla zakázka
    /// rozdělit na víc částí. Přibývají milníky.
    /// </summary>
    /// <remarks>
    /// Pořadí operací je ručně upravené oproti vygenerovanému: EF chtěl zahodit
    /// DatumOd/DatumDo dřív, než vznikne tabulka Useky, což by smazalo všechny termíny.
    /// Sloupce se proto ruší až po překopírování dat.
    /// </remarks>
    public partial class UsekyAMilniky : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Useky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZakazkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumOd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DatumDo = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Useky", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Useky_Zakazky_ZakazkaId",
                        column: x => x.ZakazkaId,
                        principalTable: "Zakazky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Milniky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZakazkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Nazev = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Milniky", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Milniky_Zakazky_ZakazkaId",
                        column: x => x.ZakazkaId,
                        principalTable: "Zakazky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Každá dosavadní zakázka se stane zakázkou s jedním úsekem.
            migrationBuilder.Sql(
                """
                INSERT INTO "Useky" ("ZakazkaId", "DatumOd", "DatumDo")
                SELECT "Id", "DatumOd", "DatumDo" FROM "Zakazky";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Milniky_ZakazkaId",
                table: "Milniky",
                column: "ZakazkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Useky_DatumOd",
                table: "Useky",
                column: "DatumOd");

            migrationBuilder.CreateIndex(
                name: "IX_Useky_ZakazkaId",
                table: "Useky",
                column: "ZakazkaId");

            // Teprve teď, když jsou data v bezpečí.
            migrationBuilder.DropIndex(
                name: "IX_Zakazky_DatumOd",
                table: "Zakazky");

            migrationBuilder.DropColumn(
                name: "DatumDo",
                table: "Zakazky");

            migrationBuilder.DropColumn(
                name: "DatumOd",
                table: "Zakazky");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DatumDo",
                table: "Zakazky",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DatumOd",
                table: "Zakazky",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Zpět se dá uložit jen celkový rozsah; rozdělení na úseky a milníky
            // se návratem na starší verzi nutně ztrácí.
            migrationBuilder.Sql(
                """
                UPDATE "Zakazky" SET
                    "DatumOd" = COALESCE((SELECT MIN("DatumOd") FROM "Useky" WHERE "ZakazkaId" = "Zakazky"."Id"), '0001-01-01'),
                    "DatumDo" = COALESCE((SELECT MAX("DatumDo") FROM "Useky" WHERE "ZakazkaId" = "Zakazky"."Id"), '0001-01-01');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Zakazky_DatumOd",
                table: "Zakazky",
                column: "DatumOd");

            migrationBuilder.DropTable(
                name: "Milniky");

            migrationBuilder.DropTable(
                name: "Useky");
        }
    }
}
