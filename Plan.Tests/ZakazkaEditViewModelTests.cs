using Plan.ViewModels;

namespace Plan.Tests;

public class ZakazkaEditViewModelTests
{
    private static ZakazkaEditViewModel Formular(string nazev, string od, string doVcetne) => new()
    {
        Nazev = nazev,
        DatumOd = DateTime.Parse(od),
        DatumDo = DateTime.Parse(doVcetne),
    };

    [Fact]
    public void Vyplneny_formular_je_platny()
    {
        var formular = Formular("Kuchyně Novák", "2026-03-01", "2026-03-10");

        Assert.True(formular.JePlatny);
        Assert.Null(formular.ChybovaZprava);
    }

    [Fact]
    public void Prazdny_nazev_neni_platny()
    {
        var formular = Formular("   ", "2026-03-01", "2026-03-10");

        Assert.False(formular.JePlatny);
        Assert.Equal("Zadejte název zakázky.", formular.ChybovaZprava);
    }

    [Fact]
    public void Termin_do_pred_terminem_od_neni_platny()
    {
        var formular = Formular("Kuchyně Novák", "2026-03-10", "2026-03-01");

        Assert.False(formular.JePlatny);
        Assert.Equal("Termín do nesmí být dřív než termín od.", formular.ChybovaZprava);
    }

    [Fact]
    public void Stejne_datum_od_i_do_je_platne()
    {
        var formular = Formular("Zaměření", "2026-03-05", "2026-03-05");

        Assert.True(formular.JePlatny);
    }

    [Fact]
    public void Chybejici_datum_neni_platne()
    {
        var formular = new ZakazkaEditViewModel { Nazev = "Kuchyně", DatumDo = null };

        Assert.False(formular.JePlatny);
    }

    [Fact]
    public void Validace_pres_IDataErrorInfo_hlasi_spatne_poradi_terminu()
    {
        var formular = Formular("Kuchyně Novák", "2026-03-10", "2026-03-01");

        Assert.Equal("Termín do nesmí být dřív než termín od.", formular[nameof(ZakazkaEditViewModel.DatumDo)]);
        Assert.Equal(string.Empty, formular[nameof(ZakazkaEditViewModel.Nazev)]);
    }

    [Fact]
    public void Vysledna_data_se_prevadeji_na_DateOnly()
    {
        var formular = Formular("Kuchyně Novák", "2026-03-01", "2026-03-10");

        Assert.Equal(new DateOnly(2026, 3, 1), formular.VyslednyDatumOd);
        Assert.Equal(new DateOnly(2026, 3, 10), formular.VyslednyDatumDo);
    }
}
