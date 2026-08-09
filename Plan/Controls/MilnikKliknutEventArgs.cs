using Plan.ViewModels;

namespace Plan.Controls;

/// <summary>Nese milník, na který uživatel klikl, i jeho zakázku.</summary>
public class MilnikKliknutEventArgs : EventArgs
{
    public MilnikKliknutEventArgs(ZakazkaViewModel zakazka, MilnikViewModel milnik)
    {
        Zakazka = zakazka;
        Milnik = milnik;
    }

    public ZakazkaViewModel Zakazka { get; }

    public MilnikViewModel Milnik { get; }
}
