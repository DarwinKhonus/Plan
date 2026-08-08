using Plan.Data.Domain;
using Plan.Data.Entities;

namespace Plan.Tests;

public class KolizeDetektorTests
{
    private static Zakazka Z(int id, string od, string doVcetne) => new()
    {
        Id = id,
        Nazev = $"Zakázka {id}",
        DatumOd = DateOnly.Parse(od),
        DatumDo = DateOnly.Parse(doVcetne),
    };

    [Fact]
    public void Dotyk_konec_na_zacatek_je_kolize()
    {
        var a = Z(1, "2026-03-01", "2026-03-10");
        var b = Z(2, "2026-03-10", "2026-03-20");

        Assert.True(KolizeDetektor.SePrekryvaji(a, b));
    }

    [Fact]
    public void Navazujici_termin_o_den_pozdeji_neni_kolize()
    {
        var a = Z(1, "2026-03-01", "2026-03-10");
        var b = Z(2, "2026-03-11", "2026-03-20");

        Assert.False(KolizeDetektor.SePrekryvaji(a, b));
    }

    [Fact]
    public void Zcela_vnorena_zakazka_je_kolize()
    {
        var a = Z(1, "2026-03-01", "2026-03-31");
        var b = Z(2, "2026-03-10", "2026-03-12");

        Assert.True(KolizeDetektor.SePrekryvaji(a, b));
        Assert.True(KolizeDetektor.SePrekryvaji(b, a));
    }

    [Fact]
    public void Jednodenni_zakazky_ve_stejny_den_koliduji()
    {
        var a = Z(1, "2026-03-05", "2026-03-05");
        var b = Z(2, "2026-03-05", "2026-03-05");

        Assert.True(KolizeDetektor.SePrekryvaji(a, b));
    }

    [Fact]
    public void Jednodenni_zakazky_v_ruzne_dny_nekoliduji()
    {
        var a = Z(1, "2026-03-05", "2026-03-05");
        var b = Z(2, "2026-03-06", "2026-03-06");

        Assert.False(KolizeDetektor.SePrekryvaji(a, b));
    }

    [Fact]
    public void NajdiKolidujici_vrati_prazdnou_mnozinu_pro_nekolidujici_zakazky()
    {
        var zakazky = new[]
        {
            Z(1, "2026-03-01", "2026-03-05"),
            Z(2, "2026-03-06", "2026-03-10"),
            Z(3, "2026-03-11", "2026-03-15"),
        };

        Assert.Empty(KolizeDetektor.NajdiKolidujici(zakazky));
    }

    [Fact]
    public void NajdiKolidujici_oznaci_obe_strany_kolize()
    {
        var zakazky = new[]
        {
            Z(1, "2026-03-01", "2026-03-10"),
            Z(2, "2026-03-08", "2026-03-15"),
            Z(3, "2026-04-01", "2026-04-05"),
        };

        var kolidujici = KolizeDetektor.NajdiKolidujici(zakazky);

        Assert.Equal(new HashSet<int> { 1, 2 }, kolidujici);
    }

    [Fact]
    public void NajdiKolidujici_zvlada_zakazku_prekryvajici_vic_dalsich()
    {
        var zakazky = new[]
        {
            Z(1, "2026-03-01", "2026-03-31"),
            Z(2, "2026-03-05", "2026-03-06"),
            Z(3, "2026-03-20", "2026-03-21"),
            Z(4, "2026-05-01", "2026-05-02"),
        };

        var kolidujici = KolizeDetektor.NajdiKolidujici(zakazky);

        Assert.Equal(new HashSet<int> { 1, 2, 3 }, kolidujici);
    }

    [Fact]
    public void NajdiKolidujici_neoznaci_zakazku_kvuli_sobe_same()
    {
        var zakazky = new[] { Z(1, "2026-03-01", "2026-03-10") };

        Assert.Empty(KolizeDetektor.NajdiKolidujici(zakazky));
    }

    /// <summary>
    /// Vnořená zakázka končí dřív než ta předchozí. Kdyby se předčasné ukončení vnitřní
    /// smyčky řídilo koncem poslední porovnávané zakázky místo té aktuální, tenhle případ
    /// by kolizi mezi 1 a 4 přehlédl.
    /// </summary>
    [Fact]
    public void NajdiKolidujici_nepresklaci_kolizi_za_kratkou_vnorenou_zakazkou()
    {
        var zakazky = new[]
        {
            Z(1, "2026-03-01", "2026-03-31"),
            Z(2, "2026-03-02", "2026-03-03"),
            Z(4, "2026-03-30", "2026-04-10"),
        };

        var kolidujici = KolizeDetektor.NajdiKolidujici(zakazky);

        Assert.Equal(new HashSet<int> { 1, 2, 4 }, kolidujici);
    }
}
