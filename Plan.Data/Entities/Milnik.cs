namespace Plan.Data.Entities;

/// <summary>
/// Významný jednodenní bod v termínu zakázky — třeba předání nebo dodání materiálu.
/// Do odhadu hodin ani do detekce kolizí nevstupuje, je to jen značka v ose.
/// </summary>
public class Milnik
{
    public int Id { get; set; }

    public int ZakazkaId { get; set; }

    public Zakazka? Zakazka { get; set; }

    public DateOnly Datum { get; set; }

    public string Nazev { get; set; } = string.Empty;
}
