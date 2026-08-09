using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Plan.Controls;
using Plan.Services;
using Plan.ViewModels;

namespace Plan.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly bool _databazeJePouzitelna;

    public MainWindow(MainViewModel viewModel, bool databazeJePouzitelna = true)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _databazeJePouzitelna = databazeJePouzitelna;
        DataContext = viewModel;

        viewModel.PozadavekNaInfo += (_, _) => ZobrazitInfo();
        viewModel.PozadavekNaPridani += (_, _) => PridatZakazku();
        viewModel.PozadavekNaUpravu += (_, _) => UpravitZakazku();
        viewModel.PozadavekNaSmazani += (_, _) => SmazatZakazku();
        viewModel.PozadavekNaNastaveni += (_, _) => OtevritNastaveni();
        viewModel.PozadavekNaSkokNaDnesek += (_, _) => SkocitNaDnesek();
        viewModel.PozadavekNaStazeniAktualizace += (_, _) => StahnoutAInstalovat();

        VerzeText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "verze {0}",
            UpdateChecker.AktualniVerze.ToString(3));

        Loaded += NaNacteni;
    }

    private async void NaNacteni(object sender, RoutedEventArgs e)
    {
        // Kontrola aktualizací jde první a nezávisle na datech. Když je databáze
        // z novější verze, načtení dat selže — a to je právě chvíle, kdy uživatel
        // nabídku aktualizace potřebuje nejvíc.
        _ = _viewModel.ZkontrolujAktualizaceAsync();

        // Nad nepoužitelnou databází se o načtení ani nepokoušíme — skončilo by to
        // nesrozumitelnou SQL chybou. Srozumitelnou hlášku už drží ViewModel.
        if (!_databazeJePouzitelna)
        {
            return;
        }

        try
        {
            await _viewModel.NactiAsync();
            SkocitNaDnesek();
        }
        catch (Exception ex)
        {
            _viewModel.OhlasChybuNacteni(ex);
        }
    }

    private void ZobrazitInfo()
    {
        if (_viewModel.VybranaZakazka is not { } vybrana)
        {
            return;
        }

        var info = new InfoViewModel(vybrana, _viewModel.Zakazky, _viewModel.Kalendar);
        new InfoDialog(info) { Owner = this }.ShowDialog();
    }

    /// <summary>
    /// WPF při pravém kliknutí řádek sám nevybere, takže by se kontextová nabídka
    /// vztahovala k dříve vybrané zakázce. Výběr proto přeneseme ručně.
    /// </summary>
    private void Tabulka_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NajdiPredka<DataGridRow>(e.OriginalSource as DependencyObject) is { } radek)
        {
            radek.IsSelected = true;
        }
    }

    private static T? NajdiPredka<T>(DependencyObject? prvek)
        where T : DependencyObject
    {
        while (prvek is not null and not T)
        {
            // VisualTreeHelper neprojde přes obsah šablon typu ContentPresenter u ne-vizuálů,
            // proto fallback na logického rodiče.
            prvek = prvek is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(prvek)
                : LogicalTreeHelper.GetParent(prvek);
        }

        return prvek as T;
    }

    private async void PridatZakazku()
    {
        var editace = new ZakazkaEditViewModel();
        var dialog = new ZakazkaDialog(editace) { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.PridejAsync(editace);
        }
    }

    private async void UpravitZakazku()
    {
        if (_viewModel.VybranaZakazka is not { } vybrana)
        {
            return;
        }

        var editace = new ZakazkaEditViewModel(vybrana);
        var dialog = new ZakazkaDialog(editace) { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.UpravAsync(vybrana.Id, editace);
        }
    }

    private async void SmazatZakazku()
    {
        if (_viewModel.VybranaZakazka is not { } vybrana)
        {
            return;
        }

        var odpoved = MessageBox.Show(
            this,
            $"Opravdu smazat zakázku „{vybrana.Nazev}“?",
            "Smazání zakázky",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (odpoved == MessageBoxResult.Yes)
        {
            await _viewModel.SmazAsync(vybrana.Id);
        }
    }

    private async void OtevritNastaveni()
    {
        var editace = new NastaveniViewModel(_viewModel.Kalendar.Nastaveni.Kopie());
        var dialog = new NastaveniDialog(editace) { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.UlozNastaveniAsync(editace.ToNastaveni());
        }
    }

    private async void Osa_UsekZmenen(object? sender, UsekZmenenEventArgs e)
    {
        await _viewModel.UlozPosunutyUsekAsync(e.Usek);
    }

    /// <summary>
    /// Zpřístupní jen ty položky, které dávají v místě kliknutí smysl — rozdělit jde
    /// jen uvnitř úseku, odebrat milník jen nad milníkem.
    /// </summary>
    private void Osa_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var maZakazku = _viewModel.VybranaZakazka is not null;
        var usek = Osa.VybranyUsek;
        var den = Osa.DenPodKurzorem;

        PolozkaUpravit.IsEnabled = maZakazku;
        PolozkaSmazat.IsEnabled = maZakazku;

        // Rozdělit nelze na prvním dni úseku — jedna z částí by byla prázdná.
        PolozkaRozdelit.IsEnabled = usek is not null && den > usek.DatumOd && den <= usek.DatumDo;

        PolozkaOdstranitUsek.IsEnabled = usek is not null
            && _viewModel.VybranaZakazka?.JeRozdelena == true;

        PolozkaPridatMilnik.IsEnabled = maZakazku;
        PolozkaOdebratMilnik.IsEnabled = Osa.VybranyMilnik is not null;
    }

    private void Nabidka_Info(object sender, RoutedEventArgs e) => ZobrazitInfo();

    private void Nabidka_Upravit(object sender, RoutedEventArgs e) => UpravitZakazku();

    private void Nabidka_Smazat(object sender, RoutedEventArgs e) => SmazatZakazku();

    private void Nabidka_PridatZakazku(object sender, RoutedEventArgs e) => PridatZakazku();

    private async void Nabidka_RozdelitZde(object sender, RoutedEventArgs e)
    {
        if (Osa.VybranyUsek is { } usek)
        {
            await _viewModel.RozdelUsekAsync(usek, Osa.DenPodKurzorem);
        }
    }

    private async void Nabidka_OdstranitUsek(object sender, RoutedEventArgs e)
    {
        if (Osa.VybranyUsek is { } usek)
        {
            await _viewModel.SmazUsekAsync(usek);
        }
    }

    private async void Nabidka_PridatMilnik(object sender, RoutedEventArgs e)
    {
        if (_viewModel.VybranaZakazka is not { } zakazka)
        {
            return;
        }

        var dialog = MilnikDialog.Novy(Osa.DenPodKurzorem, zakazka.Nazev);
        dialog.Owner = this;

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.PridejMilnikAsync(zakazka.Id, dialog.Datum, dialog.Nazev);
        }
    }

    /// <summary>Levý klik na milník v ose otevře jeho úpravu.</summary>
    private async void Osa_MilnikKliknut(object? sender, MilnikKliknutEventArgs e) =>
        await UpravMilnikAsync(e.Zakazka, e.Milnik);

    private async void Nabidka_OdebratMilnik(object sender, RoutedEventArgs e)
    {
        if (Osa.VybranyMilnik is { } milnik)
        {
            await _viewModel.SmazMilnikAsync(milnik);
        }
    }

    /// <summary>
    /// Dvojklik upraví to, na co uživatel klikl — na řádku milníku milník, jinak zakázku.
    /// </summary>
    private async void Tabulka_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Dvojklik do záhlaví nebo mimo řádky nemá co upravovat.
        if (NajdiPredka<DataGridRow>(e.OriginalSource as DependencyObject) is null)
        {
            return;
        }

        if (_viewModel.VybranyRadek is { Milnik: { } milnik } radek)
        {
            await UpravMilnikAsync(radek.Zakazka, milnik);
        }
        else if (_viewModel.VybranaZakazka is not null)
        {
            UpravitZakazku();
        }
    }

    private async void Tabulka_UpravitMilnik(object sender, RoutedEventArgs e)
    {
        if (_viewModel.VybranyRadek is { Milnik: { } milnik } radek)
        {
            await UpravMilnikAsync(radek.Zakazka, milnik);
        }
    }

    /// <summary>Otevře dialog úpravy milníku a uloží výsledek. Používá osa i tabulka.</summary>
    private async Task UpravMilnikAsync(ZakazkaViewModel zakazka, MilnikViewModel milnik)
    {
        var dialog = MilnikDialog.Uprava(milnik, zakazka.Nazev);
        dialog.Owner = this;

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.MaSmazat)
        {
            await _viewModel.SmazMilnikAsync(milnik);
        }
        else
        {
            await _viewModel.UpravMilnikAsync(milnik, dialog.Datum, dialog.Nazev);
        }
    }

    /// <summary>
    /// Hlásí ViewModelu šířku viditelné části, aby osa vyplnila okno. Bere se ze
    /// ScrollChanged, ne ze SizeChanged — tam je ViewportWidth ještě před přeskládáním
    /// a vracela by hodnotu o krok pozadu.
    /// </summary>
    private void OsaScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || _viewModel.SirkaViditelneCasti == 0)
        {
            _viewModel.SirkaViditelneCasti = e.ViewportWidth;
        }

        // Osa podle posunu drží název měsíce v dohledu.
        Osa.VodorovnyPosun = e.HorizontalOffset;
    }

    /// <summary>
    /// Kolečko posouvá osu vodorovně, s Ctrl přibližuje. Svislé posouvání kolečkem by
    /// u osy nemělo smysl — řádků je málo, kdežto dnů hodně.
    /// </summary>
    private void OsaScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Priblizit(e.Delta, e.GetPosition(Osa).X);
        }
        else
        {
            OsaScroll.ScrollToHorizontalOffset(OsaScroll.HorizontalOffset - e.Delta);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Přiblíží nebo oddálí osu a udrží přitom den pod kurzorem na stejném místě —
    /// bez toho by zoom odskakoval a uživatel by ztrácel orientaci.
    /// </summary>
    private void Priblizit(int delta, double xVOse)
    {
        const double Krok = 1.2;

        var denPodKurzorem = Osa.DenNaXove(xVOse);
        var odsazeniVOkne = xVOse - OsaScroll.HorizontalOffset;

        var nova = delta > 0 ? _viewModel.SirkaDne * Krok : _viewModel.SirkaDne / Krok;
        _viewModel.SirkaDne = Math.Clamp(
            nova, MainViewModel.MinimalniSirkaDne, MainViewModel.MaximalniSirkaDne);

        // Přepočet šířky osy proběhne až po přeměření, takže korekci posunu odložíme.
        Dispatcher.BeginInvoke(
            () => OsaScroll.ScrollToHorizontalOffset(
                Math.Max(Osa.XoveProDen(denPodKurzorem) - odsazeniVOkne, 0)),
            DispatcherPriority.Loaded);
    }

    /// <summary>Odscrolluje časovou osu tak, aby byl dnešek zhruba uprostřed viditelné části.</summary>
    private void SkocitNaDnesek()
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        var odsazeni = (dnes.DayNumber - _viewModel.PrvniDen.DayNumber) * _viewModel.SirkaDne;
        var cil = odsazeni - (OsaScroll.ViewportWidth / 2);

        OsaScroll.ScrollToHorizontalOffset(Math.Max(cil, 0));
    }

    /// <summary>
    /// Stáhne instalátor, spustí ho a ukončí aplikaci. Instalátor běží v tichém režimu
    /// a přepisuje soubor běžící aplikace, takže se musí uvolnit — proto to ukončení.
    /// </summary>
    private async void StahnoutAInstalovat()
    {
        var cesta = await _viewModel.StahniAktualizaciAsync();
        if (cesta is null)
        {
            // Důvod už drží ViewModel a je vidět v pruhu, odkaz na stránku zůstává k dispozici.
            return;
        }

        var odpoved = MessageBox.Show(
            this,
            "Aktualizace je připravená. Aplikace se teď zavře a spustí se instalace.\n\n"
            + "Pokračovat?",
            "Aktualizace aplikace Plan",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information,
            MessageBoxResult.OK);

        if (odpoved != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            // /SILENT ukáže jen průběh bez průvodce; instalace jde do stejné složky
            // a zachová stávající zástupce.
            Process.Start(new ProcessStartInfo(cesta)
            {
                Arguments = "/SILENT",
                UseShellExecute = true,
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Instalaci se nepodařilo spustit:\n\n{ex.Message}\n\nSoubor je uložený zde:\n{cesta}",
                "Plan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Odkaz vede vždy na release stránku vlastního repozitáře, kterou sestavil UpdateChecker.
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
