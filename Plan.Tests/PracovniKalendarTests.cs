using Plan.Data.Domain;

namespace Plan.Tests;

public class PracovniKalendarTests
{
    private static PracovniKalendar Kalendar(bool zohlednitSvatky = true, params DayOfWeek[] dny)
    {
        var nastaveni = new PracovniNastaveni { ZohlednitSvatky = zohlednitSvatky };
        if (dny.Length > 0)
        {
            nastaveni.PracovniDny = new HashSet<DayOfWeek>(dny);
        }

        return new PracovniKalendar(nastaveni);
    }

    [Fact]
    public void Vychozi_nastaveni_je_po_az_pa_a_8_5_hodiny()
    {
        var nastaveni = new PracovniNastaveni();

        Assert.Equal(5, nastaveni.PracovniDny.Count);
        Assert.DoesNotContain(DayOfWeek.Saturday, nastaveni.PracovniDny);
        Assert.DoesNotContain(DayOfWeek.Sunday, nastaveni.PracovniDny);
        Assert.Equal(8.5, nastaveni.HodinDenne);
    }

    [Fact]
    public void Vikend_neni_pracovni_den()
    {
        var kalendar = Kalendar();

        Assert.False(kalendar.JePracovniDen(new DateOnly(2026, 3, 7)));  // sobota
        Assert.False(kalendar.JePracovniDen(new DateOnly(2026, 3, 8)));  // neděle
        Assert.True(kalendar.JePracovniDen(new DateOnly(2026, 3, 9)));   // pondělí
    }

    [Fact]
    public void Svatek_v_pracovni_den_se_odecte_kdyz_je_zohlednovani_zapnute()
    {
        var kalendar = Kalendar(zohlednitSvatky: true);

        // 1. 5. 2026 je pátek a zároveň Svátek práce.
        Assert.False(kalendar.JePracovniDen(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void Svatek_se_pocita_jako_pracovni_kdyz_je_zohlednovani_vypnute()
    {
        var kalendar = Kalendar(zohlednitSvatky: false);

        Assert.True(kalendar.JePracovniDen(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void PocetPracovnichDnu_pocita_oba_krajni_dny()
    {
        var kalendar = Kalendar();

        // Po 2026-03-09 až Pá 2026-03-13 = 5 pracovních dnů.
        Assert.Equal(5, kalendar.PocetPracovnichDnu(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 13)));
    }

    [Fact]
    public void PocetPracovnichDnu_preskoci_vikend_uprostred()
    {
        var kalendar = Kalendar();

        // Po 2026-03-09 až Po 2026-03-16 = 6 pracovních dnů (víkend 14.–15. vypadne).
        Assert.Equal(6, kalendar.PocetPracovnichDnu(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 16)));
    }

    [Fact]
    public void PocetPracovnichDnu_odecte_svatek_v_intervalu()
    {
        var kalendar = Kalendar(zohlednitSvatky: true);

        // Po 2026-04-27 až Pá 2026-05-01: 5 všedních dnů, ale 1. 5. je svátek → 4.
        Assert.Equal(4, kalendar.PocetPracovnichDnu(new DateOnly(2026, 4, 27), new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void Jednodenni_interval_v_pracovni_den_je_jeden_den()
    {
        var kalendar = Kalendar();

        Assert.Equal(1, kalendar.PocetPracovnichDnu(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public void Interval_jen_pres_vikend_ma_nula_pracovnich_dnu()
    {
        var kalendar = Kalendar();

        Assert.Equal(0, kalendar.PocetPracovnichDnu(new DateOnly(2026, 3, 7), new DateOnly(2026, 3, 8)));
    }

    [Fact]
    public void Obraceny_interval_vrati_nulu_misto_vyjimky()
    {
        var kalendar = Kalendar();

        Assert.Equal(0, kalendar.PocetPracovnichDnu(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void OdhadHodin_je_pracovni_dny_krat_hodin_denne()
    {
        var kalendar = Kalendar();

        // 5 pracovních dnů × 8,5 h.
        Assert.Equal(42.5, kalendar.OdhadHodin(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 13)));
    }

    [Fact]
    public void Vlastni_pracovni_dny_se_respektuji()
    {
        var kalendar = Kalendar(zohlednitSvatky: false, DayOfWeek.Saturday, DayOfWeek.Sunday);

        Assert.True(kalendar.JePracovniDen(new DateOnly(2026, 3, 7)));
        Assert.False(kalendar.JePracovniDen(new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public void Konec_prace_pred_zacatkem_dava_nula_hodin_misto_zaporne_hodnoty()
    {
        var nastaveni = new PracovniNastaveni
        {
            ZacatekPrace = new TimeOnly(16, 0),
            KonecPrace = new TimeOnly(8, 0),
        };

        Assert.Equal(0, nastaveni.HodinDenne);
    }
}
