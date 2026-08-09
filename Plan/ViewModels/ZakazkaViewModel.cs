using System.Collections.ObjectModel;
using System.ComponentModel;
using Plan.Data.Domain;
using Plan.Data.Entities;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>Jedna zakázka tak, jak ji vidí tabulka i časová osa.</summary>
public class ZakazkaViewModel : ObservableObject
{
    private string _nazev = string.Empty;
    private double _odhadHodin;
    private int _pocetPracovnichDnu;
    private bool _maKolizi;

    public ZakazkaViewModel(Zakazka zakazka)
    {
        Id = zakazka.Id;
        _nazev = zakazka.Nazev;
        VytvorenoUtc = zakazka.VytvorenoUtc;
        UpravenoUtc = zakazka.UpravenoUtc;

        foreach (var usek in zakazka.Useky.OrderBy(u => u.DatumOd))
        {
            var vm = new UsekViewModel(usek);
            vm.PropertyChanged += NaZmenuUseku;
            Useky.Add(vm);
        }

        foreach (var milnik in zakazka.Milniky.OrderBy(m => m.Datum))
        {
            Milniky.Add(new MilnikViewModel(milnik));
        }
    }

    public int Id { get; }

    public DateTime VytvorenoUtc { get; }

    public DateTime UpravenoUtc { get; }

    public ObservableCollection<UsekViewModel> Useky { get; } = [];

    public ObservableCollection<MilnikViewModel> Milniky { get; } = [];

    public string Nazev
    {
        get => _nazev;
        set => SetProperty(ref _nazev, value);
    }

    /// <summary>První den zakázky napříč úseky.</summary>
    public DateOnly DatumOd => Useky.Count > 0
        ? Useky.Min(u => u.DatumOd)
        : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Poslední den zakázky napříč úseky, včetně.</summary>
    public DateOnly DatumDo => Useky.Count > 0
        ? Useky.Max(u => u.DatumDo)
        : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Délka celkového rozsahu včetně případných pauz mezi úseky.</summary>
    public int PocetDnu => DatumDo.DayNumber - DatumOd.DayNumber + 1;

    public int PocetUseku => Useky.Count;

    public bool JeRozdelena => Useky.Count > 1;

    /// <summary>Pracovní dny napříč úseky, přepočítává <see cref="MainViewModel"/>.</summary>
    public int PocetPracovnichDnu
    {
        get => _pocetPracovnichDnu;
        set => SetProperty(ref _pocetPracovnichDnu, value);
    }

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

    public IEnumerable<Rozsah> Rozsahy => Useky.Select(u => u.ToRozsah());

    public Zakazka ToEntity() => new()
    {
        Id = Id,
        Nazev = _nazev,
        Useky = [.. Useky.Select(u => new Usek
        {
            Id = u.Id,
            ZakazkaId = Id,
            DatumOd = u.DatumOd,
            DatumDo = u.DatumDo,
        })],
    };

    /// <summary>Úsek, do kterého spadá zadaný den, nebo <c>null</c>.</summary>
    public UsekViewModel? UsekVDni(DateOnly den) =>
        Useky.FirstOrDefault(u => den >= u.DatumOd && den <= u.DatumDo);

    private void NaZmenuUseku(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(UsekViewModel.DatumOd) or nameof(UsekViewModel.DatumDo)))
        {
            return;
        }

        // Posun úseku mění i celkový rozsah zakázky, na kterém stojí řazení a šířka osy.
        OnPropertyChanged(nameof(DatumOd));
        OnPropertyChanged(nameof(DatumDo));
        OnPropertyChanged(nameof(PocetDnu));
    }
}
