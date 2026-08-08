using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Plan.Data;

/// <summary>
/// Používá se jen nástroji <c>dotnet ef</c> při generování migrací, aby si uměly
/// vytvořit kontext bez spuštění WPF aplikace. V runtime se nepoužívá.
/// </summary>
public class PlanDbContextFactory : IDesignTimeDbContextFactory<PlanDbContext>
{
    public PlanDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlanDbContext>()
            .UseSqlite(AppPaths.ConnectionString)
            .Options;

        return new PlanDbContext(options);
    }
}
