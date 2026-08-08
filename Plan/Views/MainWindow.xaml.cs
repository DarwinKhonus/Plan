using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Plan.Controls;
using Plan.Services;
using Plan.ViewModels;

namespace Plan.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PozadavekNaPridani += (_, _) => PridatZakazku();
        viewModel.PozadavekNaUpravu += (_, _) => UpravitZakazku();
        viewModel.PozadavekNaSmazani += (_, _) => SmazatZakazku();
        viewModel.PozadavekNaNastaveni += (_, _) => OtevritNastaveni();
        viewModel.PozadavekNaSkokNaDnesek += (_, _) => SkocitNaDnesek();

        VerzeText.Text = string.Format(
            CultureInfo.CurrentCulture,
            "verze {0}",
            UpdateChecker.AktualniVerze.ToString(3));

        Loaded += NaNacteni;
    }

    private async void NaNacteni(object sender, RoutedEventArgs e)
    {
        await _viewModel.NactiAsync();
        SkocitNaDnesek();

        // Kontrola aktualizací se pouští až po vykreslení okna a nikdy se na ni nečeká.
        _ = _viewModel.ZkontrolujAktualizaceAsync();
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

    private async void Osa_TerminZmenen(object? sender, TerminZmenenEventArgs e)
    {
        await _viewModel.UlozPosunutyTerminAsync(e.Zakazka);
    }

    private void Tabulka_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.VybranaZakazka is not null)
        {
            UpravitZakazku();
        }
    }

    /// <summary>Odscrolluje časovou osu tak, aby byl dnešek zhruba uprostřed viditelné části.</summary>
    private void SkocitNaDnesek()
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);
        var odsazeni = (dnes.DayNumber - _viewModel.PrvniDen.DayNumber) * _viewModel.SirkaDne;
        var cil = odsazeni - (OsaScroll.ViewportWidth / 2);

        OsaScroll.ScrollToHorizontalOffset(Math.Max(cil, 0));
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Odkaz vede vždy na release stránku vlastního repozitáře, kterou sestavil UpdateChecker.
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
