using Plan.Data.Domain;

namespace Plan.Tests;

public class UsekyNormalizaceTests
{
    private static Rozsah R(string od, string doVcetne) =>
        new(DateOnly.Parse(od), DateOnly.Parse(doVcetne));

    [Fact]
    public void Nesouvisejici_useky_zustanou_oddelene()
    {
        var vysledek = UsekyNormalizace.Normalizuj([
            R("2026-08-03", "2026-08-07"),
            R("2026-08-17", "2026-08-21"),
        ]);

        Assert.Equal(2, vysledek.Count);
    }

    [Fact]
    public void Dotykajici_se_useky_se_neslijou()
    {
        // Rozdělení zakázky vyrábí právě takovou dvojici — slití by ho hned vrátilo zpět.
        var vysledek = UsekyNormalizace.Normalizuj([
            R("2026-08-03", "2026-08-09"),
            R("2026-08-10", "2026-08-14"),
        ]);

        Assert.Equal(2, vysledek.Count);
    }

    [Fact]
    public void Prekryvajici_se_useky_se_slijou()
    {
        var vysledek = UsekyNormalizace.Normalizuj([
            R("2026-08-03", "2026-08-12"),
            R("2026-08-10", "2026-08-20"),
        ]);

        Assert.Single(vysledek);
        Assert.Equal(R("2026-08-03", "2026-08-20"), vysledek[0]);
    }

    [Fact]
    public void Zcela_pohlceny_usek_se_slije_bez_zkraceni()
    {
        var vysledek = UsekyNormalizace.Normalizuj([
            R("2026-08-01", "2026-08-31"),
            R("2026-08-10", "2026-08-12"),
        ]);

        Assert.Single(vysledek);
        Assert.Equal(R("2026-08-01", "2026-08-31"), vysledek[0]);
    }

    [Fact]
    public void Useky_se_seradi_podle_zacatku()
    {
        var vysledek = UsekyNormalizace.Normalizuj([
            R("2026-09-01", "2026-09-05"),
            R("2026-08-03", "2026-08-07"),
        ]);

        Assert.Equal(R("2026-08-03", "2026-08-07"), vysledek[0]);
        Assert.Equal(R("2026-09-01", "2026-09-05"), vysledek[1]);
    }

    [Fact]
    public void PokryteDny_pocita_kazdy_den_jednou()
    {
        var dny = UsekyNormalizace.PokryteDny([
            R("2026-08-03", "2026-08-05"),
            R("2026-08-04", "2026-08-06"),
        ]);

        Assert.Equal(4, dny.Count);
    }

    [Fact]
    public void Odhad_hodin_pres_useky_nezapocita_den_dvakrat()
    {
        var kalendar = new PracovniKalendar(new PracovniNastaveni());

        // 3.–7. 8. 2026 je po–pá (5 dnů), překrývající se úsek nesmí odhad nafouknout.
        var hodiny = kalendar.OdhadHodin([
            R("2026-08-03", "2026-08-07"),
            R("2026-08-05", "2026-08-07"),
        ]);

        Assert.Equal(5 * 8.5, hodiny);
    }

    [Fact]
    public void Odhad_hodin_secte_oddelene_useky_a_vynecha_pauzu()
    {
        var kalendar = new PracovniKalendar(new PracovniNastaveni());

        // 3.–7. 8. (po–pá, 5 dnů) + 17.–21. 8. (po–pá, 5 dnů), pauza mezi tím se nepočítá.
        var hodiny = kalendar.OdhadHodin([
            R("2026-08-03", "2026-08-07"),
            R("2026-08-17", "2026-08-21"),
        ]);

        Assert.Equal(10 * 8.5, hodiny);
    }
}
