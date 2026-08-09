using Plan.Data.Entities;

namespace Plan.Data.Domain;

/// <summary>
/// Hledá překryvy termínů zakázek. Zakázka může mít víc úseků, takže se porovnává
/// každý úsek s každým — pauza uprostřed zakázky jinou zakázku neblokuje.
/// </summary>
public static class KolizeDetektor
{
    /// <summary>
    /// Překrývají se rozsahy? Intervaly jsou uzavřené na obou koncích, takže dotyk
    /// konec-na-začátek (A končí 10. 3., B začíná 10. 3.) je překryv — oba dny padnou
    /// na stejný den.
    /// </summary>
    public static bool SePrekryvaji(DateOnly aOd, DateOnly aDo, DateOnly bOd, DateOnly bDo) =>
        aOd <= bDo && aDo >= bOd;

    public static bool SePrekryvaji(Rozsah a, Rozsah b) =>
        SePrekryvaji(a.Od, a.Do, b.Od, b.Do);

    /// <summary>Překrývají se zakázky aspoň jedním úsekem, bez ohledu na pracovní dny?</summary>
    public static bool SePrekryvaji(Zakazka a, Zakazka b)
    {
        foreach (var usekA in a.Useky)
        {
            foreach (var usekB in b.Useky)
            {
                if (SePrekryvaji(usekA.DatumOd, usekA.DatumDo, usekB.DatumOd, usekB.DatumDo))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kolidují zakázky se zohledněním pracovních dnů? Překryv jen přes víkend nebo
    /// svátek kolize není — uživatel v ten den stejně na žádné z nich nedělá.
    /// Bez kalendáře stačí holý překryv úseků.
    /// </summary>
    public static bool Koliduji(Zakazka a, Zakazka b, PracovniKalendar? kalendar)
    {
        foreach (var usekA in a.Useky)
        {
            foreach (var usekB in b.Useky)
            {
                if (!SePrekryvaji(usekA.DatumOd, usekA.DatumDo, usekB.DatumOd, usekB.DatumDo))
                {
                    continue;
                }

                if (kalendar is null)
                {
                    return true;
                }

                var zacatek = usekA.DatumOd > usekB.DatumOd ? usekA.DatumOd : usekB.DatumOd;
                var konec = usekA.DatumDo < usekB.DatumDo ? usekA.DatumDo : usekB.DatumDo;

                if (kalendar.ObsahujePracovniDen(zacatek, konec))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Id zakázek, které kolidují alespoň s jednou jinou. Zakázka sama se sebou nekoliduje.
    /// Když je předaný kalendář, započítají se jen překryvy v pracovních dnech.
    /// </summary>
    public static HashSet<int> NajdiKolidujici(
        IEnumerable<Zakazka> zakazky, PracovniKalendar? kalendar = null)
    {
        // Řazení podle začátku umožní vnitřní smyčku ukončit, jakmile další zakázka
        // začíná po konci té aktuální. Krajní data jsou celkovým rozsahem přes všechny
        // úseky, takže je odhad shora a předčasné ukončení nic nepřeskočí.
        var serazene = zakazky
            .Where(z => z.Useky.Count > 0)
            .OrderBy(z => z.DatumOd)
            .ThenBy(z => z.DatumDo)
            .ToList();

        var kolidujici = new HashSet<int>();

        for (var i = 0; i < serazene.Count; i++)
        {
            var aktualni = serazene[i];

            for (var j = i + 1; j < serazene.Count; j++)
            {
                var dalsi = serazene[j];

                if (dalsi.DatumOd > aktualni.DatumDo)
                {
                    break;
                }

                if (!Koliduji(aktualni, dalsi, kalendar))
                {
                    continue;
                }

                kolidujici.Add(aktualni.Id);
                kolidujici.Add(dalsi.Id);
            }
        }

        return kolidujici;
    }
}
