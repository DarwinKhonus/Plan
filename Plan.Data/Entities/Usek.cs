namespace Plan.Data.Entities;

/// <summary>
/// Souvislý časový úsek zakázky. Zakázka jich má aspoň jeden; víc úseků znamená,
/// že se na ní mezi nimi nepracuje (pauza).
/// </summary>
public class Usek
{
    public int Id { get; set; }

    public int ZakazkaId { get; set; }

    public Zakazka? Zakazka { get; set; }

    public DateOnly DatumOd { get; set; }

    /// <summary>Poslední den úseku, včetně.</summary>
    public DateOnly DatumDo { get; set; }
}
