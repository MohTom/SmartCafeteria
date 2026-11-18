using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _licenseTimer;
    private bool _licenseLockOpen;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        _licenseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _licenseTimer.Tick += OnLicenseTimerTick;
        Closed += (_, _) => _licenseTimer.Stop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        DependencyPropertyDescriptor
            .FromProperty(ContentControl.ContentProperty, typeof(ContentControl))
            .AddValueChanged(ContentHost, (_, _) => PlayContentEnter());

        PlayContentEnter();

        if (DataContext is MainViewModel viewModel)
            await viewModel.InitializeAsyncSafe();

        _licenseTimer.Start();
    }

    private async void OnLicenseTimerTick(object? sender, EventArgs e)
    {
        if (_licenseLockOpen || DataContext is not MainViewModel viewModel)
            return;

        await viewModel.RefreshLicenseStatusAsync();
        if (!viewModel.Activation.IsLocked)
            return;

        _licenseLockOpen = true;
        _licenseTimer.Stop();

        try
        {
            var lockWindow = new ActivationLockWindow(viewModel.Activation) { Owner = this };
            var activated = lockWindow.ShowDialog() == true;

            await viewModel.RefreshLicenseStatusAsync();
            if (!activated || viewModel.Activation.IsLocked)
            {
                Application.Current.Shutdown();
                return;
            }

            _licenseTimer.Start();
        }
        finally
        {
            _licenseLockOpen = false;
        }
    }

    /// <summary>
    /// Fades and lifts the newly navigated screen into place. Only opacity and a
    /// render transform change, so the layout never reflows and automation still
    /// sees the same elements. Skipped when the OS asks for less animation.
    /// </summary>
    private void PlayContentEnter()
    {
        if (!SystemParameters.ClientAreaAnimation)
            return;

        var translate = new TranslateTransform(0, 12);
        ContentHost.RenderTransform = translate;

        var duration = new Duration(TimeSpan.FromMilliseconds(240));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        ContentHost.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, duration) { EasingFunction = ease });
    }
}
