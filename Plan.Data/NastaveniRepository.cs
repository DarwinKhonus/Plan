using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Plan.Data.Domain;
using Plan.Data.Entities;

namespace Plan.Data;

/// <summary>
/// Čte a zapisuje globální nastavení pracovní doby do key-value tabulky <c>Nastaveni</c>.
/// Chybějící nebo poškozený klíč se tiše nahradí výchozí hodnotou — nastavení nikdy
/// nesmí být důvod, proč aplikace nenaběhne.
/// </summary>
public class NastaveniRepository
{
    private const string KlicPracovniDny = "PracovniDny";
    private const string KlicZacatekPrace = "ZacatekPrace";
    private const string KlicKonecPrace = "KonecPrace";
    private const string KlicZohlednitSvatky = "ZohlednitSvatky";
    private const string KlicAutomatickeRazeni = "AutomatickeRazeni";

    private readonly PlanDbFactory _factory;

    public NastaveniRepository(PlanDbFactory factory)
    {
        _factory = factory;
    }

    public async Task<PracovniNastaveni> NactiAsync()
    {
        await using var db = _factory.Create();
        var zaznamy = await db.Nastaveni.AsNoTracking()
            .ToDictionaryAsync(n => n.Klic, n => n.Hodnota);

        var nastaveni = new PracovniNastaveni();

        if (zaznamy.TryGetValue(KlicPracovniDny, out var dny))
        {
            var parsovane = ParsujDny(dny);
            if (parsovane.Count > 0)
            {
                nastaveni.PracovniDny = parsovane;
            }
        }

        if (zaznamy.TryGetValue(KlicZacatekPrace, out var zacatek)
            && TimeOnly.TryParse(zacatek, CultureInfo.InvariantCulture, out var zacatekParsed))
        {
            nastaveni.ZacatekPrace = zacatekParsed;
        }

        if (zaznamy.TryGetValue(KlicKonecPrace, out var konec)
            && TimeOnly.TryParse(konec, CultureInfo.InvariantCulture, out var konecParsed))
        {
            nastaveni.KonecPrace = konecParsed;
        }

        if (zaznamy.TryGetValue(KlicZohlednitSvatky, out var svatky)
            && bool.TryParse(svatky, out var svatkyParsed))
        {
            nastaveni.ZohlednitSvatky = svatkyParsed;
        }

        if (zaznamy.TryGetValue(KlicAutomatickeRazeni, out var razeni)
            && bool.TryParse(razeni, out var razeniParsed))
        {
            nastaveni.AutomatickeRazeni = razeniParsed;
        }

        return nastaveni;
    }

    public async Task UlozAsync(PracovniNastaveni nastaveni)
    {
        await using var db = _factory.Create();

        await NastavAsync(db, KlicPracovniDny,
            string.Join(",", nastaveni.PracovniDny.Select(d => ((int)d).ToString(CultureInfo.InvariantCulture))));
        await NastavAsync(db, KlicZacatekPrace,
            nastaveni.ZacatekPrace.ToString("HH:mm", CultureInfo.InvariantCulture));
        await NastavAsync(db, KlicKonecPrace,
            nastaveni.KonecPrace.ToString("HH:mm", CultureInfo.InvariantCulture));
        await NastavAsync(db, KlicZohlednitSvatky,
            nastaveni.ZohlednitSvatky.ToString(CultureInfo.InvariantCulture));
        await NastavAsync(db, KlicAutomatickeRazeni,
            nastaveni.AutomatickeRazeni.ToString(CultureInfo.InvariantCulture));

        await db.SaveChangesAsync();
    }

    private static async Task NastavAsync(PlanDbContext db, string klic, string hodnota)
    {
        var zaznam = await db.Nastaveni.FindAsync(klic);
        if (zaznam is null)
        {
            db.Nastaveni.Add(new NastaveniZaznam { Klic = klic, Hodnota = hodnota });
        }
        else
        {
            zaznam.Hodnota = hodnota;
        }
    }

    private static HashSet<DayOfWeek> ParsujDny(string hodnota)
    {
        var dny = new HashSet<DayOfWeek>();

        foreach (var cast in hodnota.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(cast, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cislo)
                && cislo is >= 0 and <= 6)
            {
                dny.Add((DayOfWeek)cislo);
            }
        }

        return dny;
    }
}
