using Plan.Data.Domain;

namespace Plan.ViewModels;

/// <summary>Položka nabídky pro zobrazení nepracovních dnů v pruhu zakázky.</summary>
public record MoznostZobrazeni(ZobrazeniNepracovnichDnu Hodnota, string Popis)
{
    public override string ToString() => Popis;
}
