using Microsoft.EntityFrameworkCore;

namespace Plan.Data;

/// <summary>
/// Vytváří krátkožijící <see cref="PlanDbContext"/> nad lokálním SQLite souborem.
/// Kontext se nedrží dlouhodobě otevřený — u desktopové aplikace s jedním uživatelem
/// je jednodušší a bezpečnější otevřít kontext na jednu operaci a zase ho zavřít.
/// </summary>
public class PlanDbFactory
{
    private readonly string _connectionString;

    public PlanDbFactory() : this(AppPaths.ConnectionString)
    {
    }

    public PlanDbFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public PlanDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PlanDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        return new PlanDbContext(options);
    }

    /// <summary>
    /// Aplikuje čekající migrace. Volá se jednou při startu — díky tomu update aplikace
    /// nikdy nepřepíše ani nezahodí existující data uživatele.
    /// </summary>
    public void MigrateDatabase()
    {
        using var db = Create();
        db.Database.Migrate();
    }

    /// <summary>
    /// Připraví databázi ke čtení a vrátí, v jakém je stavu.
    /// </summary>
    /// <remarks>
    /// Databáze z novější verze aplikace se pozná podle migrací zapsaných v historii,
    /// které tohle sestavení nezná. Migrovat ji nelze a čtení by skončilo nesrozumitelnou
    /// SQL chybou o chybějícím sloupci, proto se to hlásí zvlášť.
    /// </remarks>
    public StavDatabaze Priprav(out string? popisChyby)
    {
        popisChyby = null;

        try
        {
            using var db = Create();

            var znameMigrace = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var pouziteMigrace = db.Database.GetAppliedMigrations().ToList();
            var neznameMigrace = pouziteMigrace.Where(m => !znameMigrace.Contains(m)).ToList();

            if (neznameMigrace.Count > 0)
            {
                popisChyby = string.Join(", ", neznameMigrace);
                return StavDatabaze.NovejsiNezAplikace;
            }

            db.Database.Migrate();
            return StavDatabaze.Ok;
        }
        catch (Exception ex)
        {
            popisChyby = ex.Message;
            return StavDatabaze.Nedostupna;
        }
    }
}

public enum StavDatabaze
{
    /// <summary>Databáze je připravená a odpovídá tomuto sestavení.</summary>
    Ok,

    /// <summary>Databáze byla vytvořena novější verzí aplikace a nejde s ní pracovat.</summary>
    NovejsiNezAplikace,

    /// <summary>Databázi se nepodařilo otevřít.</summary>
    Nedostupna,
}
