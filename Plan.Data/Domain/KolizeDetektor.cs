using Plan.Data.Entities;

namespace Plan.Data.Domain;

/// <summary>
/// Hledá překryvy termínů zakázek.
/// </summary>
public static class KolizeDetektor
{
    /// <summary>
    /// Překrývají se termíny? Intervaly jsou uzavřené na obou koncích, takže dotyk
    /// konec-na-začátek (A končí 10. 3., B začíná 10. 3.) je překryv — oba dny padnou
    /// na stejný den.
    /// </summary>
    public static bool SePrekryvaji(Zakazka a, Zakazka b) =>
        SePrekryvaji(a.DatumOd, a.DatumDo, b.DatumOd, b.DatumDo);

    public static bool SePrekryvaji(DateOnly aOd, DateOnly aDo, DateOnly bOd, DateOnly bDo) =>
        aOd <= bDo && aDo >= bOd;

    /// <summary>
    /// Kolidují zakázky se zohledněním pracovních dnů? Překryv jen přes víkend nebo
    /// svátek kolize není — uživatel v ten den stejně na žádné z nich nedělá.
    /// Bez kalendáře se chová jako <see cref="SePrekryvaji(Zakazka, Zakazka)"/>.
    /// </summary>
    public static bool Koliduji(Zakazka a, Zakazka b, PracovniKalendar? kalendar)
    {
        if (!SePrekryvaji(a, b))
        {
            return false;
        }

        if (kalendar is null)
        {
            return true;
        }

        var zacatekPrekryvu = a.DatumOd > b.DatumOd ? a.DatumOd : b.DatumOd;
        var konecPrekryvu = a.DatumDo < b.DatumDo ? a.DatumDo : b.DatumDo;

        return kalendar.ObsahujePracovniDen(zacatekPrekryvu, konecPrekryvu);
    }

    /// <summary>
    /// Id zakázek, které kolidují alespoň s jednou jinou. Zakázka sama se sebou nekoliduje.
    /// Když je předaný kalendář, započítají se jen překryvy v pracovních dnech.
    /// </summary>
    public static HashSet<int> NajdiKolidujici(
        IEnumerable<Zakazka> zakazky, PracovniKalendar? kalendar = null)
    {
        // Řazení podle začátku umožní vnitřní smyčku ukončit, jakmile další zakázka
        // začíná po konci té aktuální — bez toho by to bylo vždy O(n²).
        var serazene = zakazky.OrderBy(z => z.DatumOd).ThenBy(z => z.DatumDo).ToList();
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
