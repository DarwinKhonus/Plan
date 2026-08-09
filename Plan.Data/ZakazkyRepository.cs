using Microsoft.EntityFrameworkCore;
using Plan.Data.Domain;
using Plan.Data.Entities;

namespace Plan.Data;

public class ZakazkyRepository
{
    private readonly PlanDbFactory _factory;

    public ZakazkyRepository(PlanDbFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Všechny zakázky včetně úseků a milníků, seřazené podle začátku.</summary>
    public async Task<List<Zakazka>> NactiVseAsync()
    {
        await using var db = _factory.Create();

        var zakazky = await db.Zakazky
            .AsNoTracking()
            .Include(z => z.Useky)
            .Include(z => z.Milniky)
            .ToListAsync();

        // Deterministicky podle ručního pořadí. Kdo chce řazení podle termínu, přeskládá
        // si to sám — DatumOd je dopočítaná vlastnost nad úseky, kterou SQL nezná.
        return zakazky
            .OrderBy(z => z.Poradi)
            .ThenBy(z => z.Id)
            .ToList();
    }

    /// <summary>Uloží ruční pořadí zakázek podle jejich posloupnosti v seznamu.</summary>
    public async Task UlozPoradiAsync(IReadOnlyList<int> idsVPoradi)
    {
        await using var db = _factory.Create();
        var zakazky = await db.Zakazky.ToDictionaryAsync(z => z.Id);

        for (var i = 0; i < idsVPoradi.Count; i++)
        {
            if (zakazky.TryGetValue(idsVPoradi[i], out var zakazka))
            {
                zakazka.Poradi = i + 1;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<Zakazka> PridejAsync(string nazev, DateOnly od, DateOnly doVcetne)
    {
        var ted = DateTime.UtcNow;

        await using var db = _factory.Create();

        // Nová zakázka jde v ručním řazení na konec.
        var posledniPoradi = await db.Zakazky.AnyAsync()
            ? await db.Zakazky.MaxAsync(z => z.Poradi)
            : 0;

        var zakazka = new Zakazka
        {
            Nazev = nazev,
            Poradi = posledniPoradi + 1,
            VytvorenoUtc = ted,
            UpravenoUtc = ted,
            Useky = [new Usek { DatumOd = od, DatumDo = doVcetne }],
        };

        db.Zakazky.Add(zakazka);
        await db.SaveChangesAsync();
        return zakazka;
    }

    /// <summary>
    /// Úprava z dialogu: nastaví název a termín. Zakázka rozdělená na víc úseků se
    /// tím sloučí zpátky do jednoho — dialog pracuje s jedním rozsahem.
    /// </summary>
    public async Task UpravAsync(int id, string nazev, DateOnly od, DateOnly doVcetne)
    {
        await using var db = _factory.Create();
        var zakazka = await NactiSUsekyAsync(db, id);
        if (zakazka is null)
        {
            return;
        }

        zakazka.Nazev = nazev;
        db.Useky.RemoveRange(zakazka.Useky);
        zakazka.Useky = [new Usek { ZakazkaId = id, DatumOd = od, DatumDo = doVcetne }];
        zakazka.UpravenoUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Uloží posunutý úsek. Používá časová osa po dotažení pruhu myší — proto
    /// samostatná metoda, aby tažení nepřepisovalo název načtený do UI.
    /// </summary>
    public async Task UlozUsekAsync(int usekId, DateOnly od, DateOnly doVcetne)
    {
        await using var db = _factory.Create();
        var usek = await db.Useky.FindAsync(usekId);
        if (usek is null)
        {
            return;
        }

        usek.DatumOd = od;
        usek.DatumDo = doVcetne;

        await OznacUpravuAsync(db, usek.ZakazkaId);
        await db.SaveChangesAsync();

        await NormalizujUsekyAsync(usek.ZakazkaId);
    }

    /// <summary>
    /// Rozdělí úsek ke dni <paramref name="den"/>: první část končí předchozím dnem,
    /// druhá začíná zadaným dnem. Vrátí <c>false</c>, když by jedna část byla prázdná.
    /// </summary>
    public async Task<bool> RozdelUsekAsync(int usekId, DateOnly den)
    {
        await using var db = _factory.Create();
        var usek = await db.Useky.FindAsync(usekId);
        if (usek is null || den <= usek.DatumOd || den > usek.DatumDo)
        {
            return false;
        }

        var puvodniKonec = usek.DatumDo;
        usek.DatumDo = den.AddDays(-1);

        db.Useky.Add(new Usek
        {
            ZakazkaId = usek.ZakazkaId,
            DatumOd = den,
            DatumDo = puvodniKonec,
        });

        await OznacUpravuAsync(db, usek.ZakazkaId);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Smaže úsek. Poslední úsek zakázky smazat nelze — zakázka bez termínu nedává smysl.
    /// </summary>
    public async Task<bool> SmazUsekAsync(int usekId)
    {
        await using var db = _factory.Create();
        var usek = await db.Useky.FindAsync(usekId);
        if (usek is null)
        {
            return false;
        }

        var pocetUseku = await db.Useky.CountAsync(u => u.ZakazkaId == usek.ZakazkaId);
        if (pocetUseku <= 1)
        {
            return false;
        }

        db.Useky.Remove(usek);
        await OznacUpravuAsync(db, usek.ZakazkaId);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task PridejMilnikAsync(int zakazkaId, DateOnly datum, string nazev)
    {
        await using var db = _factory.Create();

        db.Milniky.Add(new Milnik
        {
            ZakazkaId = zakazkaId,
            Datum = datum,
            Nazev = nazev,
        });

        await OznacUpravuAsync(db, zakazkaId);
        await db.SaveChangesAsync();
    }

    public async Task UpravMilnikAsync(int milnikId, DateOnly datum, string nazev)
    {
        await using var db = _factory.Create();
        var milnik = await db.Milniky.FindAsync(milnikId);
        if (milnik is null)
        {
            return;
        }

        milnik.Datum = datum;
        milnik.Nazev = nazev;

        await OznacUpravuAsync(db, milnik.ZakazkaId);
        await db.SaveChangesAsync();
    }

    public async Task SmazMilnikAsync(int milnikId)
    {
        await using var db = _factory.Create();
        var milnik = await db.Milniky.FindAsync(milnikId);
        if (milnik is null)
        {
            return;
        }

        db.Milniky.Remove(milnik);
        await OznacUpravuAsync(db, milnik.ZakazkaId);
        await db.SaveChangesAsync();
    }

    public async Task SmazAsync(int id)
    {
        await using var db = _factory.Create();
        var zakazka = await db.Zakazky.FindAsync(id);
        if (zakazka is null)
        {
            return;
        }

        // Úseky a milníky odejdou s ní díky kaskádě nastavené v PlanDbContext.
        db.Zakazky.Remove(zakazka);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Slije úseky, které se po přetažení začaly překrývat. Dotýkající se úseky
    /// zůstanou oddělené, jinak by se rozdělení zakázky samo vrátilo zpátky.
    /// </summary>
    private async Task NormalizujUsekyAsync(int zakazkaId)
    {
        await using var db = _factory.Create();
        var useky = await db.Useky.Where(u => u.ZakazkaId == zakazkaId).ToListAsync();
        if (useky.Count < 2)
        {
            return;
        }

        var slite = UsekyNormalizace.Normalizuj(useky.Select(u => new Rozsah(u.DatumOd, u.DatumDo)));
        if (slite.Count == useky.Count)
        {
            return;
        }

        db.Useky.RemoveRange(useky);
        foreach (var rozsah in slite)
        {
            db.Useky.Add(new Usek { ZakazkaId = zakazkaId, DatumOd = rozsah.Od, DatumDo = rozsah.Do });
        }

        await db.SaveChangesAsync();
    }

    private static Task<Zakazka?> NactiSUsekyAsync(PlanDbContext db, int id) =>
        db.Zakazky.Include(z => z.Useky).FirstOrDefaultAsync(z => z.Id == id);

    private static async Task OznacUpravuAsync(PlanDbContext db, int zakazkaId)
    {
        var zakazka = await db.Zakazky.FindAsync(zakazkaId);
        if (zakazka is not null)
        {
            zakazka.UpravenoUtc = DateTime.UtcNow;
        }
    }
}
