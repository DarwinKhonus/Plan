namespace Plan.Data.Entities;

/// <summary>
/// Zakázka s naplánovaným termínem. Termín je v denní granularitě — <see cref="DatumOd"/>
/// i <see cref="DatumDo"/> jsou dny včetně, tedy jednodenní zakázka má obě data stejná.
/// </summary>
public class Zakazka
{
    public int Id { get; set; }

    public string Nazev { get; set; } = string.Empty;

    public DateOnly DatumOd { get; set; }

    /// <summary>Poslední den zakázky, včetně.</summary>
    public DateOnly DatumDo { get; set; }

    public DateTime VytvorenoUtc { get; set; }

    public DateTime UpravenoUtc { get; set; }
}
