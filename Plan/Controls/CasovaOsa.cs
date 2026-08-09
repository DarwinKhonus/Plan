using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
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
    // Poloprůhledný, aby přes zvýrazněný řádek zůstalo vidět podbarvení víkendů a svátků.
    private static readonly Brush StetecVybranyRadek = Vytvor("#2E3B82C4");

    // Ztmavení nepracovních dnů uvnitř pruhu. Termín je souvislý, ale z osy má být vidět,
    // ve kterých dnech se na zakázce nepracuje.
    private static readonly Brush StetecNepracovniVPruhu = Vytvor("#38000000");
    private static readonly Brush StetecMilnik = Vytvor("#F5B301");
    private static readonly Pen PeroMilnik = VytvorPero("#8A6100", 1.5);

    // Přerušovaná linka přes pauzu mezi úseky jedné zakázky.
    private static readonly Pen PeroSpojnice = VytvorTeckovanePero("#7E93AC", 1.5);

    private static readonly Typeface Pismo = new("Segoe UI");

    private TazeniStav? _tazeni;

    static CasovaOsa()
    {
        FocusableProperty.OverrideMetadata(typeof(CasovaOsa), new FrameworkPropertyMetadata(true));
    }

    public event EventHandler<UsekZmenenEventArgs>? UsekZmenen;

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

        for (var i = 0; i < PocetDnu; i++)
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
            var sirkaUseku = (i - zacatekUseku) * SirkaDne;

            if (text.Width + 8 <= sirkaUseku)
            {
                dc.DrawText(text, new Point((zacatekUseku * SirkaDne) + 6, (vyskaMesicu - text.Height) / 2));
            }

            zacatekUseku = i;
        }

        // Pás s čísly dnů a zkratkou dne v týdnu.
        var dnes = DateOnly.FromDateTime(DateTime.Today);

        for (var i = 0; i < PocetDnu; i++)
        {
            var den = PrvniDen.AddDays(i);
            var x = i * SirkaDne;
            var jeDnes = den == dnes;
            var jeNepracovni = Kalendar is not null && !Kalendar.JePracovniDen(den);

            var stetec = jeDnes
                ? StetecDnes
                : jeNepracovni ? StetecTextSlaby : StetecText;
            var vaha = jeDnes ? FontWeights.Bold : FontWeights.Normal;

            var cislo = VytvorText(den.Day.ToString(kultura), 11, stetec, vaha);

            // Dvoupísmenná zkratka; nejkratší tvar je v češtině nejednoznačný
            // („P“ je pondělí i pátek, „S“ středa i sobota).
            var zkratka = kultura.TextInfo.ToTitleCase(
                kultura.DateTimeFormat.AbbreviatedDayNames[(int)den.DayOfWeek]);
            var denVTydnu = VytvorText(
                zkratka, 9, jeDnes ? StetecDnes : StetecTextSlaby, vaha);

            // Obě řádky se vysází jako blok vystředěný ve zbytku hlavičky. Pevná odsazení
            // tu dřív byla — při skutečné výšce písma zkratka přetekla pod hlavičku,
            // kde ji řezala dělicí čára a začátky svislých čar mřížky.
            var vyskaBloku = cislo.Height + denVTydnu.Height;
            var horniOkraj = vyskaMesicu + ((VyskaHlavicky - vyskaMesicu - vyskaBloku) / 2);

            if (cislo.Width <= SirkaDne)
            {
                dc.DrawText(cislo, new Point(x + ((SirkaDne - cislo.Width) / 2), horniOkraj));
            }

            if (denVTydnu.Width <= SirkaDne)
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
            var prvni = true;

            foreach (var usek in zakazka.Useky.OrderBy(u => u.DatumOd))
            {
                var obdelnik = ObdelnikUseku(usek, radek);
                if (obdelnik.Width <= 0)
                {
                    continue;
                }

                dc.DrawRoundedRectangle(vypln, pero, obdelnik, 3, 3);
                VykresliNepracovniDnyVUseku(dc, usek, obdelnik);

                if (prvni)
                {
                    VykresliPopisek(dc, zakazka, obdelnik);
                    prvni = false;
                }
            }

            VykresliMilniky(dc, zakazka, radek);
        }
    }

    private void VykresliPopisek(DrawingContext dc, ZakazkaViewModel zakazka, Rect obdelnik)
    {
        var popisek = zakazka.MaKolizi ? $"⚠ {zakazka.Nazev}" : zakazka.Nazev;
        var text = VytvorText(popisek, 11, StetecTextPruhu, FontWeights.Normal);

        var dostupnaSirka = obdelnik.Width - 10;
        if (dostupnaSirka <= 12)
        {
            return;
        }

        text.MaxTextWidth = dostupnaSirka;
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(obdelnik.X + 5, obdelnik.Y + ((obdelnik.Height - text.Height) / 2)));
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

    /// <summary>
    /// Ztmaví dny uvnitř úseku, ve kterých se nepracuje. Úsek zůstává souvislý, protože
    /// souvislý je — jen z něj má být poznat, které dny se do odhadu hodin nepočítají.
    /// </summary>
    private void VykresliNepracovniDnyVUseku(DrawingContext dc, UsekViewModel usek, Rect obdelnik)
    {
        var kalendar = Kalendar;
        if (kalendar is null)
        {
            return;
        }

        // Ořez podle tvaru pruhu, aby ztmavení nepřečnívalo přes zaoblené rohy.
        var tvarPruhu = new RectangleGeometry(obdelnik, 3, 3);
        tvarPruhu.Freeze();
        dc.PushClip(tvarPruhu);

        for (var den = usek.DatumOd; den <= usek.DatumDo; den = den.AddDays(1))
        {
            if (kalendar.JePracovniDen(den))
            {
                continue;
            }

            var x = (den.DayNumber - PrvniDen.DayNumber) * SirkaDne;
            dc.DrawRectangle(
                StetecNepracovniVPruhu,
                null,
                new Rect(x, obdelnik.Y, SirkaDne, obdelnik.Height));
        }

        dc.Pop();
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

    private static FormattedText VytvorText(string text, double velikost, Brush stetec, FontWeight vaha) =>
        new(
            text,
            CultureInfo.GetCultureInfo("cs-CZ"),
            FlowDirection.LeftToRight,
            new Typeface(Pismo.FontFamily, FontStyles.Normal, vaha, FontStretches.Normal),
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

        ToolTip = zasah.Milnik is { } milnik
            ? $"{milnik.Nazev} — {milnik.Datum:d. M. yyyy}"
            : null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var zasah = ZjistiZasah(e.GetPosition(this));
        VybranaZakazka = zasah.Zakazka;

        // Milníky se netáhnou — přesouvají se smazáním a novým přidáním.
        if (zasah.Usek is null || zasah.Zona is Zona.Zadna or Zona.Milnik)
        {
            return;
        }

        _tazeni = new TazeniStav
        {
            Zakazka = zasah.Zakazka!,
            Usek = zasah.Usek,
            Zona = zasah.Zona,
            VychoziX = e.GetPosition(this).X,
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
        if (tazeni.Usek.DatumOd != tazeni.PuvodniOd || tazeni.Usek.DatumDo != tazeni.PuvodniDo)
        {
            UsekZmenen?.Invoke(this, new UsekZmenenEventArgs(tazeni.Zakazka, tazeni.Usek));
        }

        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // Ztráta capture (Alt+Tab, jiné okno) vrátí termín na původní hodnotu,
        // aby nezůstal viset nedokončený posun, který se nikam neuloží.
        if (_tazeni is null)
        {
            return;
        }

        _tazeni.Usek.DatumOd = _tazeni.PuvodniOd;
        _tazeni.Usek.DatumDo = _tazeni.PuvodniDo;
        _tazeni = null;
        InvalidateVisual();
    }

    private void AktualizujTazeni(Point pozice)
    {
        var tazeni = _tazeni!;
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

    /// <summary>Den, na který ukazuje daná vodorovná pozice.</summary>
    private DateOnly DenNaPozici(Point pozice) =>
        PrvniDen.AddDays(Math.Clamp((int)(pozice.X / SirkaDne), 0, Math.Max(PocetDnu - 1, 0)));

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

    private class TazeniStav
    {
        public required ZakazkaViewModel Zakazka { get; init; }

        public required UsekViewModel Usek { get; init; }

        public required Zona Zona { get; init; }

        public required double VychoziX { get; init; }

        public required DateOnly PuvodniOd { get; init; }

        public required DateOnly PuvodniDo { get; init; }
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
