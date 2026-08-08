using Plan.Data.Domain;

namespace Plan.Tests;

public class SvatkyCzTests
{
    // Známá data Velikonoční neděle podle gregoriánského kalendáře.
    [Theory]
    [InlineData(2020, "2020-04-12")]
    [InlineData(2021, "2021-04-04")]
    [InlineData(2022, "2022-04-17")]
    [InlineData(2023, "2023-04-09")]
    [InlineData(2024, "2024-03-31")]
    [InlineData(2025, "2025-04-20")]
    [InlineData(2026, "2026-04-05")]
    [InlineData(2027, "2027-03-28")]
    [InlineData(2030, "2030-04-21")]
    [InlineData(2038, "2038-04-25")] // nejzazší možné datum
    [InlineData(2285, "2285-03-22")] // nejranější možné datum
    public void VelikonocniNedele_odpovida_znamym_datum(int rok, string ocekavane)
    {
        Assert.Equal(DateOnly.Parse(ocekavane), SvatkyCz.VelikonocniNedele(rok));
    }

    [Fact]
    public void Velikonocni_nedele_vzdy_pada_na_nedeli()
    {
        for (var rok = 2000; rok <= 2100; rok++)
        {
            Assert.Equal(DayOfWeek.Sunday, SvatkyCz.VelikonocniNedele(rok).DayOfWeek);
        }
    }

    [Fact]
    public void Velky_patek_a_velikonocni_pondeli_jsou_svatky()
    {
        // 2026: Velikonoční neděle 5. 4. → Velký pátek 3. 4., pondělí 6. 4.
        Assert.True(SvatkyCz.JeSvatek(new DateOnly(2026, 4, 3)));
        Assert.True(SvatkyCz.JeSvatek(new DateOnly(2026, 4, 6)));
        Assert.Equal("Velký pátek", SvatkyCz.NazevSvatku(new DateOnly(2026, 4, 3)));
        Assert.Equal("Velikonoční pondělí", SvatkyCz.NazevSvatku(new DateOnly(2026, 4, 6)));
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 5, 1)]
    [InlineData(2026, 5, 8)]
    [InlineData(2026, 7, 5)]
    [InlineData(2026, 7, 6)]
    [InlineData(2026, 9, 28)]
    [InlineData(2026, 10, 28)]
    [InlineData(2026, 11, 17)]
    [InlineData(2026, 12, 24)]
    [InlineData(2026, 12, 25)]
    [InlineData(2026, 12, 26)]
    public void Pevne_svatky_jsou_rozpoznane(int rok, int mesic, int den)
    {
        Assert.True(SvatkyCz.JeSvatek(new DateOnly(rok, mesic, den)));
    }

    [Fact]
    public void Bezny_den_neni_svatek()
    {
        Assert.False(SvatkyCz.JeSvatek(new DateOnly(2026, 3, 11)));
        Assert.Null(SvatkyCz.NazevSvatku(new DateOnly(2026, 3, 11)));
    }

    [Fact]
    public void ProRok_vrati_trinact_svatku_serazenych()
    {
        var svatky = SvatkyCz.ProRok(2026);

        Assert.Equal(13, svatky.Count);
        Assert.Equal(svatky.OrderBy(s => s.Datum).Select(s => s.Datum), svatky.Select(s => s.Datum));
    }

    [Fact]
    public void ProRozsahLet_pokryva_vsechny_roky()
    {
        var svatky = SvatkyCz.ProRozsahLet(2025, 2027);

        Assert.Contains(new DateOnly(2025, 1, 1), svatky);
        Assert.Contains(new DateOnly(2026, 7, 6), svatky);
        Assert.Contains(new DateOnly(2027, 12, 26), svatky);
        Assert.DoesNotContain(new DateOnly(2028, 1, 1), svatky);
    }
}
