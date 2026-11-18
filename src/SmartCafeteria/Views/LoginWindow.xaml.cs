using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmartCafeteria.Core.Models;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class LoginWindow : Window
{
    public event EventHandler? LoginSucceeded;

    private LoginViewModel ViewModel => (LoginViewModel)DataContext;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        PasswordInput.PasswordChanged += (_, _) =>
        {
            ViewModel.Password = PasswordInput.Password;
        };
        viewModel.LoginSucceeded += (_, _) => LoginSucceeded?.Invoke(this, EventArgs.Empty);
        viewModel.PasswordChangeRequired += OnPasswordChangeRequired;
        Loaded += async (_, _) => await ViewModel.LoadSettingsAsync();
        Loaded += (_, _) => PlayEnter();
    }

    private async void OnPasswordChangeRequired(object? sender, User user)
    {
        var dialog = new ChangePasswordDialog(
            user.Username,
            (current, next) => ViewModel.ChangeRequiredPasswordAsync(user.Id, current, next))
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
            await ViewModel.CompleteLoginAfterPasswordChangeAsync(user);
    }

    /// <summary>Fades the card up on open; skipped when the OS asks for less animation.</summary>
    private void PlayEnter()
    {
        if (!SystemParameters.ClientAreaAnimation || Content is not UIElement root)
            return;

        var translate = new TranslateTransform(0, 16);
        root.RenderTransform = translate;

        var duration = new Duration(TimeSpan.FromMilliseconds(280));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        root.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, duration) { EasingFunction = ease });
    }

    private void PasswordInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (ViewModel.LoginCommand.CanExecute(null))
                ViewModel.LoginCommand.Execute(null);
        }
    }
}