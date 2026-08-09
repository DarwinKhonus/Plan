using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plan.Data.Migrations
{
    /// <summary>
    /// Pozice zakázky v ručním řazení. Uplatní se, jen když je v nastavení vypnuté
    /// automatické řazení podle termínu.
    /// </summary>
    public partial class PoradiZakazek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Poradi",
                table: "Zakazky",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Výchozí nula by všem zakázkám dala stejné pořadí a ruční řazení by
            // začínalo náhodně. Naplní se proto podle dosavadního řazení, tedy
            // podle začátku prvního úseku.
            migrationBuilder.Sql(
                """
                WITH "vypocet" AS (
                    SELECT
                        "z"."Id" AS "ZakazkaId",
                        ROW_NUMBER() OVER (
                            ORDER BY
                                (SELECT MIN("u"."DatumOd") FROM "Useky" "u" WHERE "u"."ZakazkaId" = "z"."Id"),
                                "z"."Id"
                        ) AS "Cislo"
                    FROM "Zakazky" "z"
                )
                UPDATE "Zakazky"
                SET "Poradi" = (
                    SELECT "Cislo" FROM "vypocet" WHERE "vypocet"."ZakazkaId" = "Zakazky"."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Poradi",
                table: "Zakazky");
        }
    }
}
