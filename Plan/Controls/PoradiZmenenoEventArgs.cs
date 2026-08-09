using Plan.ViewModels;

namespace Plan.Controls;

/// <summary>Zakázka se má během tažení přesunout na jinou pozici v seznamu.</summary>
public class PoradiZmenenoEventArgs : EventArgs
{
    public PoradiZmenenoEventArgs(ZakazkaViewModel zakazka, int cilovyIndex)
    {
        Zakazka = zakazka;
        CilovyIndex = cilovyIndex;
    }

    public ZakazkaViewModel Zakazka { get; }

    public int CilovyIndex { get; }
}
