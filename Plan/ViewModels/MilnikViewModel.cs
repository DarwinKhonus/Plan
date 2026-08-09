using Plan.Data.Entities;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>Jednodenní značka v řádku zakázky.</summary>
public class MilnikViewModel : ObservableObject
{
    private DateOnly _datum;
    private string _nazev;

    public MilnikViewModel(Milnik milnik)
    {
        Id = milnik.Id;
        _datum = milnik.Datum;
        _nazev = milnik.Nazev;
    }

    public int Id { get; }

    public DateOnly Datum
    {
        get => _datum;
        set => SetProperty(ref _datum, value);
    }

    public string Nazev
    {
        get => _nazev;
        set => SetProperty(ref _nazev, value);
    }
}
