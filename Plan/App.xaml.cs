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

        try
        {
            dbFactory.MigrateDatabase();
        }
        catch (Exception ex)
        {
            // Bez databáze nemá aplikace co dělat — jediný případ, kdy start ukončíme.
            MessageBox.Show(
                $"Nepodařilo se otevřít databázi.\n\n{AppPaths.DatabaseFile}\n\n{ex.Message}",
                "Plan",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        var viewModel = new MainViewModel(
            new ZakazkyRepository(dbFactory),
            new NastaveniRepository(dbFactory),
            new UpdateChecker());

        MainWindow = new MainWindow(viewModel);
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
