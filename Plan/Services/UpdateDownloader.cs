using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Plan.Services;

public enum VysledekStazeni
{
    Ok,

    /// <summary>Stahování se nepodařilo dokončit (offline, přerušené spojení, chyba serveru).</summary>
    Selhalo,

    /// <summary>Soubor dorazil, ale nesouhlasí kontrolní součet — spouštět se nesmí.</summary>
    NesouhlasiKontrolniSoucet,
}

/// <summary>
/// Stáhne instalátor novější verze do dočasné složky a ověří ho proti kontrolnímu součtu
/// z GitHub API. Ověření je tady podstatné, protože se soubor následně spouští.
/// </summary>
public class UpdateDownloader
{
    private static readonly HttpClient Client = VytvorClienta();

    private static HttpClient VytvorClienta()
    {
        var client = new HttpClient
        {
            // Instalátor má desítky MB, výchozích 100 s na pomalé lince nestačí.
            Timeout = TimeSpan.FromMinutes(15),
        };

        client.DefaultRequestHeaders.Add("User-Agent", "Plan-UpdateDownloader");
        return client;
    }

    /// <summary>Složka pro stažené instalátory. Maže se před každým stahováním.</summary>
    public static string Slozka => Path.Combine(Path.GetTempPath(), "Plan-aktualizace");

    /// <summary>
    /// Smaže dřív stažené instalátory. Volá se, když je aplikace aktuální — instalátor
    /// už není k čemu a zabírá desítky megabajtů. Selhání se ignoruje (soubor může být
    /// právě spuštěný).
    /// </summary>
    public static void UklidStazene()
    {
        try
        {
            if (Directory.Exists(Slozka))
            {
                Directory.Delete(Slozka, recursive: true);
            }
        }
        catch
        {
            // Úklid je pohodlí, ne funkce — když to nejde, nic se neděje.
        }
    }

    /// <summary>
    /// Stáhne instalátor a vrátí cestu k souboru. <paramref name="pokrok"/> dostává podíl
    /// 0–1, když server ohlásí délku obsahu.
    /// </summary>
    public async Task<(VysledekStazeni Vysledek, string? Cesta, string? Chyba)> StahniAsync(
        DostupnaAktualizace aktualizace,
        IProgress<double>? pokrok = null,
        CancellationToken cancellationToken = default)
    {
        if (!aktualizace.LzeStahnout)
        {
            return (VysledekStazeni.Selhalo, null, "Release neobsahuje instalátor.");
        }

        var cil = Path.Combine(Slozka, aktualizace.InstalatorNazev ?? "Plan-setup.exe");

        try
        {
            // Zbytky z dřívějšího pokusu by mohly být poškozené nebo z jiné verze.
            if (Directory.Exists(Slozka))
            {
                Directory.Delete(Slozka, recursive: true);
            }

            Directory.CreateDirectory(Slozka);

            using var odpoved = await Client
                .GetAsync(aktualizace.InstalatorUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!odpoved.IsSuccessStatusCode)
            {
                return (VysledekStazeni.Selhalo, null, $"Server odpověděl {(int)odpoved.StatusCode}.");
            }

            var celkem = odpoved.Content.Headers.ContentLength ?? aktualizace.InstalatorVelikost;
            var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await using (var zdroj = await odpoved.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var soubor = File.Create(cil))
            {
                var buffer = new byte[81920];
                long prenaseno = 0;
                int precteno;

                while ((precteno = await zdroj.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await soubor.WriteAsync(buffer.AsMemory(0, precteno), cancellationToken).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, precteno);

                    prenaseno += precteno;
                    if (celkem > 0)
                    {
                        pokrok?.Report((double)prenaseno / celkem);
                    }
                }
            }

            if (aktualizace.InstalatorSha256 is { } ocekavany)
            {
                var spocitany = Convert.ToHexString(hash.GetHashAndReset());

                if (!spocitany.Equals(ocekavany, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(cil);
                    return (VysledekStazeni.NesouhlasiKontrolniSoucet, null,
                        "Stažený soubor nesouhlasí s kontrolním součtem z GitHubu.");
                }
            }

            return (VysledekStazeni.Ok, cil, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (VysledekStazeni.Selhalo, null, ex.Message);
        }
    }
}
