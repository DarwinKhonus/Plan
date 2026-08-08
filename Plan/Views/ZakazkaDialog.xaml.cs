using System.Windows;
using Plan.ViewModels;

namespace Plan.Views;

public partial class ZakazkaDialog : Window
{
    public ZakazkaDialog(ZakazkaEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public ZakazkaEditViewModel ViewModel { get; }

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.JePlatny)
        {
            return;
        }

        DialogResult = true;
    }
}
