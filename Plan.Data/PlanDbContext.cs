using Microsoft.EntityFrameworkCore;
using Plan.Data.Entities;

namespace Plan.Data;

public class PlanDbContext : DbContext
{
    public PlanDbContext(DbContextOptions<PlanDbContext> options) : base(options)
    {
    }

    public DbSet<Zakazka> Zakazky => Set<Zakazka>();

    public DbSet<Usek> Useky => Set<Usek>();

    public DbSet<Milnik> Milniky => Set<Milnik>();

    public DbSet<NastaveniZaznam> Nastaveni => Set<NastaveniZaznam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zakazka>(e =>
        {
            e.ToTable("Zakazky");
            e.HasKey(z => z.Id);
            e.Property(z => z.Nazev).IsRequired().HasMaxLength(200);
            e.Property(z => z.VytvorenoUtc).IsRequired();
            e.Property(z => z.UpravenoUtc).IsRequired();

            // DatumOd a DatumDo se dopočítávají z úseků, v databázi nemají co dělat.
            e.Ignore(z => z.DatumOd);
            e.Ignore(z => z.DatumDo);
        });

        modelBuilder.Entity<Usek>(e =>
        {
            e.ToTable("Useky");
            e.HasKey(u => u.Id);
            e.Property(u => u.DatumOd).IsRequired();
            e.Property(u => u.DatumDo).IsRequired();

            // Smazání zakázky vezme její úseky s sebou.
            e.HasOne(u => u.Zakazka)
                .WithMany(z => z.Useky)
                .HasForeignKey(u => u.ZakazkaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(u => u.ZakazkaId);
            e.HasIndex(u => u.DatumOd);
        });

        modelBuilder.Entity<Milnik>(e =>
        {
            e.ToTable("Milniky");
            e.HasKey(m => m.Id);
            e.Property(m => m.Datum).IsRequired();
            e.Property(m => m.Nazev).IsRequired().HasMaxLength(200);

            e.HasOne(m => m.Zakazka)
                .WithMany(z => z.Milniky)
                .HasForeignKey(m => m.ZakazkaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(m => m.ZakazkaId);
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
