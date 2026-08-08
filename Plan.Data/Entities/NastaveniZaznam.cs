namespace Plan.Data.Entities;

/// <summary>
/// Key-value záznam globálního nastavení. Držíme nastavení v DB (ne v JSON vedle ní),
/// aby existoval jediný soubor k záloze a aby změny schématu nastavení pokrývaly migrace.
/// </summary>
public class NastaveniZaznam
{
    public string Klic { get; set; } = string.Empty;

    public string Hodnota { get; set; } = string.Empty;
}
