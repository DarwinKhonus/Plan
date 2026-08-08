using Microsoft.EntityFrameworkCore;
using Plan.Data.Entities;

namespace Plan.Data;

public class ZakazkyRepository
{
    private readonly PlanDbFactory _factory;

    public ZakazkyRepository(PlanDbFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Všechny zakázky seřazené podle termínu od.</summary>
    public async Task<List<Zakazka>> NactiVseAsync()
    {
        await using var db = _factory.Create();
        return await db.Zakazky
            .AsNoTracking()
            .OrderBy(z => z.DatumOd)
            .ThenBy(z => z.DatumDo)
            .ToListAsync();
    }

    public async Task<Zakazka> PridejAsync(string nazev, DateOnly od, DateOnly doVcetne)
    {
        var ted = DateTime.UtcNow;
        var zakazka = new Zakazka
        {
            Nazev = nazev,
            DatumOd = od,
            DatumDo = doVcetne,
            VytvorenoUtc = ted,
            UpravenoUtc = ted,
        };

        await using var db = _factory.Create();
        db.Zakazky.Add(zakazka);
        await db.SaveChangesAsync();
        return zakazka;
    }

    public async Task UpravAsync(int id, string nazev, DateOnly od, DateOnly doVcetne)
    {
        await using var db = _factory.Create();
        var zakazka = await db.Zakazky.FindAsync(id);
        if (zakazka is null)
        {
            return;
        }

        zakazka.Nazev = nazev;
        zakazka.DatumOd = od;
        zakazka.DatumDo = doVcetne;
        zakazka.UpravenoUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Uloží jen posunutý termín. Používá časová osa po dotažení pruhu myší — proto
    /// samostatná metoda, aby drag nepřepisoval název načtený do UI.
    /// </summary>
    public async Task UlozTerminAsync(int id, DateOnly od, DateOnly doVcetne)
    {
        await using var db = _factory.Create();
        var zakazka = await db.Zakazky.FindAsync(id);
        if (zakazka is null)
        {
            return;
        }

        zakazka.DatumOd = od;
        zakazka.DatumDo = doVcetne;
        zakazka.UpravenoUtc = DateTime.UtcNow;
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

        db.Zakazky.Remove(zakazka);
        await db.SaveChangesAsync();
    }
}
