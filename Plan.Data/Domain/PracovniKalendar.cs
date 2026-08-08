namespace Plan.Data.Domain;

/// <summary>
/// Odpovídá na otázku „je tenhle den pracovní?“ a počítá odhad pracovních hodin
/// v termínu zakázky. Odhad je čistě informativní — neovlivňuje plánování.
/// </summary>
public class PracovniKalendar
{
    private readonly PracovniNastaveni _nastaveni;

    public PracovniKalendar(PracovniNastaveni nastaveni)
    {
        _nastaveni = nastaveni;
    }

    public PracovniNastaveni Nastaveni => _nastaveni;

    public bool JePracovniDen(DateOnly den)
    {
        if (!_nastaveni.PracovniDny.Contains(den.DayOfWeek))
        {
            return false;
        }

        if (_nastaveni.ZohlednitSvatky && SvatkyCz.JeSvatek(den))
        {
            return false;
        }

        return true;
    }

    /// <summary>Počet pracovních dnů v intervalu včetně obou krajních dnů.</summary>
    public int PocetPracovnichDnu(DateOnly od, DateOnly doVcetne)
    {
        if (doVcetne < od)
        {
            return 0;
        }

        var pocet = 0;
        for (var den = od; den <= doVcetne; den = den.AddDays(1))
        {
            if (JePracovniDen(den))
            {
                pocet++;
            }
        }

        return pocet;
    }

    /// <summary>
    /// Je v intervalu aspoň jeden pracovní den? Končí u prvního nalezeného, takže se
    /// nemusí projít celý rozsah jako u <see cref="PocetPracovnichDnu"/>.
    /// </summary>
    public bool ObsahujePracovniDen(DateOnly od, DateOnly doVcetne)
    {
        for (var den = od; den <= doVcetne; den = den.AddDays(1))
        {
            if (JePracovniDen(den))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Odhad pracovních hodin v intervalu = pracovní dny × hodin/den.</summary>
    public double OdhadHodin(DateOnly od, DateOnly doVcetne) =>
        PocetPracovnichDnu(od, doVcetne) * _nastaveni.HodinDenne;
}
