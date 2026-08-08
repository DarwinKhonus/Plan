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
}
