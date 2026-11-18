using System.Windows;
using SmartCafeteria.Activator.ViewModels;

namespace SmartCafeteria.Activator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ActivatorViewModel();
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ActivatorViewModel viewModel)
            return;

        viewModel.Password = KeyPasswordBox.Password;
        viewModel.GenerateCommand.Execute(null);
    }
}
