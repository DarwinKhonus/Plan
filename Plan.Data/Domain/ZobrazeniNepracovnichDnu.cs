namespace Plan.Data.Domain;

/// <summary>Jak se v pruhu zakázky vyznačují víkendy a svátky.</summary>
public enum ZobrazeniNepracovnichDnu
{
    /// <summary>
    /// Pruh se přeruší a přes nepracovní dny vede spojovací čára — stejně jako mezi
    /// úseky rozdělené zakázky.
    /// </summary>
    Cara,

    /// <summary>Pruh drží plnou barvu a přes nepracovní dny jdou bílé pruhy.</summary>
    Srafa,

    /// <summary>Výplň se v nepracovních dnech vynechá a zůstane jen obrys pruhu.</summary>
    Obrys,
}
