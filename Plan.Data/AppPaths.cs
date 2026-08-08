namespace Plan.Data;

/// <summary>Umístění lokálních souborů aplikace.</summary>
public static class AppPaths
{
    /// <summary>Složka <c>%AppData%\Plan</c>. Vytvoří ji, pokud neexistuje.</summary>
    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Plan");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, "plan.db");

    public static string ConnectionString => $"Data Source={DatabaseFile}";
}
