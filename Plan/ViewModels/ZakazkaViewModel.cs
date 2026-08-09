using Plan.Data.Entities;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>Jedna zakázka tak, jak ji vidí tabulka i časová osa.</summary>
public class ZakazkaViewModel : ObservableObject
{
    private string _nazev = string.Empty;
    private DateOnly _datumOd;
    private DateOnly _datumDo;
    private double _odhadHodin;
    private bool _maKolizi;

    public ZakazkaViewModel(Zakazka zakazka)
    {
        Id = zakazka.Id;
        _nazev = zakazka.Nazev;
        _datumOd = zakazka.DatumOd;
        _datumDo = zakazka.DatumDo;
        VytvorenoUtc = zakazka.VytvorenoUtc;
        UpravenoUtc = zakazka.UpravenoUtc;
    }

    public int Id { get; }

    public DateTime VytvorenoUtc { get; }

    public DateTime UpravenoUtc { get; }

    public string Nazev
    {
        get => _nazev;
        set => SetProperty(ref _nazev, value);
    }

    public DateOnly DatumOd
    {
        get => _datumOd;
        set
        {
            if (SetProperty(ref _datumOd, value))
            {
                OnPropertyChanged(nameof(PocetDnu));
            }
        }
    }

    public DateOnly DatumDo
    {
        get => _datumDo;
        set
        {
            if (SetProperty(ref _datumDo, value))
            {
                OnPropertyChanged(nameof(PocetDnu));
            }
        }
    }

    /// <summary>Délka termínu ve dnech včetně obou krajních dnů.</summary>
    public int PocetDnu => _datumDo.DayNumber - _datumOd.DayNumber + 1;

    /// <summary>Informativní odhad pracovních hodin, přepočítává <see cref="MainViewModel"/>.</summary>
    public double OdhadHodin
    {
        get => _odhadHodin;
        set => SetProperty(ref _odhadHodin, value);
    }

    public bool MaKolizi
    {
        get => _maKolizi;
        set
        {
            if (SetProperty(ref _maKolizi, value))
            {
                OnPropertyChanged(nameof(Stav));
            }
        }
    }

    public string Stav => _maKolizi ? "Konflikt" : "OK";

    public Zakazka ToEntity() => new()
    {
        Id = Id,
        Nazev = _nazev,
        DatumOd = _datumOd,
        DatumDo = _datumDo,
    };
}
