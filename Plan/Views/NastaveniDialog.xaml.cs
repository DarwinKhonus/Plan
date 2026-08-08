using System.Windows;
using Plan.ViewModels;

namespace Plan.Views;

public partial class NastaveniDialog : Window
{
    public NastaveniDialog(NastaveniViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public NastaveniViewModel ViewModel { get; }

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.JePlatny)
        {
            return;
        }

        DialogResult = true;
    }
}
