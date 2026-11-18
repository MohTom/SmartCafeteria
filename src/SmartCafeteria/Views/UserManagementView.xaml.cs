using System.Windows.Controls;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is UserManagementViewModel vm)
                await vm.LoadUsersAsync();
        };
    }

    private void NewPasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.NewPassword = NewPasswordInput.Password;
    }
}