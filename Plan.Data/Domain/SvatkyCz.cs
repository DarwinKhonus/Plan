namespace Plan.Data.Domain;

/// <summary>
/// České státní svátky a dny pracovního klidu (zákon č. 245/2000 Sb.).
/// Počítají se pro libovolný rok, takže aplikace nepotřebuje udržovanou tabulku dat.
/// </summary>
public static class SvatkyCz
{
    /// <summary>Vrátí všechny svátky daného roku s názvem, seřazené podle data.</summary>
    public static IReadOnlyList<(DateOnly Datum, string Nazev)> ProRok(int rok)
    {
        var velikonocniNedele = VelikonocniNedele(rok);

        var svatky = new List<(DateOnly, string)>
        {
            (new DateOnly(rok, 1, 1), "Den obnovy samostatného českého státu"),
            (velikonocniNedele.AddDays(-2), "Velký pátek"),
            (velikonocniNedele.AddDays(1), "Velikonoční pondělí"),
            (new DateOnly(rok, 5, 1), "Svátek práce"),
            (new DateOnly(rok, 5, 8), "Den vítězství"),
            (new DateOnly(rok, 7, 5), "Den slovanských věrozvěstů Cyrila a Metoděje"),
            (new DateOnly(rok, 7, 6), "Den upálení mistra Jana Husa"),
            (new DateOnly(rok, 9, 28), "Den české státnosti"),
            (new DateOnly(rok, 10, 28), "Den vzniku samostatného československého státu"),
            (new DateOnly(rok, 11, 17), "Den boje za svobodu a demokracii"),
            (new DateOnly(rok, 12, 24), "Štědrý den"),
            (new DateOnly(rok, 12, 25), "1. svátek vánoční"),
            (new DateOnly(rok, 12, 26), "2. svátek vánoční"),
        };

        svatky.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return svatky;
    }

    /// <summary>Množina dat svátků pro rychlé dotazy napříč zadaným rozsahem let.</summary>
    public static HashSet<DateOnly> ProRozsahLet(int rokOd, int rokDo)
    {
        var vysledek = new HashSet<DateOnly>();
        for (var rok = rokOd; rok <= rokDo; rok++)
        {
            foreach (var (datum, _) in ProRok(rok))
            {
                vysledek.Add(datum);
            }
        }

        return vysledek;
    }

    public static bool JeSvatek(DateOnly datum) =>
        ProRok(datum.Year).Any(s => s.Datum == datum);

    /// <summary>Název svátku, nebo <c>null</c> pokud daný den svátek není.</summary>
    public static string? NazevSvatku(DateOnly datum)
    {
        foreach (var (d, nazev) in ProRok(datum.Year))
        {
            if (d == datum)
            {
                return nazev;
            }
        }

        return null;
    }

    /// <summary>
    /// Velikonoční neděle podle gregoriánského kalendáře — anonymní gregoriánský
    /// algoritmus (Meeus/Jones/Butcher). Od ní se odvozuje Velký pátek a Velikonoční pondělí.
    /// </summary>
    public static DateOnly VelikonocniNedele(int rok)
    {
        var a = rok % 19;
        var b = rok / 100;
        var c = rok % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mesic = (h + l - (7 * m) + 114) / 31;
        var den = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(rok, mesic, den);
    }
}
