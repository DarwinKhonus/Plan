using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Plan.Data;
using Plan.Services;
using Plan.ViewModels;
using Plan.Views;

namespace Plan;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        NastavCeskeFormatovani();
        DispatcherUnhandledException += NaNeosetrenouVyjimku;

        var dbFactory = new PlanDbFactory();

        // Okno se otevře i při potížích s databází. Dřív se aplikace ukončila, jenže tím
        // se nikdy nespustila kontrola aktualizací — a u staré verze nad novou databází
        // je nabídka novější verze jediná užitečná věc, kterou lze uživateli nabídnout.
        var stav = dbFactory.Priprav(out var podrobnosti);

        var viewModel = new MainViewModel(
            new ZakazkyRepository(dbFactory),
            new NastaveniRepository(dbFactory),
            new UpdateChecker(),
            new UpdateDownloader());

        viewModel.NastavStavDatabaze(stav, podrobnosti);

        MainWindow = new MainWindow(viewModel, stav == StavDatabaze.Ok);
        MainWindow.Show();
    }

    /// <summary>
    /// WPF jinak formátuje data podle výchozí kultury XAML (en-US) bez ohledu na systém,
    /// takže by se v tabulce zobrazovalo 3/9/2026 místo 9. 3. 2026.
    /// </summary>
    private static void NastavCeskeFormatovani()
    {
        var kultura = CultureInfo.GetCultureInfo("cs-CZ");
        CultureInfo.DefaultThreadCurrentCulture = kultura;
        CultureInfo.DefaultThreadCurrentUICulture = kultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(kultura.IetfLanguageTag)));
    }

    private void NaNeosetrenouVyjimku(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Došlo k neočekávané chybě:\n\n{e.Exception.Message}",
            "Plan",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Chyba v jedné akci nesmí shodit celou aplikaci a připravit uživatele o rozdělanou práci.
        e.Handled = true;
    }
}
