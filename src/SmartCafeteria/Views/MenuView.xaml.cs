using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class MenuView : UserControl
{
    private MenuViewModel? _menu;
    private CartViewModel? _cart;

    public MenuView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _menu = DataContext as MenuViewModel;
        if (Window.GetWindow(this)?.DataContext is MainViewModel main)
            _cart = main.CartViewModel;
        if (_menu != null && IsVisible)
            SearchBox.Focus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _menu = null;
        _cart = null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_menu != null && _menu.ShowCustomizationDialog)
        {
            if (e.Key == Key.Escape)
            {
                _menu.CloseCustomizationCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                _menu.AddToCartCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        // Require Ctrl+F5 for keyboard checkout to avoid accidental orders.
        if (e.Key == Key.F5 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _cart != null)
        {
            _cart.PlaceOrderCommand.Execute(null);
            e.Handled = true;
        }
    }
}