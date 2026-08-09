namespace Plan.Data.Entities;

/// <summary>
/// Zakázka s naplánovaným termínem. Termín tvoří jeden nebo více <see cref="Useky"/> —
/// víc úseků znamená, že se mezi nimi na zakázce nepracuje.
/// </summary>
public class Zakazka
{
    public int Id { get; set; }

    public string Nazev { get; set; } = string.Empty;

    public List<Usek> Useky { get; set; } = [];

    public List<Milnik> Milniky { get; set; } = [];

    public DateTime VytvorenoUtc { get; set; }

    public DateTime UpravenoUtc { get; set; }

    /// <summary>První den zakázky napříč všemi úseky.</summary>
    public DateOnly DatumOd => Useky.Count > 0
        ? Useky.Min(u => u.DatumOd)
        : DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Poslední den zakázky napříč všemi úseky, včetně.</summary>
    public DateOnly DatumDo => Useky.Count > 0
        ? Useky.Max(u => u.DatumDo)
        : DateOnly.FromDateTime(DateTime.Today);
}
