using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Plan.Services;

public record DostupnaAktualizace(string Verze, string StrankaReleaseUrl);

/// <summary>
/// Zjišťuje, jestli je na GitHubu novější release než běžící sestavení.
/// Selhání je vždy tichá — offline stroj ani nedostupný GitHub nesmí ovlivnit start aplikace.
/// </summary>
public class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/DarwinKhonus/Plan/releases/latest";

    private static readonly HttpClient Client = VytvorClienta();

    private static HttpClient VytvorClienta()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // GitHub API odmítá požadavky bez User-Agent.
        client.DefaultRequestHeaders.Add("User-Agent", "Plan-UpdateChecker");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    /// <summary>Verze běžícího sestavení, odvozená z <c>InformationalVersion</c>.</summary>
    public static Version AktualniVerze
    {
        get
        {
            var informational = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            return ParsujVerzi(informational) ?? new Version(0, 0, 0);
        }
    }

    /// <summary>
    /// Vrátí popis novější verze, nebo <c>null</c> — když je aplikace aktuální,
    /// nebo když se kontrolu z jakéhokoli důvodu nepodařilo dokončit.
    /// </summary>
    public async Task<DostupnaAktualizace?> ZkontrolujAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var odpoved = await Client.GetAsync(ApiUrl, cancellationToken).ConfigureAwait(false);
            if (!odpoved.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await odpoved.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var dokument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var korene = dokument.RootElement;

            if (korene.TryGetProperty("draft", out var draft) && draft.GetBoolean())
            {
                return null;
            }

            if (!korene.TryGetProperty("tag_name", out var tagElement))
            {
                return null;
            }

            var tag = tagElement.GetString();
            var nejnovejsi = ParsujVerzi(tag);
            if (nejnovejsi is null || nejnovejsi <= AktualniVerze)
            {
                return null;
            }

            var url = korene.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            return new DostupnaAktualizace(
                tag!.TrimStart('v', 'V'),
                url ?? "https://github.com/DarwinKhonus/Plan/releases/latest");
        }
        catch
        {
            // Offline, DNS, timeout, změněný tvar odpovědi — nic z toho nesmí bublat do UI.
            return null;
        }
    }

    /// <summary>
    /// Vytáhne verzi z tagu (<c>v1.2.3</c>) nebo z InformationalVersion
    /// (<c>1.2.3+abc1234</c>, <c>1.2.3-beta</c>).
    /// </summary>
    public static Version? ParsujVerzi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var ocistene = text.Trim().TrimStart('v', 'V');

        var oddelovac = ocistene.IndexOfAny(['+', '-']);
        if (oddelovac >= 0)
        {
            ocistene = ocistene[..oddelovac];
        }

        return Version.TryParse(ocistene, out var verze) ? verze : null;
    }
}
