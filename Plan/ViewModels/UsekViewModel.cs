using Plan.Data.Domain;
using Plan.Data.Entities;
using Plan.Mvvm;

namespace Plan.ViewModels;

/// <summary>Jeden souvislý úsek zakázky — právě ten se na ose chytá myší.</summary>
public class UsekViewModel : ObservableObject
{
    private DateOnly _datumOd;
    private DateOnly _datumDo;

    public UsekViewModel(Usek usek)
    {
        Id = usek.Id;
        _datumOd = usek.DatumOd;
        _datumDo = usek.DatumDo;
    }

    public int Id { get; }

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

    public int PocetDnu => _datumDo.DayNumber - _datumOd.DayNumber + 1;

    public Rozsah ToRozsah() => new(_datumOd, _datumDo);
}
