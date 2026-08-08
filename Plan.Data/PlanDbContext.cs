using Microsoft.EntityFrameworkCore;
using Plan.Data.Entities;

namespace Plan.Data;

public class PlanDbContext : DbContext
{
    public PlanDbContext(DbContextOptions<PlanDbContext> options) : base(options)
    {
    }

    public DbSet<Zakazka> Zakazky => Set<Zakazka>();

    public DbSet<NastaveniZaznam> Nastaveni => Set<NastaveniZaznam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zakazka>(e =>
        {
            e.ToTable("Zakazky");
            e.HasKey(z => z.Id);
            e.Property(z => z.Nazev).IsRequired().HasMaxLength(200);
            e.Property(z => z.DatumOd).IsRequired();
            e.Property(z => z.DatumDo).IsRequired();
            e.Property(z => z.VytvorenoUtc).IsRequired();
            e.Property(z => z.UpravenoUtc).IsRequired();

            // Kalendář i tabulka řadí a filtrují podle termínu.
            e.HasIndex(z => z.DatumOd);
        });

        modelBuilder.Entity<NastaveniZaznam>(e =>
        {
            e.ToTable("Nastaveni");
            e.HasKey(n => n.Klic);
            e.Property(n => n.Klic).HasMaxLength(100);
            e.Property(n => n.Hodnota).IsRequired();
        });
    }
}
