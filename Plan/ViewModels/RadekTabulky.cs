using System.ComponentModel;
using System.Globalization;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>
/// Jeden řádek tabulky. Buď zakázka, nebo její milník odsazený pod ní — tabulka tak
/// tvoří plochý strom se zachovaným zarovnáním sloupců, které by TreeView neuměl.
/// </summary>
public class RadekTabulky : ObservableObject, IDisposable
{
    private static readonly CultureInfo Kultura = CultureInfo.GetCultureInfo("cs-CZ");

    private readonly bool _jePosledniMilnik;

    /// <summary>Řádek zakázky.</summary>
    public RadekTabulky(ZakazkaViewModel zakazka, bool jeRozbalena)
    {
        Zakazka = zakazka;
        JeRozbalena = jeRozbalena;

        // Posun úseku tažením mění data i hodiny, řádek na to musí reagovat.
        Zakazka.PropertyChanged += NaZmenuZakazky;
    }

    /// <summary>Řádek milníku pod zakázkou.</summary>
    public RadekTabulky(ZakazkaViewModel zakazka, MilnikViewModel milnik, bool jePosledni)
    {
        Zakazka = zakazka;
        Milnik = milnik;
        _jePosledniMilnik = jePosledni;
    }

    public ZakazkaViewModel Zakazka { get; }

    public MilnikViewModel? Milnik { get; }

    public bool JeMilnik => Milnik is not null;

    /// <summary>Je strom milníků u této zakázky rozbalený? U řádku milníku bez významu.</summary>
    public bool JeRozbalena { get; }

    /// <summary>Zakázka bez milníků přepínač nepotřebuje.</summary>
    public bool LzeRozbalit => !JeMilnik && Zakazka.Milniky.Count > 0;

    public string PopisPrepinace => JeRozbalena
        ? "Sbalit milníky"
        : $"Rozbalit milníky ({Zakazka.Milniky.Count})";

    /// <summary>Naznačení stromu: poslední milník uzavírá větev.</summary>
    public string Spojnice => JeMilnik ? (_jePosledniMilnik ? "└─" : "├─") : string.Empty;

    public string Nazev => Milnik?.Nazev ?? Zakazka.Nazev;

    public string TerminOd => Milnik is { } m
        ? m.Datum.ToString("dd.MM.yyyy", Kultura)
        : Zakazka.DatumOd.ToString("dd.MM.yyyy", Kultura);

    /// <summary>Milník je jednodenní, druhé datum u něj nemá co zobrazit.</summary>
    public string TerminDo => JeMilnik
        ? string.Empty
        : Zakazka.DatumDo.ToString("dd.MM.yyyy", Kultura);

    public string Dnu => JeMilnik ? string.Empty : Zakazka.PocetDnu.ToString(Kultura);

    public string Useku => JeMilnik ? string.Empty : Zakazka.PocetUseku.ToString(Kultura);

    public string PracovnichDnu => JeMilnik
        ? string.Empty
        : Zakazka.PocetPracovnichDnu.ToString(Kultura);

    public string Hodiny => JeMilnik
        ? string.Empty
        : string.Format(Kultura, "{0:0.#} h", Zakazka.OdhadHodin);

    /// <summary>Stav se ukazuje jen u zakázky; milník do kolizí nevstupuje.</summary>
    public bool ZobrazitStav => !JeMilnik;

    public bool MaKolizi => !JeMilnik && Zakazka.MaKolizi;

    public string Stav => Zakazka.Stav;

    public void Dispose()
    {
        if (!JeMilnik)
        {
            Zakazka.PropertyChanged -= NaZmenuZakazky;
        }
    }

    private void NaZmenuZakazky(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ZakazkaViewModel.DatumOd):
            case nameof(ZakazkaViewModel.DatumDo):
            case nameof(ZakazkaViewModel.PocetDnu):
                OnPropertyChanged(nameof(TerminOd));
                OnPropertyChanged(nameof(TerminDo));
                OnPropertyChanged(nameof(Dnu));
                break;

            case nameof(ZakazkaViewModel.OdhadHodin):
                OnPropertyChanged(nameof(Hodiny));
                break;

            case nameof(ZakazkaViewModel.PocetPracovnichDnu):
                OnPropertyChanged(nameof(PracovnichDnu));
                break;

            case nameof(ZakazkaViewModel.MaKolizi):
                OnPropertyChanged(nameof(MaKolizi));
                OnPropertyChanged(nameof(Stav));
                break;
        }
    }
}
