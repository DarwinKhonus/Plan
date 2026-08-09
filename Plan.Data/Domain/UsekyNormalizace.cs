namespace Plan.Data.Domain;

/// <summary>Rozsah dnů, uzavřený na obou koncích.</summary>
public readonly record struct Rozsah(DateOnly Od, DateOnly Do)
{
    public bool Obsahuje(DateOnly den) => den >= Od && den <= Do;

    public int PocetDnu => Do.DayNumber - Od.DayNumber + 1;
}

public static class UsekyNormalizace
{
    /// <summary>
    /// Seřadí úseky a slije ty, které se překrývají. Úseky jedné zakázky se překrývat
    /// nemají — po přetažení úseku na sousední z toho jinak vznikne dvojitý pruh
    /// a dvakrát započtené hodiny.
    /// </summary>
    /// <remarks>
    /// Pouze dotýkající se úseky (konec 10. 3., začátek 11. 3.) se záměrně neslévají —
    /// jinak by se rozdělení zakázky ihned samo vrátilo zpátky.
    /// </remarks>
    public static List<Rozsah> Normalizuj(IEnumerable<Rozsah> useky)
    {
        var serazene = useky.OrderBy(u => u.Od).ThenBy(u => u.Do).ToList();
        var vysledek = new List<Rozsah>();

        foreach (var usek in serazene)
        {
            if (vysledek.Count == 0)
            {
                vysledek.Add(usek);
                continue;
            }

            var posledni = vysledek[^1];

            if (usek.Od <= posledni.Do)
            {
                vysledek[^1] = posledni with { Do = usek.Do > posledni.Do ? usek.Do : posledni.Do };
            }
            else
            {
                vysledek.Add(usek);
            }
        }

        return vysledek;
    }

    /// <summary>Všechny dny pokryté úseky, každý právě jednou.</summary>
    public static HashSet<DateOnly> PokryteDny(IEnumerable<Rozsah> useky)
    {
        var dny = new HashSet<DateOnly>();

        foreach (var usek in useky)
        {
            for (var den = usek.Od; den <= usek.Do; den = den.AddDays(1))
            {
                dny.Add(den);
            }
        }

        return dny;
    }
}
