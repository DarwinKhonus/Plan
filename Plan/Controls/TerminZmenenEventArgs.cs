using Plan.ViewModels;

namespace Plan.Controls;

/// <summary>Nese výsledek dokončeného tažení pruhu na časové ose.</summary>
public class TerminZmenenEventArgs : EventArgs
{
    public TerminZmenenEventArgs(ZakazkaViewModel zakazka, DateOnly datumOd, DateOnly datumDo)
    {
        Zakazka = zakazka;
        DatumOd = datumOd;
        DatumDo = datumDo;
    }

    public ZakazkaViewModel Zakazka { get; }

    public DateOnly DatumOd { get; }

    public DateOnly DatumDo { get; }
}
