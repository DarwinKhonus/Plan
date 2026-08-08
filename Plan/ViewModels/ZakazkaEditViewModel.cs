using System.ComponentModel;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>
/// Podklad pro dialog přidání/úpravy zakázky. Data drží jako <see cref="DateTime"/>,
/// protože na ně přímo váže WPF <c>DatePicker</c>; ven je vydává jako <see cref="DateOnly"/>.
/// </summary>
public class ZakazkaEditViewModel : ObservableObject, IDataErrorInfo
{
    private string _nazev = string.Empty;
    private DateTime? _datumOd;
    private DateTime? _datumDo;

    public ZakazkaEditViewModel()
    {
        var dnes = DateTime.Today;
        _datumOd = dnes;
        _datumDo = dnes;
        Titulek = "Nová zakázka";
    }

    public ZakazkaEditViewModel(ZakazkaViewModel zakazka)
    {
        Id = zakazka.Id;
        _nazev = zakazka.Nazev;
        _datumOd = zakazka.DatumOd.ToDateTime(TimeOnly.MinValue);
        _datumDo = zakazka.DatumDo.ToDateTime(TimeOnly.MinValue);
        Titulek = "Úprava zakázky";
    }

    public int? Id { get; }

    public string Titulek { get; }

    public string Nazev
    {
        get => _nazev;
        set
        {
            if (SetProperty(ref _nazev, value))
            {
                OnPropertyChanged(nameof(JePlatny));
                OnPropertyChanged(nameof(ChybovaZprava));
            }
        }
    }

    public DateTime? DatumOd
    {
        get => _datumOd;
        set
        {
            if (SetProperty(ref _datumOd, value))
            {
                OnPropertyChanged(nameof(DatumDo));
                OnPropertyChanged(nameof(JePlatny));
                OnPropertyChanged(nameof(ChybovaZprava));
            }
        }
    }

    public DateTime? DatumDo
    {
        get => _datumDo;
        set
        {
            if (SetProperty(ref _datumDo, value))
            {
                OnPropertyChanged(nameof(JePlatny));
                OnPropertyChanged(nameof(ChybovaZprava));
            }
        }
    }

    public bool JePlatny => ChybovaZprava is null;

    /// <summary>První chyba bránící uložení, nebo <c>null</c> když je formulář v pořádku.</summary>
    public string? ChybovaZprava
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_nazev))
            {
                return "Zadejte název zakázky.";
            }

            if (!_datumOd.HasValue || !_datumDo.HasValue)
            {
                return "Vyplňte oba termíny.";
            }

            if (_datumDo.Value.Date < _datumOd.Value.Date)
            {
                return "Termín do nesmí být dřív než termín od.";
            }

            return null;
        }
    }

    public DateOnly VyslednyDatumOd => DateOnly.FromDateTime(_datumOd ?? DateTime.Today);

    public DateOnly VyslednyDatumDo => DateOnly.FromDateTime(_datumDo ?? DateTime.Today);

    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(Nazev) when string.IsNullOrWhiteSpace(_nazev) => "Zadejte název zakázky.",
        nameof(DatumOd) when !_datumOd.HasValue => "Zadejte termín od.",
        nameof(DatumDo) when !_datumDo.HasValue => "Zadejte termín do.",
        nameof(DatumDo) when _datumOd.HasValue && _datumDo!.Value.Date < _datumOd.Value.Date =>
            "Termín do nesmí být dřív než termín od.",
        _ => string.Empty,
    };
}
