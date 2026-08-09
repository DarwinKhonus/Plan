using System.Globalization;
using Plan.Data.Domain;

namespace Plan.ViewModels;

/// <summary>Jeden řádek přehledu: popisek vlevo, hodnota vpravo.</summary>
public record InfoRadek(string Popisek, string Hodnota);

/// <summary>
/// Podklad pro dialog Info. Údaje jsou schválně jen seznam řádků — přidání dalšího
/// údaje je pak jedna řádka tady a nic se nemusí měnit v XAML.
/// </summary>
public class InfoViewModel
{
    private const string FormatData = "d. M. yyyy";
    private const string FormatCasu = "d. M. yyyy H:mm";

    public InfoViewModel(
        ZakazkaViewModel zakazka,
        IEnumerable<ZakazkaViewModel> vsechnyZakazky,
        PracovniKalendar kalendar)
    {
        var kultura = CultureInfo.GetCultureInfo("cs-CZ");

        Nazev = zakazka.Nazev;
        MaKolizi = zakazka.MaKolizi;
        Stav = zakazka.MaKolizi ? "Konflikt s jinou zakázkou" : "Bez konfliktu";

        var pracovnichDnu = kalendar.PocetPracovnichDnu(zakazka.Rozsahy);
        var pokrytychDnu = UsekyNormalizace.PokryteDny(zakazka.Rozsahy).Count;
        var nepracovnichDnu = pokrytychDnu - pracovnichDnu;
        var hodinDenne = kalendar.Nastaveni.HodinDenne;

        KolidujiciZakazky = NajdiKolidujici(zakazka, vsechnyZakazky, kalendar);

        Useky = zakazka.Useky
            .OrderBy(u => u.DatumOd)
            .Select(u =>
                $"{u.DatumOd.ToString(FormatData, kultura)} – {u.DatumDo.ToString(FormatData, kultura)}"
                + $"  ({u.PocetDnu} {SklonujDny(u.PocetDnu)})")
            .ToList();

        Milniky = zakazka.Milniky
            .OrderBy(m => m.Datum)
            .Select(m => $"{m.Datum.ToString(FormatData, kultura)}  ·  {m.Nazev}")
            .ToList();

        var radky = new List<InfoRadek>
        {
            new(
                "Termín",
                $"{zakazka.DatumOd.ToString(FormatData, kultura)} – {zakazka.DatumDo.ToString(FormatData, kultura)}"),

            new(
                "Délka termínu",
                $"{zakazka.PocetDnu} {SklonujDny(zakazka.PocetDnu)}"),
        };

        // U rozdělené zakázky je rozdíl mezi celkovým rozsahem a skutečně pokrytými dny
        // podstatný, u nerozdělené by šlo o zbytečný řádek se stejným číslem.
        if (zakazka.JeRozdelena)
        {
            radky.Add(new InfoRadek(
                "Z toho pokryto úseky",
                $"{pokrytychDnu} {SklonujDny(pokrytychDnu)} v {zakazka.PocetUseku} {SklonujUseky(zakazka.PocetUseku)}"));
        }

        radky.Add(new InfoRadek("Pracovních dnů", $"{pracovnichDnu} {SklonujDny(pracovnichDnu)}"));
        radky.Add(new InfoRadek(
            "Nepracovních dnů",
            $"{nepracovnichDnu} {SklonujDny(nepracovnichDnu)} (víkendy a svátky)"));

        radky.Add(new InfoRadek(
            "Odhad hodin",
            string.Format(
                kultura,
                "{0:0.#} h  ({1} × {2:0.#} h/den)",
                zakazka.OdhadHodin,
                pracovnichDnu,
                hodinDenne)));

        radky.Add(new InfoRadek("Vytvořeno", zakazka.VytvorenoUtc.ToLocalTime().ToString(FormatCasu, kultura)));
        radky.Add(new InfoRadek("Naposledy upraveno", zakazka.UpravenoUtc.ToLocalTime().ToString(FormatCasu, kultura)));

        Radky = radky;
    }

    public string Nazev { get; }

    public bool MaKolizi { get; }

    public string Stav { get; }

    public IReadOnlyList<InfoRadek> Radky { get; }

    public IReadOnlyList<string> KolidujiciZakazky { get; }

    public bool MaSeznamKolizi => KolidujiciZakazky.Count > 0;

    public IReadOnlyList<string> Useky { get; }

    /// <summary>Výpis úseků má smysl jen u rozdělené zakázky; jinak jen opakuje termín.</summary>
    public bool MaSeznamUseku => Useky.Count > 1;

    public IReadOnlyList<string> Milniky { get; }

    public bool MaSeznamMilniku => Milniky.Count > 0;

    private static List<string> NajdiKolidujici(
        ZakazkaViewModel zakazka,
        IEnumerable<ZakazkaViewModel> vsechnyZakazky,
        PracovniKalendar kalendar)
    {
        var tato = zakazka.ToEntity();

        return vsechnyZakazky
            .Where(j => j.Id != zakazka.Id)
            .Where(j => KolizeDetektor.Koliduji(tato, j.ToEntity(), kalendar))
            .OrderBy(j => j.DatumOd)
            .Select(j => $"{j.Nazev}  ({j.DatumOd.ToString(FormatData, CultureInfo.GetCultureInfo("cs-CZ"))} – {j.DatumDo.ToString(FormatData, CultureInfo.GetCultureInfo("cs-CZ"))})")
            .ToList();
    }

    private static string SklonujDny(int pocet) => pocet switch
    {
        1 => "den",
        >= 2 and <= 4 => "dny",
        _ => "dnů",
    };

    private static string SklonujUseky(int pocet) => pocet switch
    {
        1 => "úseku",
        >= 2 and <= 4 => "úsecích",
        _ => "úsecích",
    };
}
