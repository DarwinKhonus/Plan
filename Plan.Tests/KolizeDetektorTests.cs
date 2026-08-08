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

    // --- Zohlednění pracovních dnů (Po–Pá, svátky nepracovní) ---

    private static PracovniKalendar Kalendar() => new(new PracovniNastaveni());

    [Fact]
    public void Prekryv_jen_pres_vikend_neni_kolize()
    {
        // 8. a 9. 8. 2026 je sobota a neděle — v ty dny se na žádné z nich nedělá.
        var a = Z(1, "2026-08-03", "2026-08-08");
        var b = Z(2, "2026-08-08", "2026-08-14");

        Assert.True(KolizeDetektor.SePrekryvaji(a, b));
        Assert.False(KolizeDetektor.Koliduji(a, b, Kalendar()));
    }

    [Fact]
    public void Prekryv_zasahujici_pracovni_den_je_kolize()
    {
        // Překryv 7.–10. 8. obsahuje pátek 7. 8. i pondělí 10. 8.
        var a = Z(1, "2026-08-03", "2026-08-10");
        var b = Z(2, "2026-08-07", "2026-08-14");

        Assert.True(KolizeDetektor.Koliduji(a, b, Kalendar()));
    }

    [Fact]
    public void Prekryv_jen_ve_svatek_neni_kolize()
    {
        // 28. 10. 2026 je středa, ale zároveň státní svátek.
        var a = Z(1, "2026-10-26", "2026-10-28");
        var b = Z(2, "2026-10-28", "2026-10-30");

        Assert.False(KolizeDetektor.Koliduji(a, b, Kalendar()));
    }

    [Fact]
    public void Prekryv_ve_svatek_je_kolize_kdyz_se_svatky_nezohlednuji()
    {
        var kalendar = new PracovniKalendar(new PracovniNastaveni { ZohlednitSvatky = false });
        var a = Z(1, "2026-10-26", "2026-10-28");
        var b = Z(2, "2026-10-28", "2026-10-30");

        Assert.True(KolizeDetektor.Koliduji(a, b, kalendar));
    }

    [Fact]
    public void Prekryv_pres_vikend_je_kolize_kdyz_se_o_vikendu_pracuje()
    {
        var kalendar = new PracovniKalendar(new PracovniNastaveni
        {
            PracovniDny = [.. Enum.GetValues<DayOfWeek>()],
        });

        var a = Z(1, "2026-08-03", "2026-08-08");
        var b = Z(2, "2026-08-08", "2026-08-14");

        Assert.True(KolizeDetektor.Koliduji(a, b, kalendar));
    }

    [Fact]
    public void Bez_kalendare_se_chova_jako_drive()
    {
        var a = Z(1, "2026-08-03", "2026-08-08");
        var b = Z(2, "2026-08-08", "2026-08-14");

        Assert.True(KolizeDetektor.Koliduji(a, b, null));
    }

    [Fact]
    public void NajdiKolidujici_s_kalendarem_vynecha_vikendovy_prekryv()
    {
        var zakazky = new[]
        {
            Z(1, "2026-08-03", "2026-08-08"),   // po–so
            Z(2, "2026-08-08", "2026-08-14"),   // so–pá, překryv jen v sobotu
            Z(3, "2026-08-12", "2026-08-20"),   // překryv se #2 ve všední dny
        };

        var kolidujici = KolizeDetektor.NajdiKolidujici(zakazky, Kalendar());

        Assert.Equal(new HashSet<int> { 2, 3 }, kolidujici);
    }

    [Fact]
    public void Zakazka_ciste_o_vikendu_nekoliduje_s_nicim()
    {
        var zakazky = new[]
        {
            Z(1, "2026-08-01", "2026-08-31"),   // celý srpen
            Z(2, "2026-08-08", "2026-08-09"),   // jen sobota a neděle
        };

        Assert.Empty(KolizeDetektor.NajdiKolidujici(zakazky, Kalendar()));
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
