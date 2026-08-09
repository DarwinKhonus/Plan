using Plan.ViewModels;

namespace Plan.Controls;

/// <summary>Nese výsledek dokončeného tažení úseku na časové ose.</summary>
public class UsekZmenenEventArgs : EventArgs
{
    public UsekZmenenEventArgs(ZakazkaViewModel zakazka, UsekViewModel usek)
    {
        Zakazka = zakazka;
        Usek = usek;
    }

    public ZakazkaViewModel Zakazka { get; }

    public UsekViewModel Usek { get; }
}
