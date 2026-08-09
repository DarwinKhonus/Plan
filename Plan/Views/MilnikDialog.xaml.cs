using System.Windows;
using System.Windows.Controls;
using Plan.ViewModels;

namespace Plan.Views;

/// <summary>
/// Přidání i úprava milníku. V režimu úpravy jde milník i smazat, takže se nemusí
/// mazat a znovu zakládat kvůli změně data.
/// </summary>
public partial class MilnikDialog : Window
{
    private MilnikDialog(DateOnly datum, string nazev, string nazevZakazky, bool jeUprava)
    {
        InitializeComponent();

        Title = jeUprava ? "Úprava milníku" : "Nový milník";
        UlozitButton.Content = jeUprava ? "Uložit" : "Přidat";
        SmazatButton.Visibility = jeUprava ? Visibility.Visible : Visibility.Collapsed;
        ZakazkaText.Text = nazevZakazky;

        NazevBox.Text = nazev;
        NazevBox.SelectAll();
        DatumPicker.SelectedDate = datum.ToDateTime(TimeOnly.MinValue);

        Aktualizuj();
    }

    public static MilnikDialog Novy(DateOnly datum, string nazevZakazky) =>
        new(datum, string.Empty, nazevZakazky, jeUprava: false);

    public static MilnikDialog Uprava(MilnikViewModel milnik, string nazevZakazky) =>
        new(milnik.Datum, milnik.Nazev, nazevZakazky, jeUprava: true);

    public string Nazev => NazevBox.Text.Trim();

    public DateOnly Datum => DateOnly.FromDateTime(DatumPicker.SelectedDate ?? DateTime.Today);

    /// <summary>Uživatel v režimu úpravy zvolil smazání.</summary>
    public bool MaSmazat { get; private set; }

    private void Vstup_Zmenen(object sender, TextChangedEventArgs e) => Aktualizuj();

    private void Datum_Zmeneno(object sender, SelectionChangedEventArgs e) => Aktualizuj();

    private void Aktualizuj() =>
        UlozitButton.IsEnabled =
            !string.IsNullOrWhiteSpace(NazevBox.Text) && DatumPicker.SelectedDate.HasValue;

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        if (UlozitButton.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void Smazat_Click(object sender, RoutedEventArgs e)
    {
        var odpoved = MessageBox.Show(
            this,
            $"Opravdu smazat milník „{NazevBox.Text}“?",
            "Smazání milníku",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (odpoved == MessageBoxResult.Yes)
        {
            MaSmazat = true;
            DialogResult = true;
        }
    }
}
