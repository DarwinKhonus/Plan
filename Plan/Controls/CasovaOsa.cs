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

    private static readonly Typeface Pismo = new("Segoe UI");

    private TazeniStav? _tazeni;

    static CasovaOsa()
    {
        FocusableProperty.OverrideMetadata(typeof(CasovaOsa), new FrameworkPropertyMetadata(true));
    }

    public event EventHandler<TerminZmenenEventArgs>? TerminZmenen;

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

            if (ReferenceEquals(zakazka, VybranaZakazka))
            {
                dc.DrawRectangle(
                    StetecVybranyRadek,
                    null,
                    new Rect(0, VyskaHlavicky + (radek * VyskaRadku), sirkaOsy, VyskaRadku));
            }

            var obdelnik = ObdelnikPruhu(zakazka, radek);
            if (obdelnik.Width <= 0)
            {
                continue;
            }

            var jeVybrana = ReferenceEquals(zakazka, VybranaZakazka);
            var vypln = zakazka.MaKolizi ? StetecPruhKolize : StetecPruh;
            var pero = jeVybrana
                ? PeroVyber
                : zakazka.MaKolizi ? PeroPruhKolize : PeroPruh;

            dc.DrawRoundedRectangle(vypln, pero, obdelnik, 3, 3);

            var popisek = zakazka.MaKolizi ? $"⚠ {zakazka.Nazev}" : zakazka.Nazev;
            var text = VytvorText(popisek, 11, StetecTextPruhu, FontWeights.Normal);

            var dostupnaSirka = obdelnik.Width - 10;
            if (dostupnaSirka > 12)
            {
                text.MaxTextWidth = dostupnaSirka;
                text.MaxLineCount = 1;
                text.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(text, new Point(obdelnik.X + 5, obdelnik.Y + ((obdelnik.Height - text.Height) / 2)));
            }
        }
    }

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

    private Rect ObdelnikPruhu(ZakazkaViewModel zakazka, int radek)
    {
        const double svisleOdsazeni = 5;

        var zacatek = (zakazka.DatumOd.DayNumber - PrvniDen.DayNumber) * SirkaDne;
        var sirka = zakazka.PocetDnu * SirkaDne;
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

        Cursor = ZjistiZonu(pozice) switch
        {
            (not null, Zona.LevyOkraj) or (not null, Zona.PravyOkraj) => Cursors.SizeWE,
            (not null, Zona.Telo) => Cursors.SizeAll,
            _ => Cursors.Arrow,
        };
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var (zakazka, zona) = ZjistiZonu(e.GetPosition(this));
        if (zakazka is null)
        {
            VybranaZakazka = null;
            return;
        }

        VybranaZakazka = zakazka;

        _tazeni = new TazeniStav
        {
            Zakazka = zakazka,
            Zona = zona,
            VychoziX = e.GetPosition(this).X,
            PuvodniOd = zakazka.DatumOd,
            PuvodniDo = zakazka.DatumDo,
        };

        CaptureMouse();
        e.Handled = true;
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
        if (tazeni.Zakazka.DatumOd != tazeni.PuvodniOd || tazeni.Zakazka.DatumDo != tazeni.PuvodniDo)
        {
            TerminZmenen?.Invoke(this, new TerminZmenenEventArgs(
                tazeni.Zakazka,
                tazeni.Zakazka.DatumOd,
                tazeni.Zakazka.DatumDo));
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

        _tazeni.Zakazka.DatumOd = _tazeni.PuvodniOd;
        _tazeni.Zakazka.DatumDo = _tazeni.PuvodniDo;
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
                tazeni.Zakazka.DatumOd = tazeni.PuvodniOd.AddDays(posunDnu);
                tazeni.Zakazka.DatumDo = tazeni.PuvodniDo.AddDays(posunDnu);
                break;

            case Zona.LevyOkraj:
                // Začátek nesmí přeskočit konec — zakázka má vždy aspoň jeden den.
                var novyOd = tazeni.PuvodniOd.AddDays(posunDnu);
                tazeni.Zakazka.DatumOd = novyOd > tazeni.PuvodniDo ? tazeni.PuvodniDo : novyOd;
                break;

            case Zona.PravyOkraj:
                var novyDo = tazeni.PuvodniDo.AddDays(posunDnu);
                tazeni.Zakazka.DatumDo = novyDo < tazeni.PuvodniOd ? tazeni.PuvodniOd : novyDo;
                break;
        }

        InvalidateVisual();
    }

    private (ZakazkaViewModel? Zakazka, Zona Zona) ZjistiZonu(Point pozice)
    {
        if (pozice.Y < VyskaHlavicky)
        {
            return (null, Zona.Zadna);
        }

        var radek = (int)((pozice.Y - VyskaHlavicky) / VyskaRadku);
        var zakazky = AktualniZakazky();
        if (radek < 0 || radek >= zakazky.Count)
        {
            return (null, Zona.Zadna);
        }

        var zakazka = zakazky[radek];
        var obdelnik = ObdelnikPruhu(zakazka, radek);
        if (!obdelnik.Contains(pozice))
        {
            return (null, Zona.Zadna);
        }

        // U úzkých pruhů by se úchopy okrajů překryly a tělo by nešlo chytit vůbec.
        if (obdelnik.Width < MinimalniSirkaProUchopy)
        {
            return (zakazka, Zona.Telo);
        }

        if (pozice.X - obdelnik.Left <= SirkaUchopuOkraje)
        {
            return (zakazka, Zona.LevyOkraj);
        }

        if (obdelnik.Right - pozice.X <= SirkaUchopuOkraje)
        {
            return (zakazka, Zona.PravyOkraj);
        }

        return (zakazka, Zona.Telo);
    }

    private enum Zona
    {
        Zadna,
        Telo,
        LevyOkraj,
        PravyOkraj,
    }

    private class TazeniStav
    {
        public required ZakazkaViewModel Zakazka { get; init; }

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
