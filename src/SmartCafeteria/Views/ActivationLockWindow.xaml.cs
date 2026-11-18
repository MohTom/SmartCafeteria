using System.ComponentModel;
using System.Windows;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class ActivationLockWindow : Window
{
    private readonly ActivationViewModel _viewModel;
    private bool _allowClose;

    public ActivationLockWindow(ActivationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += OnClosing;
        viewModel.Activated += OnActivated;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.RefreshStatusAsync();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (_allowClose)
            return;

        _allowClose = true;
        _viewModel.Activated -= OnActivated;

        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            if (IsVisible)
                Close();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        _viewModel.Activated -= OnActivated;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        Application.Current.Shutdown();
    }
}
