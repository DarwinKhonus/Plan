namespace Plan.Data.Domain;

/// <summary>
/// Globální nastavení pracovní doby. Jedna sada pro celou aplikaci, ne per zakázka.
/// </summary>
public class PracovniNastaveni
{
    /// <summary>Dny, kdy se pracuje. Výchozí Po–Pá.</summary>
    public HashSet<DayOfWeek> PracovniDny { get; set; } = new()
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    };

    public TimeOnly ZacatekPrace { get; set; } = new(8, 0);

    public TimeOnly KonecPrace { get; set; } = new(16, 30);

    /// <summary>Považovat české státní svátky za nepracovní dny.</summary>
    public bool ZohlednitSvatky { get; set; } = true;

    /// <summary>
    /// Řadit zakázky automaticky podle termínu. Když je vypnuté, drží se ruční pořadí
    /// a zakázky lze v ose přetahovat mezi sebou.
    /// </summary>
    /// <remarks>
    /// S pracovní dobou to nesouvisí, ale jde o nastavení aplikace a sdílí s ní dialog
    /// i tabulku v databázi. <see cref="PracovniKalendar"/> tuto vlastnost ignoruje.
    /// </remarks>
    public bool AutomatickeRazeni { get; set; } = true;

    /// <summary>
    /// Šrafovat nepracovní dny uvnitř pruhu zakázky. Když je vypnuté, výplň se v těch
    /// dnech vynechá a zůstane jen obrys („skořápka“).
    /// </summary>
    public bool SrafovatNepracovniDny { get; set; } = true;

    /// <summary>
    /// Počet hodin na jeden pracovní den, odvozený z rozsahu pracovní doby.
    /// Přes půlnoc se needituje — konec dřív než začátek dává 0.
    /// </summary>
    public double HodinDenne
    {
        get
        {
            var rozsah = KonecPrace.ToTimeSpan() - ZacatekPrace.ToTimeSpan();
            return rozsah > TimeSpan.Zero ? rozsah.TotalHours : 0;
        }
    }

    public PracovniNastaveni Kopie() => new()
    {
        PracovniDny = new HashSet<DayOfWeek>(PracovniDny),
        ZacatekPrace = ZacatekPrace,
        KonecPrace = KonecPrace,
        ZohlednitSvatky = ZohlednitSvatky,
        AutomatickeRazeni = AutomatickeRazeni,
        SrafovatNepracovniDny = SrafovatNepracovniDny,
    };
}
