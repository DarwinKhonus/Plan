using System.Windows;
using Plan.ViewModels;

namespace Plan.Views;

public partial class InfoDialog : Window
{
    public InfoDialog(InfoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
