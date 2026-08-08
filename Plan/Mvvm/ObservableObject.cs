using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Plan.Mvvm;

/// <summary>
/// Minimální základ pro ViewModely. Vlastní implementace místo externí MVVM knihovny —
/// pro rozsah téhle aplikace je to pár řádků a nulová závislost navíc.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T pole, T hodnota, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(pole, hodnota))
        {
            return false;
        }

        pole = hodnota;
        OnPropertyChanged(propertyName);
        return true;
    }
}
