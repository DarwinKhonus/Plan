using System.ComponentModel;
using System.Globalization;
using Plan.Data.Domain;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>
/// Podklad pro dialog nastavení pracovní doby. WPF nemá vestavěný TimePicker, takže
/// časy jsou textové a validují se přes <see cref="IDataErrorInfo"/>.
/// </summary>
public class NastaveniViewModel : ObservableObject, IDataErrorInfo
{
    private bool _pondeli;
    private bool _utery;
    private bool _streda;
    private bool _ctvrtek;
    private bool _patek;
    private bool _sobota;
    private bool _nedele;
    private string _zacatekPrace;
    private string _konecPrace;
    private bool _zohlednitSvatky;
    private bool _automatickeRazeni;
    private MoznostZobrazeni _zobrazeniNepracovnichDnu;

    public NastaveniViewModel(PracovniNastaveni nastaveni)
    {
        _pondeli = nastaveni.PracovniDny.Contains(DayOfWeek.Monday);
        _utery = nastaveni.PracovniDny.Contains(DayOfWeek.Tuesday);
        _streda = nastaveni.PracovniDny.Contains(DayOfWeek.Wednesday);
        _ctvrtek = nastaveni.PracovniDny.Contains(DayOfWeek.Thursday);
        _patek = nastaveni.PracovniDny.Contains(DayOfWeek.Friday);
        _sobota = nastaveni.PracovniDny.Contains(DayOfWeek.Saturday);
        _nedele = nastaveni.PracovniDny.Contains(DayOfWeek.Sunday);
        _zacatekPrace = nastaveni.ZacatekPrace.ToString("HH:mm", CultureInfo.InvariantCulture);
        _konecPrace = nastaveni.KonecPrace.ToString("HH:mm", CultureInfo.InvariantCulture);
        _zohlednitSvatky = nastaveni.ZohlednitSvatky;
        _automatickeRazeni = nastaveni.AutomatickeRazeni;
        _zobrazeniNepracovnichDnu =
            MoznostiZobrazeni.First(m => m.Hodnota == nastaveni.ZobrazeniNepracovnichDnu);
    }

    /// <summary>
    /// Nabídka pro combo box v nastavení. Výčtový typ je kvalifikovaný, protože stejnojmenná
    /// vlastnost níže by ho jinak zastínila.
    /// </summary>
    public IReadOnlyList<MoznostZobrazeni> MoznostiZobrazeni { get; } =
    [
        new(Data.Domain.ZobrazeniNepracovnichDnu.Cara, "Spojovací čára"),
        new(Data.Domain.ZobrazeniNepracovnichDnu.Srafa, "Bílé pruhy"),
        new(Data.Domain.ZobrazeniNepracovnichDnu.Obrys, "Jen obrys"),
    ];

    public bool Pondeli { get => _pondeli; set => NastavDen(ref _pondeli, value); }

    public bool Utery { get => _utery; set => NastavDen(ref _utery, value); }

    public bool Streda { get => _streda; set => NastavDen(ref _streda, value); }

    public bool Ctvrtek { get => _ctvrtek; set => NastavDen(ref _ctvrtek, value); }

    public bool Patek { get => _patek; set => NastavDen(ref _patek, value); }

    public bool Sobota { get => _sobota; set => NastavDen(ref _sobota, value); }

    public bool Nedele { get => _nedele; set => NastavDen(ref _nedele, value); }

    public string ZacatekPrace
    {
        get => _zacatekPrace;
        set
        {
            if (SetProperty(ref _zacatekPrace, value))
            {
                OnPropertyChanged(nameof(KonecPrace));
                OnPropertyChanged(nameof(HodinDenneText));
                OnPropertyChanged(nameof(JePlatny));
            }
        }
    }

    public string KonecPrace
    {
        get => _konecPrace;
        set
        {
            if (SetProperty(ref _konecPrace, value))
            {
                OnPropertyChanged(nameof(HodinDenneText));
                OnPropertyChanged(nameof(JePlatny));
            }
        }
    }

    public bool ZohlednitSvatky
    {
        get => _zohlednitSvatky;
        set => SetProperty(ref _zohlednitSvatky, value);
    }

    public bool AutomatickeRazeni
    {
        get => _automatickeRazeni;
        set => SetProperty(ref _automatickeRazeni, value);
    }

    public MoznostZobrazeni ZobrazeniNepracovnichDnu
    {
        get => _zobrazeniNepracovnichDnu;
        set
        {
            if (SetProperty(ref _zobrazeniNepracovnichDnu, value))
            {
                OnPropertyChanged(nameof(PopisZobrazeni));
            }
        }
    }

    /// <summary>Vysvětlení k právě vybrané možnosti.</summary>
    public string PopisZobrazeni => _zobrazeniNepracovnichDnu.Hodnota switch
    {
        Data.Domain.ZobrazeniNepracovnichDnu.Cara =>
            "Pruh se přeruší a přes víkendy a svátky vede spojovací čára. "
            + "Vypadá to stejně jako zakázka rozdělená na úseky.",

        Data.Domain.ZobrazeniNepracovnichDnu.Srafa =>
            "Pruh drží plnou barvu a přes víkendy a svátky jdou bílé pruhy.",

        _ => "Výplň se ve víkendech a svátcích vynechá a zůstane jen obrys pruhu.",
    };

    public string HodinDenneText
    {
        get
        {
            if (!ZkusParsovatCasy(out var zacatek, out var konec) || konec <= zacatek)
            {
                return "—";
            }

            var hodin = (konec.ToTimeSpan() - zacatek.ToTimeSpan()).TotalHours;
            return string.Format(CultureInfo.CurrentCulture, "{0:0.##} h / den", hodin);
        }
    }

    public bool JePlatny =>
        VybraneDny().Count > 0
        && ZkusParsovatCasy(out var zacatek, out var konec)
        && konec > zacatek;

    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(ZacatekPrace) when !TimeOnly.TryParse(_zacatekPrace, CultureInfo.InvariantCulture, out _) =>
            "Zadejte čas ve tvaru HH:mm.",
        nameof(KonecPrace) when !TimeOnly.TryParse(_konecPrace, CultureInfo.InvariantCulture, out _) =>
            "Zadejte čas ve tvaru HH:mm.",
        nameof(KonecPrace) when ZkusParsovatCasy(out var z, out var k) && k <= z =>
            "Konec práce musí být později než začátek.",
        _ => string.Empty,
    };

    public PracovniNastaveni ToNastaveni()
    {
        ZkusParsovatCasy(out var zacatek, out var konec);

        return new PracovniNastaveni
        {
            PracovniDny = VybraneDny(),
            ZacatekPrace = zacatek,
            KonecPrace = konec,
            ZohlednitSvatky = _zohlednitSvatky,
            AutomatickeRazeni = _automatickeRazeni,
            ZobrazeniNepracovnichDnu = _zobrazeniNepracovnichDnu.Hodnota,
        };
    }

    private HashSet<DayOfWeek> VybraneDny()
    {
        var dny = new HashSet<DayOfWeek>();
        if (_pondeli) dny.Add(DayOfWeek.Monday);
        if (_utery) dny.Add(DayOfWeek.Tuesday);
        if (_streda) dny.Add(DayOfWeek.Wednesday);
        if (_ctvrtek) dny.Add(DayOfWeek.Thursday);
        if (_patek) dny.Add(DayOfWeek.Friday);
        if (_sobota) dny.Add(DayOfWeek.Saturday);
        if (_nedele) dny.Add(DayOfWeek.Sunday);
        return dny;
    }

    private bool ZkusParsovatCasy(out TimeOnly zacatek, out TimeOnly konec)
    {
        var okZacatek = TimeOnly.TryParse(_zacatekPrace, CultureInfo.InvariantCulture, out zacatek);
        var okKonec = TimeOnly.TryParse(_konecPrace, CultureInfo.InvariantCulture, out konec);
        return okZacatek && okKonec;
    }

    private void NastavDen(ref bool pole, bool hodnota, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref pole, hodnota, propertyName))
        {
            OnPropertyChanged(nameof(JePlatny));
        }
    }
}
