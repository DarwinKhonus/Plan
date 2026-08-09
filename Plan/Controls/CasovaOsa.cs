using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Plan.Data.Domain;
using Plan.ViewModels;

namespace Plan.Controls;

/// <summary>
/// Časová osa zakázek. Řádek = zakázka, sloupec = den. Pruh jde tažením posunout
/// (tělo) nebo za okraj protáhnout (jen začátek, resp. jen konec).
/// </summary>
/// <remarks>
/// Kreslí se ručně přes <see cref="OnRender"/> místo skládání z WPF prvků — při stovkách
/// dnů × zakázek by vizuální strom s elementem na každý den byl zbytečně těžký a hit-testing
/// pro tažení okrajů by se stejně musel psát ručně.
/// </remarks>
public class CasovaOsa : FrameworkElement
{
    private const double SirkaUchopuOkraje = 6;
    private const double MinimalniSirkaProUchopy = 20;
    private const double PolomerMilniku = 7;

    private static readonly Brush StetecPozadi = Vytvor("#FFFFFF");
    private static readonly Brush StetecNepracovniDen = Vytvor("#F1F4F8");
    private static readonly Brush StetecSvatek = Vytvor("#FDF0E3");
    private static readonly Brush StetecHlavicka = Vytvor("#FAFBFC");
    private static readonly Brush StetecText = Vytvor("#3C4653");
    private static readonly Brush StetecTextSlaby = Vytvor("#8A94A3");
    private static readonly Brush StetecDnes = Vytvor("#E8590C");
    private static readonly Brush StetecPruh = Vytvor("#3B82C4");
    private static readonly Brush StetecPruhKolize = Vytvor("#C0392B");
    private static readonly Brush StetecTextPruhu = Brushes.White;
    private static readonly Pen PeroMrizka = VytvorPero("#E6E9ED", 1);
    private static readonly Pen PeroMesic = VytvorPero("#C7CDD5", 1);
    private static readonly Pen PeroPredelMesice = VytvorTeckovanePero("#9AA4B2", 1);
    private static readonly Pen PeroDnes = VytvorPero("#E8590C", 2);
    private static readonly Pen PeroPruh = VytvorPero("#2C6494", 1);
    private static readonly Pen PeroPruhKolize = VytvorPero("#922B21", 1);
    private static readonly Pen PeroVyber = VytvorPero("#1B3E5E", 2.5);

    // Název zakázky mimo pruh: tmavý text na podkladu osy.
    private static readonly Brush StetecPopisku = Vytvor("#2C3542");
    private static readonly Brush StetecPopiskuKolize = Vytvor("#922B21");

    // Podložka pro krajní případ, kdy se název musí vejít dovnitř pruhu.
    private static readonly Brush StetecPodlozkaPopisku = Vytvor("#73101A24");
    // Poloprůhledný, aby přes zvýrazněný řádek zůstalo vidět podbarvení víkendů a svátků.
    private static readonly Brush StetecVybranyRadek = Vytvor("#2E3B82C4");

    private static readonly Brush StetecMilnik = Vytvor("#F5B301");
    private static readonly Pen PeroMilnik = VytvorPero("#8A6100", 1.5);

    // Přerušovaná linka přes pauzu mezi úseky jedné zakázky.
    private static readonly Pen PeroSpojnice = VytvorTeckovanePero("#7E93AC", 1.5);

    private static readonly CultureInfo Kultura = CultureInfo.GetCultureInfo("cs-CZ");

    private static readonly Typeface Pismo = new("Segoe UI");

    private static readonly Typeface PismoTucne = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static readonly Typeface PismoPolotucne = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private TazeniStav? _tazeni;

    /// <summary>
    /// Popisek milníku. Řídí se ručně, protože nastavovat vlastnost ToolTip až během
    /// pohybu myši nefunguje — ToolTipService si obsah přečte při otevírání a změna
    /// za běhu se neprojeví.
    /// </summary>
    private readonly ToolTip _popisekMilniku = new()
    {
        Placement = PlacementMode.MousePoint,
        HorizontalOffset = 12,
        VerticalOffset = 12,
        StaysOpen = true,
    };

    private MilnikViewModel? _popsanyMilnik;

    static CasovaOsa()
    {
        FocusableProperty.OverrideMetadata(typeof(CasovaOsa), new FrameworkPropertyMetadata(true));
    }

    public CasovaOsa()
    {
        _popisekMilniku.PlacementTarget = this;
    }

    public event EventHandler<UsekZmenenEventArgs>? UsekZmenen;

    /// <summary>Levý klik na milník — okno na to otevírá dialog úpravy.</summary>
    public event EventHandler<MilnikKliknutEventArgs>? MilnikKliknut;

    /// <summary>Průběžná změna pořadí při svislém tažení.</summary>
    public event EventHandler<PoradiZmenenoEventArgs>? PoradiZmeneno;

    /// <summary>Svislé tažení skončilo — pořadí je na místě a má se uložit.</summary>
    public event EventHandler? PoradiDotazeno;

    #region Dependency properties

    public static readonly DependencyProperty ZakazkyProperty = DependencyProperty.Register(
        nameof(Zakazky),
        typeof(System.Collections.IEnumerable),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(null, OnZakazkyChanged));

    public static readonly DependencyProperty VybranaZakazkaProperty = DependencyProperty.Register(
        nameof(VybranaZakazka),
        typeof(ZakazkaViewModel),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty KalendarProperty = DependencyProperty.Register(
        nameof(Kalendar),
        typeof(PracovniKalendar),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrvniDenProperty = DependencyProperty.Register(
        nameof(PrvniDen),
        typeof(DateOnly),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            DateOnly.FromDateTime(DateTime.Today),
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PocetDnuProperty = DependencyProperty.Register(
        nameof(PocetDnu),
        typeof(int),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            90,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Vodorovný posun osy. Okno ho hlásí ze ScrollVieweru, aby název měsíce mohl zůstat
    /// vidět i po odscrollování doprostřed měsíce.
    /// </summary>
    public static readonly DependencyProperty VodorovnyPosunProperty = DependencyProperty.Register(
        nameof(VodorovnyPosun),
        typeof(double),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Šířka viditelné části. Spolu s posunem určuje, které dny se vůbec kreslí —
    /// bez toho se sázely texty i pro dny mimo okno a vykreslení stálo desítky ms.
    /// </summary>
    /// <summary>Smí svislé tažení měnit pořadí? Řídí se nastavením automatického řazení.</summary>
    public static readonly DependencyProperty LzeMenitPoradiProperty = DependencyProperty.Register(
        nameof(LzeMenitPoradi),
        typeof(bool),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty SirkaViditelnehoOknaProperty = DependencyProperty.Register(
        nameof(SirkaViditelnehoOkna),
        typeof(double),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SirkaDneProperty = DependencyProperty.Register(
        nameof(SirkaDne),
        typeof(double),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            26.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VyskaRadkuProperty = DependencyProperty.Register(
        nameof(VyskaRadku),
        typeof(double),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            34.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VyskaHlavickyProperty = DependencyProperty.Register(
        nameof(VyskaHlavicky),
        typeof(double),
        typeof(CasovaOsa),
        new FrameworkPropertyMetadata(
            56.0,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public System.Collections.IEnumerable? Zakazky
    {
        get => (System.Collections.IEnumerable?)GetValue(ZakazkyProperty);
        set => SetValue(ZakazkyProperty, value);
    }

    public ZakazkaViewModel? VybranaZakazka
    {
        get => (ZakazkaViewModel?)GetValue(VybranaZakazkaProperty);
        set => SetValue(VybranaZakazkaProperty, value);
    }

    /// <summary>
    /// Kontext posledního pravého kliknutí. Kontextová nabídka podle něj ví, kterého
    /// úseku, milníku a dne se položky týkají.
    /// </summary>
    public UsekViewModel? VybranyUsek { get; private set; }

    public MilnikViewModel? VybranyMilnik { get; private set; }

    public DateOnly DenPodKurzorem { get; private set; }

    public PracovniKalendar? Kalendar
    {
        get => (PracovniKalendar?)GetValue(KalendarProperty);
        set => SetValue(KalendarProperty, value);
    }

    public DateOnly PrvniDen
    {
        get => (DateOnly)GetValue(PrvniDenProperty);
        set => SetValue(PrvniDenProperty, value);
    }

    public int PocetDnu
    {
        get => (int)GetValue(PocetDnuProperty);
        set => SetValue(PocetDnuProperty, value);
    }

    public double VodorovnyPosun
    {
        get => (double)GetValue(VodorovnyPosunProperty);
        set => SetValue(VodorovnyPosunProperty, value);
    }

    public double SirkaViditelnehoOkna
    {
        get => (double)GetValue(SirkaViditelnehoOknaProperty);
        set => SetValue(SirkaViditelnehoOknaProperty, value);
    }

    public bool LzeMenitPoradi
    {
        get => (bool)GetValue(LzeMenitPoradiProperty);
        set => SetValue(LzeMenitPoradiProperty, value);
    }

    /// <summary>
    /// Rozsah indexů dnů, které mají smysl kreslit. Když okno svou šířku ještě nenahlásilo,
    /// kreslí se všechno, aby se nic neztratilo.
    /// </summary>
    private (int Prvni, int Posledni) ViditelneDny()
    {
        var posledniIndex = Math.Max(PocetDnu - 1, 0);

        if (SirkaViditelnehoOkna <= 0 || SirkaDne <= 0)
        {
            return (0, posledniIndex);
        }

        // Jeden den rezervy na každou stranu, aby na okrajích nechyběly čáry mřížky.
        var prvni = Math.Max((int)(VodorovnyPosun / SirkaDne) - 1, 0);
        var posledni = Math.Min(
            (int)Math.Ceiling((VodorovnyPosun + SirkaViditelnehoOkna) / SirkaDne) + 1,
            posledniIndex);

        return (prvni, posledni);
    }

    public double SirkaDne
    {
        get => (double)GetValue(SirkaDneProperty);
        set => SetValue(SirkaDneProperty, value);
    }

    public double VyskaRadku
    {
        get => (double)GetValue(VyskaRadkuProperty);
        set => SetValue(VyskaRadkuProperty, value);
    }

    public double VyskaHlavicky
    {
        get => (double)GetValue(VyskaHlavickyProperty);
        set => SetValue(VyskaHlavickyProperty, value);
    }

    #endregion

    #region Sledování kolekce

    private static void OnZakazkyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var osa = (CasovaOsa)d;
        osa.OdpojSledovani(e.OldValue as System.Collections.IEnumerable);
        osa.PripojSledovani(e.NewValue as System.Collections.IEnumerable);
        osa.InvalidateMeasure();
        osa.InvalidateVisual();
    }

    private void PripojSledovani(System.Collections.IEnumerable? kolekce)
    {
        if (kolekce is null)
        {
            return;
        }

        if (kolekce is INotifyCollectionChanged notifikujici)
        {
            notifikujici.CollectionChanged += NaZmenuKolekce;
        }

        foreach (var polozka in kolekce)
        {
            if (polozka is INotifyPropertyChanged sledovana)
            {
                sledovana.PropertyChanged += NaZmenuPolozky;
            }
        }
    }

    private void OdpojSledovani(System.Collections.IEnumerable? kolekce)
    {
        if (kolekce is null)
        {
            return;
        }

        if (kolekce is INotifyCollectionChanged notifikujici)
        {
            notifikujici.CollectionChanged -= NaZmenuKolekce;
        }

        foreach (var polozka in kolekce)
        {
            if (polozka is INotifyPropertyChanged sledovana)
            {
                sledovana.PropertyChanged -= NaZmenuPolozky;
            }
        }
    }

    private void NaZmenuKolekce(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Reset neřekne, co odešlo — přepojíme sledování na aktuální obsah.
            PripojSledovani(Zakazky);
        }
        else
        {
            foreach (var polozka in e.OldItems ?? Array.Empty<object>())
            {
                if (polozka is INotifyPropertyChanged stara)
                {
                    stara.PropertyChanged -= NaZmenuPolozky;
                }
            }

            foreach (var polozka in e.NewItems ?? Array.Empty<object>())
            {
                if (polozka is INotifyPropertyChanged nova)
                {
                    nova.PropertyChanged += NaZmenuPolozky;
                }
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void NaZmenuPolozky(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    #endregion

    private List<ZakazkaViewModel> AktualniZakazky()
    {
        if (Zakazky is null)
        {
            return [];
        }

        var seznam = new List<ZakazkaViewModel>();
        foreach (var polozka in Zakazky)
        {
            if (polozka is ZakazkaViewModel zakazka)
            {
                seznam.Add(zakazka);
            }
        }

        return seznam;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var sirka = Math.Max(1, PocetDnu) * SirkaDne;
        var vyska = VyskaHlavicky + (Math.Max(AktualniZakazky().Count, 1) * VyskaRadku);
        return new Size(sirka, vyska);
    }

    #region Vykreslení

    protected override void OnRender(DrawingContext dc)
    {
        var zakazky = AktualniZakazky();
        var sirka = Math.Max(1, PocetDnu) * SirkaDne;
        var vyska = Math.Max(RenderSize.Height, VyskaHlavicky + (zakazky.Count * VyskaRadku));

        dc.DrawRectangle(StetecPozadi, null, new Rect(0, 0, sirka, vyska));

        VykresliPozadiDnu(dc, sirka, vyska);
        VykresliHlavicku(dc, sirka);
        VykresliPruhy(dc, zakazky);
        VykresliDnesniDen(dc, vyska);
    }

    private void VykresliPozadiDnu(DrawingContext dc, double sirka, double vyska)
    {
        var kalendar = Kalendar;
        var vyskaObsahu = vyska - VyskaHlavicky;

        var (prvniDen, posledniDen) = ViditelneDny();

        for (var i = prvniDen; i <= posledniDen; i++)
        {
            var den = PrvniDen.AddDays(i);
            var x = i * SirkaDne;

            var jeSvatek = kalendar?.Nastaveni.ZohlednitSvatky == true && SvatkyCz.JeSvatek(den);
            var jeNepracovni = kalendar is not null && !kalendar.JePracovniDen(den);

            if (jeSvatek)
            {
                dc.DrawRectangle(StetecSvatek, null, new Rect(x, VyskaHlavicky, SirkaDne, vyskaObsahu));
            }
            else if (jeNepracovni)
            {
                dc.DrawRectangle(StetecNepracovniDen, null, new Rect(x, VyskaHlavicky, SirkaDne, vyskaObsahu));
            }

            // Denní mřížka začíná až pod hlavičkou, jinak by přeškrtávala čísla dnů.
            // Předěl měsíců je tečkovaný a vede přes celou výšku, aby oddělil i popisky
            // v hlavičce — tečkovaně proto, ať nesoupeří o pozornost s pruhy zakázek.
            var jeZacatekMesice = den.Day == 1;
            var pero = jeZacatekMesice ? PeroPredelMesice : PeroMrizka;
            var horni = jeZacatekMesice ? 0 : VyskaHlavicky;
            dc.DrawLine(pero, new Point(x + 0.5, horni), new Point(x + 0.5, vyska));
        }

        dc.DrawLine(PeroMesic, new Point(0, VyskaHlavicky + 0.5), new Point(sirka, VyskaHlavicky + 0.5));
    }

    /// <summary>
    /// Po kolika dnech popisovat čísla v hlavičce. Dokud se dvojciferné číslo vejde do
    /// jednoho dne, popisují se všechny; pak se přechází na pondělky, každý druhý
    /// a každý čtvrtý.
    /// </summary>
    /// <remarks>
    /// Krok je záměrně po týdnech, ne po desítkách dnů: měsíce mají různou délku, takže
    /// popisky 1–10–20–30 by se na přelomu měsíce srazily k sobě. Týdenní rytmus navíc
    /// odpovídá podbarveným víkendům.
    /// </remarks>
    private int KrokPopiskuDnu()
    {
        // Nejširší dvojciferné číslo plus odsazení.
        const double PotrebnaSirka = 20;

        if (SirkaDne >= PotrebnaSirka)
        {
            return 1;
        }

        foreach (var krok in (int[])[7, 14, 28])
        {
            if (krok * SirkaDne >= PotrebnaSirka)
            {
                return krok;
            }
        }

        return 28;
    }

    /// <summary>
    /// Padne den na pravidelný krok? U týdenních kroků se počítá od pondělí, aby popisky
    /// stály vždy na stejném dni v týdnu.
    /// </summary>
    private static bool JeDenNaKroku(DateOnly den, int krok)
    {
        if (den.DayOfWeek != DayOfWeek.Monday)
        {
            return false;
        }

        // Počet týdnů od pevného pondělí (5. 1. 1970), aby volba nezávisela na rozsahu osy.
        var tydnuOdReferencniho = (den.DayNumber - new DateOnly(1970, 1, 5).DayNumber) / 7;
        return tydnuOdReferencniho % (krok / 7) == 0;
    }

    private void VykresliHlavicku(DrawingContext dc, double sirka)
    {
        dc.DrawRectangle(StetecHlavicka, null, new Rect(0, 0, sirka, VyskaHlavicky));

        var kultura = CultureInfo.GetCultureInfo("cs-CZ");

        // Horní pás patří názvu měsíce, zbytek dvouřádkovému bloku „číslo + zkratka dne“,
        // který je vyšší — proto ne půl na půl.
        var vyskaMesicu = VyskaHlavicky * 0.4;

        // Pás s názvy měsíců.
        var zacatekUseku = 0;
        for (var i = 1; i <= PocetDnu; i++)
        {
            var konecUseku = i == PocetDnu;
            var zmenaMesice = !konecUseku && PrvniDen.AddDays(i).Month != PrvniDen.AddDays(zacatekUseku).Month;

            if (!konecUseku && !zmenaMesice)
            {
                continue;
            }

            var den = PrvniDen.AddDays(zacatekUseku);

            // MonthNames dává 1. pád („srpen“); formát "MMMM" by v češtině vrátil 2. pád („srpna“).
            var nazevMesice = kultura.DateTimeFormat.MonthNames[den.Month - 1];
            var popisek = $"{kultura.TextInfo.ToTitleCase(nazevMesice)} {den.Year}";
            var text = VytvorText(popisek, 12, StetecText, FontWeights.SemiBold);

            var levyOkraj = zacatekUseku * SirkaDne;
            var pravyOkraj = i * SirkaDne;

            // Popisek se drží u levého okraje měsíce, ale po odscrollování se posune
            // k okraji okna — jinak by u širokého přiblížení zmizel z dohledu.
            var x = Math.Max(levyOkraj, VodorovnyPosun) + 6;

            if (x + text.Width + 2 <= pravyOkraj)
            {
                dc.DrawText(text, new Point(x, (vyskaMesicu - text.Height) / 2));
            }

            zacatekUseku = i;
        }

        // Pás s čísly dnů a zkratkou dne v týdnu. Jen viditelné dny — sazba textu je
        // nejdražší část vykreslení, takže dny mimo okno se vynechávají.
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        var (prvniViditelny, posledniViditelny) = ViditelneDny();

        var krokCisel = KrokPopiskuDnu();
        var dvoupismennaZkratka = SirkaDne >= 16;
        var zobrazitZkratky = SirkaDne >= 8;

        for (var i = prvniViditelny; i <= posledniViditelny; i++)
        {
            var den = PrvniDen.AddDays(i);
            var x = i * SirkaDne;
            var jeDnes = den == dnes;
            var jeNepracovni = Kalendar is not null && !Kalendar.JePracovniDen(den);

            var stetec = jeDnes
                ? StetecDnes
                : jeNepracovni ? StetecTextSlaby : StetecText;
            var vaha = jeDnes ? FontWeights.Bold : FontWeights.Normal;

            // Číslo dne se popisuje jen v pravidelném kroku. Dřív se dvojciferné číslo
            // prostě zahodilo, když se nevešlo, takže v hlavičce vznikaly nepravidelné
            // mezery a datum bylo místy nečitelné.
            var popsatCislo = krokCisel == 1 || JeDenNaKroku(den, krokCisel);

            var cislo = VytvorTextZMezipameti(den.Day.ToString(kultura), 11, stetec, vaha);

            var zkratka = dvoupismennaZkratka
                ? ZkratkaDne(den.DayOfWeek)
                : ZkratkaDneKratka(den.DayOfWeek);
            var denVTydnu = VytvorTextZMezipameti(
                zkratka, 9, jeDnes ? StetecDnes : StetecTextSlaby, vaha);

            // Obě řádky se vysází jako blok vystředěný ve zbytku hlavičky. Pevná odsazení
            // tu dřív byla — při skutečné výšce písma zkratka přetekla pod hlavičku,
            // kde ji řezala dělicí čára a začátky svislých čar mřížky.
            var vyskaBloku = cislo.Height + denVTydnu.Height;
            var horniOkraj = vyskaMesicu + ((VyskaHlavicky - vyskaMesicu - vyskaBloku) / 2);

            if (popsatCislo)
            {
                // Vždy vystředěné na svůj den — je to datum, musí sedět na dni, kterému
                // patří. Při širším kroku smí přečnívat, protože sousední dny popisek nemají.
                dc.DrawText(cislo, new Point(x + ((SirkaDne - cislo.Width) / 2), horniOkraj));
            }

            if (zobrazitZkratky && denVTydnu.Width <= SirkaDne)
            {
                dc.DrawText(
                    denVTydnu,
                    new Point(x + ((SirkaDne - denVTydnu.Width) / 2), horniOkraj + cislo.Height));
            }
        }
    }

    private void VykresliPruhy(DrawingContext dc, List<ZakazkaViewModel> zakazky)
    {
        var sirkaOsy = Math.Max(1, PocetDnu) * SirkaDne;

        for (var radek = 0; radek < zakazky.Count; radek++)
        {
            var zakazka = zakazky[radek];
            var jeVybrana = ReferenceEquals(zakazka, VybranaZakazka);

            if (jeVybrana)
            {
                dc.DrawRectangle(
                    StetecVybranyRadek,
                    null,
                    new Rect(0, VyskaHlavicky + (radek * VyskaRadku), sirkaOsy, VyskaRadku));
            }

            VykresliSpojniceUseku(dc, zakazka, radek);

            var vypln = zakazka.MaKolizi ? StetecPruhKolize : StetecPruh;
            var pero = jeVybrana
                ? PeroVyber
                : zakazka.MaKolizi ? PeroPruhKolize : PeroPruh;

            // Název nese jen první úsek; opakovat ho na každé části by osu zaplevelilo.
            Rect? obdelnikProPopisek = null;

            foreach (var usek in zakazka.Useky.OrderBy(u => u.DatumOd))
            {
                var obdelnik = ObdelnikUseku(usek, radek);
                if (obdelnik.Width <= 0)
                {
                    continue;
                }

                var pracovniUseky = PracovniPodUseky(usek);

                // Výplň jen v pracovních dnech, obrys přes celý úsek. Nad víkendem
                // a svátkem tak zůstane jen „skořápka“ a prosvítá jí podbarvení osy —
                // z pruhu je pak vidět, že se v ten den nepracuje.
                VykresliVyplnUseku(dc, obdelnik, vypln, pracovniUseky);
                dc.DrawRoundedRectangle(null, pero, obdelnik, 3, 3);

                obdelnikProPopisek ??= obdelnik;
            }

            // Název leží mimo pruh, takže si s milníky nelezou do cesty; milníky se proto
            // kreslí až nad ním a nikdy je nic nepřekryje.
            if (obdelnikProPopisek is { } kam)
            {
                VykresliPopisek(dc, zakazka, kam);
            }

            VykresliMilniky(dc, zakazka, radek);
        }
    }

    /// <summary>Souvislé úseky pracovních dnů v rámci jednoho úseku zakázky.</summary>
    private List<Rozsah> PracovniPodUseky(UsekViewModel usek)
    {
        var kalendar = Kalendar;
        var vysledek = new List<Rozsah>();

        if (kalendar is null)
        {
            vysledek.Add(new Rozsah(usek.DatumOd, usek.DatumDo));
            return vysledek;
        }

        DateOnly? zacatek = null;

        for (var den = usek.DatumOd; den <= usek.DatumDo; den = den.AddDays(1))
        {
            if (kalendar.JePracovniDen(den))
            {
                zacatek ??= den;
                continue;
            }

            if (zacatek is { } od)
            {
                vysledek.Add(new Rozsah(od, den.AddDays(-1)));
                zacatek = null;
            }
        }

        if (zacatek is { } posledni)
        {
            vysledek.Add(new Rozsah(posledni, usek.DatumDo));
        }

        return vysledek;
    }

    private void VykresliVyplnUseku(
        DrawingContext dc, Rect obdelnik, Brush vypln, List<Rozsah> pracovniUseky)
    {
        // Ořez podle tvaru pruhu, aby výplň nepřečnívala přes zaoblené rohy.
        var tvarPruhu = new RectangleGeometry(obdelnik, 3, 3);
        tvarPruhu.Freeze();
        dc.PushClip(tvarPruhu);

        foreach (var rozsah in pracovniUseky)
        {
            var levy = XoveProDen(rozsah.Od);
            var pravy = XoveProDen(rozsah.Do) + SirkaDne;
            dc.DrawRectangle(vypln, null, new Rect(levy, obdelnik.Y, pravy - levy, obdelnik.Height));
        }

        dc.Pop();
    }

    /// <summary>
    /// Vykreslí název zakázky mimo pruh — přednostně těsně před jeho začátkem.
    /// </summary>
    /// <remarks>
    /// V pruhu název nešel umístit rozumně: bílý text zanikal nad nevyplněnými víkendy
    /// a s milníkem si vzájemně lezly do cesty, ať se kreslilo v jakémkoli pořadí.
    /// Mimo pruh je tmavý text na světlém podkladu a milníky zůstávají volné.
    /// </remarks>
    private void VykresliPopisek(DrawingContext dc, ZakazkaViewModel zakazka, Rect obdelnik)
    {
        const double Mezera = 6;

        var popisek = zakazka.MaKolizi ? $"⚠ {zakazka.Nazev}" : zakazka.Nazev;
        var stetec = zakazka.MaKolizi ? StetecPopiskuKolize : StetecPopisku;
        var text = VytvorText(popisek, 11, stetec, FontWeights.Normal);

        var levyOkrajOkna = VodorovnyPosun;
        var pravyOkrajOkna = SirkaViditelnehoOkna > 0
            ? VodorovnyPosun + SirkaViditelnehoOkna
            : Math.Max(1, PocetDnu) * SirkaDne;

        var y = obdelnik.Y + ((obdelnik.Height - text.Height) / 2);

        // Před pruhem, zprava zarovnané na jeho začátek.
        var pred = obdelnik.X - Mezera - text.Width;
        if (pred >= levyOkrajOkna)
        {
            dc.DrawText(text, new Point(pred, y));
            return;
        }

        // Nevejde se to vlevo (zakázka začíná u kraje osy nebo je odscrollovaná),
        // takže název jde za pruh.
        var za = obdelnik.Right + Mezera;
        if (za + text.Width <= pravyOkrajOkna)
        {
            dc.DrawText(text, new Point(za, y));
            return;
        }

        // Pruh zabírá celé okno — pak zbývá jen dovnitř, s podložkou pro kontrast.
        var uvnitr = new Point(Math.Max(obdelnik.X, levyOkrajOkna) + 5, y);
        var svetlyText = VytvorText(popisek, 11, StetecTextPruhu, FontWeights.Normal);
        svetlyText.MaxTextWidth = Math.Max(obdelnik.Width - 10, 1);
        svetlyText.MaxLineCount = 1;
        svetlyText.Trimming = TextTrimming.CharacterEllipsis;

        var podlozka = new Rect(
            uvnitr.X - 3,
            uvnitr.Y - 1,
            Math.Min(svetlyText.Width, svetlyText.MaxTextWidth) + 6,
            svetlyText.Height + 2);

        dc.DrawRoundedRectangle(StetecPodlozkaPopisku, null, podlozka, 2, 2);
        dc.DrawText(svetlyText, uvnitr);
    }

    /// <summary>
    /// Tenká linka přes pauzy mezi úseky, aby bylo poznat, že části patří jedné zakázce
    /// a nejde o dvě různé.
    /// </summary>
    private void VykresliSpojniceUseku(DrawingContext dc, ZakazkaViewModel zakazka, int radek)
    {
        if (zakazka.Useky.Count < 2)
        {
            return;
        }

        var serazene = zakazka.Useky.OrderBy(u => u.DatumOd).ToList();
        var y = VyskaHlavicky + (radek * VyskaRadku) + (VyskaRadku / 2);

        for (var i = 0; i < serazene.Count - 1; i++)
        {
            var konecPredchoziho = ObdelnikUseku(serazene[i], radek).Right;
            var zacatekDalsiho = ObdelnikUseku(serazene[i + 1], radek).Left;

            if (zacatekDalsiho > konecPredchoziho)
            {
                dc.DrawLine(PeroSpojnice, new Point(konecPredchoziho, y), new Point(zacatekDalsiho, y));
            }
        }
    }

    private void VykresliMilniky(DrawingContext dc, ZakazkaViewModel zakazka, int radek)
    {
        foreach (var milnik in zakazka.Milniky)
        {
            var stred = StredMilniku(milnik, radek);
            dc.DrawGeometry(StetecMilnik, PeroMilnik, TvarMilniku(stred));
        }
    }

    /// <summary>Kosočtverec se špičkami nahoru a dolů, vystředěný na den milníku.</summary>
    private static StreamGeometry TvarMilniku(Point stred)
    {
        var geometrie = new StreamGeometry();

        using (var kontext = geometrie.Open())
        {
            kontext.BeginFigure(new Point(stred.X, stred.Y - PolomerMilniku), true, true);
            kontext.LineTo(new Point(stred.X + PolomerMilniku, stred.Y), true, true);
            kontext.LineTo(new Point(stred.X, stred.Y + PolomerMilniku), true, true);
            kontext.LineTo(new Point(stred.X - PolomerMilniku, stred.Y), true, true);
        }

        geometrie.Freeze();
        return geometrie;
    }

    private Point StredMilniku(MilnikViewModel milnik, int radek) => new(
        ((milnik.Datum.DayNumber - PrvniDen.DayNumber) * SirkaDne) + (SirkaDne / 2),
        VyskaHlavicky + (radek * VyskaRadku) + (VyskaRadku / 2));

    private void VykresliDnesniDen(DrawingContext dc, double vyska)
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        var index = dnes.DayNumber - PrvniDen.DayNumber;
        if (index < 0 || index >= PocetDnu)
        {
            return;
        }

        // Až od spodního okraje hlavičky — přes datum vedená čára působila jako přeškrtnutí.
        // Dnešek je v hlavičce místo toho zvýrazněný barvou (viz VykresliHlavicku).
        var x = (index * SirkaDne) + (SirkaDne / 2);
        dc.DrawLine(PeroDnes, new Point(x, VyskaHlavicky), new Point(x, vyska));
    }

    private Rect ObdelnikUseku(UsekViewModel usek, int radek)
    {
        const double svisleOdsazeni = 5;

        var zacatek = (usek.DatumOd.DayNumber - PrvniDen.DayNumber) * SirkaDne;
        var sirka = usek.PocetDnu * SirkaDne;
        var y = VyskaHlavicky + (radek * VyskaRadku) + svisleOdsazeni;
        var vyska = VyskaRadku - (2 * svisleOdsazeni);

        return new Rect(zacatek, y, Math.Max(sirka, 0), Math.Max(vyska, 0));
    }

    /// <summary>
    /// Vysázené texty hlavičky. Sazba FormattedText je líná, ale při kreslení se vynutí
    /// a stojí většinu času vykreslení — čísla dnů a zkratky dnů se přitom pořád opakují,
    /// takže se vyplatí je držet.
    /// </summary>
    private static readonly Dictionary<(string Text, double Velikost, int Vaha, Brush Stetec), FormattedText>
        MezipametTextu = [];

    /// <summary>Zkratky dnů se spočítají jednou; ToTitleCase v každém vykreslení je zbytečný.</summary>
    private static readonly string[] ZkratkyDnu = Enumerable.Range(0, 7)
        .Select(d => Kultura.TextInfo.ToTitleCase(Kultura.DateTimeFormat.AbbreviatedDayNames[d]))
        .ToArray();

    /// <summary>Jednopísmenné zkratky pro oddálenou osu, kde se dvě písmena nevejdou.</summary>
    private static readonly string[] ZkratkyDnuKratke = ZkratkyDnu
        .Select(z => z[..1])
        .ToArray();

    private static string ZkratkaDne(DayOfWeek den) => ZkratkyDnu[(int)den];

    private static string ZkratkaDneKratka(DayOfWeek den) => ZkratkyDnuKratke[(int)den];

    private static FormattedText VytvorTextZMezipameti(
        string text, double velikost, Brush stetec, FontWeight vaha)
    {
        var klic = (text, velikost, vaha.ToOpenTypeWeight(), stetec);

        if (!MezipametTextu.TryGetValue(klic, out var vysazeny))
        {
            vysazeny = VytvorText(text, velikost, stetec, vaha);

            // Vynutíme sazbu hned, ať ji nezaplatí až první kreslení.
            _ = vysazeny.Width;
            MezipametTextu[klic] = vysazeny;
        }

        return vysazeny;
    }

    private static FormattedText VytvorText(string text, double velikost, Brush stetec, FontWeight vaha) =>
        new(
            text,
            Kultura,
            FlowDirection.LeftToRight,
            vaha == FontWeights.Bold ? PismoTucne
                : vaha == FontWeights.SemiBold ? PismoPolotucne
                : Pismo,
            velikost,
            stetec,
            96);

    #endregion

    #region Interakce myší

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pozice = e.GetPosition(this);

        if (_tazeni is not null)
        {
            AktualizujTazeni(pozice);
            return;
        }

        var zasah = ZjistiZasah(pozice);

        Cursor = zasah.Zona switch
        {
            Zona.LevyOkraj or Zona.PravyOkraj => Cursors.SizeWE,
            Zona.Telo => Cursors.SizeAll,
            Zona.Milnik => Cursors.Hand,
            _ => Cursors.Arrow,
        };

        ZobrazPopisek(zasah.Milnik);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ZobrazPopisek(null);
    }

    /// <summary>
    /// Otevře nebo zavře popisek milníku. Při přejezdu na jiný milník se popisek zavře
    /// a znovu otevře, aby se přesunul k novému místu.
    /// </summary>
    private void ZobrazPopisek(MilnikViewModel? milnik)
    {
        if (ReferenceEquals(milnik, _popsanyMilnik))
        {
            return;
        }

        _popsanyMilnik = milnik;
        _popisekMilniku.IsOpen = false;

        if (milnik is null)
        {
            return;
        }

        _popisekMilniku.Content = $"{milnik.Nazev}\n{milnik.Datum:d. M. yyyy}";
        _popisekMilniku.IsOpen = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var zasah = ZjistiZasah(e.GetPosition(this));
        VybranaZakazka = zasah.Zakazka;

        // Klik na milník otevírá jeho úpravu, netáhne se.
        if (zasah is { Zona: Zona.Milnik, Zakazka: { } zakazka, Milnik: { } milnik })
        {
            ZobrazPopisek(null);
            MilnikKliknut?.Invoke(this, new MilnikKliknutEventArgs(zakazka, milnik));
            e.Handled = true;
            return;
        }

        if (zasah.Usek is null || zasah.Zona is Zona.Zadna)
        {
            return;
        }

        var pozice = e.GetPosition(this);

        _tazeni = new TazeniStav
        {
            Zakazka = zasah.Zakazka!,
            Usek = zasah.Usek,
            Zona = zasah.Zona,
            VychoziX = pozice.X,
            VychoziY = pozice.Y,
            PuvodniOd = zasah.Usek.DatumOd,
            PuvodniDo = zasah.Usek.DatumDo,
        };

        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Pravé tlačítko jen přenese výběr, aby se kontextová nabídka vztahovala ke správné
    /// zakázce, úseku a dni. Samotnou nabídku pak zobrazí WPF.
    /// </summary>
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        // Během tažení se výběr nepřepíná — pravý klik uprostřed tažení by jinak
        // přehodil výběr na jinou zakázku, než se kterou uživatel právě hýbe.
        if (_tazeni is not null)
        {
            return;
        }

        var zasah = ZjistiZasah(e.GetPosition(this));

        VybranaZakazka = zasah.Zakazka;
        VybranyUsek = zasah.Usek;
        VybranyMilnik = zasah.Milnik;
        DenPodKurzorem = DenNaPozici(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_tazeni is null)
        {
            return;
        }

        var tazeni = _tazeni;
        _tazeni = null;
        ReleaseMouseCapture();

        // Do databáze se zapisuje až tady — během tažení by se jinak uložily desítky mezistavů.
        if (tazeni.PoradiZmeneno)
        {
            PoradiDotazeno?.Invoke(this, EventArgs.Empty);
        }
        else if (tazeni.Usek.DatumOd != tazeni.PuvodniOd || tazeni.Usek.DatumDo != tazeni.PuvodniDo)
        {
            UsekZmenen?.Invoke(this, new UsekZmenenEventArgs(tazeni.Zakazka, tazeni.Usek));
        }

        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (_tazeni is null)
        {
            return;
        }

        // Ztráta capture (Alt+Tab, jiné okno) vrátí termín na původní hodnotu,
        // aby nezůstal viset nedokončený posun, který se nikam neuloží.
        if (_tazeni.PoradiZmeneno)
        {
            // Pořadí se vrátit nedá — zakázky už jsou přeskládané, tak se to aspoň uloží,
            // aby stav v okně odpovídal databázi.
            PoradiDotazeno?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _tazeni.Usek.DatumOd = _tazeni.PuvodniOd;
            _tazeni.Usek.DatumDo = _tazeni.PuvodniDo;
        }

        _tazeni = null;
        InvalidateVisual();
    }

    private void AktualizujTazeni(Point pozice)
    {
        var tazeni = _tazeni!;

        if (tazeni.Rezim == RezimTazeni.Nerozhodnuto)
        {
            RozhodniRezim(tazeni, pozice);
        }

        if (tazeni.Rezim == RezimTazeni.Poradi)
        {
            AktualizujPoradi(tazeni, pozice);
            return;
        }

        if (tazeni.Rezim == RezimTazeni.Nerozhodnuto)
        {
            return;
        }

        var posunDnu = (int)Math.Round((pozice.X - tazeni.VychoziX) / SirkaDne);

        switch (tazeni.Zona)
        {
            case Zona.Telo:
                tazeni.Usek.DatumOd = tazeni.PuvodniOd.AddDays(posunDnu);
                tazeni.Usek.DatumDo = tazeni.PuvodniDo.AddDays(posunDnu);
                break;

            case Zona.LevyOkraj:
                // Začátek nesmí přeskočit konec — úsek má vždy aspoň jeden den.
                var novyOd = tazeni.PuvodniOd.AddDays(posunDnu);
                tazeni.Usek.DatumOd = novyOd > tazeni.PuvodniDo ? tazeni.PuvodniDo : novyOd;
                break;

            case Zona.PravyOkraj:
                var novyDo = tazeni.PuvodniDo.AddDays(posunDnu);
                tazeni.Usek.DatumDo = novyDo < tazeni.PuvodniOd ? tazeni.PuvodniOd : novyDo;
                break;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Rozhodne, jestli tažení mění termín, nebo pořadí. Rozhoduje se podle toho, kterým
    /// směrem uživatel vyjel dřív — teprve po pár pixelech, aby drobné chvění nerozhodovalo.
    /// </summary>
    private void RozhodniRezim(TazeniStav tazeni, Point pozice)
    {
        const double Prah = 4;

        var dx = Math.Abs(pozice.X - tazeni.VychoziX);
        var dy = Math.Abs(pozice.Y - tazeni.VychoziY);

        if (dx < Prah && dy < Prah)
        {
            return;
        }

        // Změna pořadí jen tehdy, když ji nastavení dovoluje; jinak zůstává termín.
        tazeni.Rezim = dy > dx && LzeMenitPoradi
            ? RezimTazeni.Poradi
            : RezimTazeni.Termin;
    }

    /// <summary>
    /// Přeskládá zakázky během svislého tažení. Řádky se posouvají hned, takže je vidět,
    /// kam zakázka spadne.
    /// </summary>
    private void AktualizujPoradi(TazeniStav tazeni, Point pozice)
    {
        var zakazky = AktualniZakazky();
        var puvodniIndex = zakazky.IndexOf(tazeni.Zakazka);
        if (puvodniIndex < 0)
        {
            return;
        }

        var cilovyIndex = Math.Clamp(
            (int)((pozice.Y - VyskaHlavicky) / VyskaRadku),
            0,
            zakazky.Count - 1);

        if (cilovyIndex == puvodniIndex)
        {
            return;
        }

        PoradiZmeneno?.Invoke(this, new PoradiZmenenoEventArgs(tazeni.Zakazka, cilovyIndex));
        tazeni.PoradiZmeneno = true;
        InvalidateVisual();
    }

    /// <summary>Den, na který ukazuje daná vodorovná pozice.</summary>
    private DateOnly DenNaPozici(Point pozice) => DenNaXove(pozice.X);

    /// <summary>Den na dané vodorovné souřadnici v ose. Používá zoom kolečkem myši.</summary>
    public DateOnly DenNaXove(double x) =>
        PrvniDen.AddDays(Math.Clamp((int)(x / SirkaDne), 0, Math.Max(PocetDnu - 1, 0)));

    /// <summary>Vodorovná souřadnice levého okraje daného dne.</summary>
    public double XoveProDen(DateOnly den) => (den.DayNumber - PrvniDen.DayNumber) * SirkaDne;

    private Zasah ZjistiZasah(Point pozice)
    {
        if (pozice.Y < VyskaHlavicky)
        {
            return Zasah.Zadny;
        }

        var radek = (int)((pozice.Y - VyskaHlavicky) / VyskaRadku);
        var zakazky = AktualniZakazky();
        if (radek < 0 || radek >= zakazky.Count)
        {
            return Zasah.Zadny;
        }

        var zakazka = zakazky[radek];

        // Milník leží nad pruhem, takže se testuje první.
        foreach (var milnik in zakazka.Milniky)
        {
            var stred = StredMilniku(milnik, radek);
            if (Math.Abs(pozice.X - stred.X) + Math.Abs(pozice.Y - stred.Y) <= PolomerMilniku)
            {
                return new Zasah(zakazka, null, milnik, Zona.Milnik);
            }
        }

        foreach (var usek in zakazka.Useky)
        {
            var obdelnik = ObdelnikUseku(usek, radek);
            if (!obdelnik.Contains(pozice))
            {
                continue;
            }

            // U úzkých pruhů by se úchopy okrajů překryly a tělo by nešlo chytit vůbec.
            if (obdelnik.Width < MinimalniSirkaProUchopy)
            {
                return new Zasah(zakazka, usek, null, Zona.Telo);
            }

            if (pozice.X - obdelnik.Left <= SirkaUchopuOkraje)
            {
                return new Zasah(zakazka, usek, null, Zona.LevyOkraj);
            }

            if (obdelnik.Right - pozice.X <= SirkaUchopuOkraje)
            {
                return new Zasah(zakazka, usek, null, Zona.PravyOkraj);
            }

            return new Zasah(zakazka, usek, null, Zona.Telo);
        }

        // Prázdné místo v řádku zakázky: zakázka se vybere, ale nic se netáhne.
        return new Zasah(zakazka, null, null, Zona.Zadna);
    }

    private readonly record struct Zasah(
        ZakazkaViewModel? Zakazka,
        UsekViewModel? Usek,
        MilnikViewModel? Milnik,
        Zona Zona)
    {
        public static Zasah Zadny => new(null, null, null, Zona.Zadna);
    }

    private enum Zona
    {
        Zadna,
        Telo,
        LevyOkraj,
        PravyOkraj,
        Milnik,
    }

    /// <summary>Co tažení dělá. Rozhodne se po prvních pixelech podle jeho směru.</summary>
    private enum RezimTazeni
    {
        Nerozhodnuto,
        Termin,
        Poradi,
    }

    private class TazeniStav
    {
        public required ZakazkaViewModel Zakazka { get; init; }

        public required UsekViewModel Usek { get; init; }

        public required Zona Zona { get; init; }

        public required double VychoziX { get; init; }

        public required double VychoziY { get; init; }

        public required DateOnly PuvodniOd { get; init; }

        public required DateOnly PuvodniDo { get; init; }

        public RezimTazeni Rezim { get; set; } = RezimTazeni.Nerozhodnuto;

        /// <summary>Změnilo tažení pořadí? Podle toho se po dotažení ukládá.</summary>
        public bool PoradiZmeneno { get; set; }
    }

    #endregion

    private static Brush Vytvor(string hex)
    {
        var stetec = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        stetec.Freeze();
        return stetec;
    }

    private static Pen VytvorPero(string hex, double tloustka)
    {
        var pero = new Pen(Vytvor(hex), tloustka);
        pero.Freeze();
        return pero;
    }

    private static Pen VytvorTeckovanePero(string hex, double tloustka)
    {
        var pero = new Pen(Vytvor(hex), tloustka)
        {
            // Délky jsou násobky tloušťky pera, proto 2 a 3 dávají tečku a mezeru ~2 a 3 px.
            DashStyle = new DashStyle([2, 3], 0),
            DashCap = PenLineCap.Flat,
        };

        pero.Freeze();
        return pero;
    }
}
