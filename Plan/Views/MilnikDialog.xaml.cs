using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Plan.Views;

/// <summary>
/// Zeptá se na název milníku. Datum je dané dnem, na který uživatel klikl,
/// takže se v dialogu jen zobrazuje.
/// </summary>
public partial class MilnikDialog : Window
{
    public MilnikDialog(DateOnly datum, string nazevZakazky)
    {
        InitializeComponent();

        Datum = datum;
        DatumText.Text = string.Format(
            CultureInfo.GetCultureInfo("cs-CZ"),
            "{0:d. M. yyyy} · {1}",
            datum,
            nazevZakazky);
    }

    public DateOnly Datum { get; }

    public string Nazev => NazevBox.Text.Trim();

    private void NazevBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UlozitButton.IsEnabled = !string.IsNullOrWhiteSpace(NazevBox.Text);

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NazevBox.Text))
        {
            DialogResult = true;
        }
    }
}
