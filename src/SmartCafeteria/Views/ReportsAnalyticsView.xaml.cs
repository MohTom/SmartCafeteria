using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SmartCafeteria.ViewModels;

namespace SmartCafeteria.Views;

public partial class ReportsAnalyticsView : UserControl
{
    public ReportsAnalyticsView()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider?.GetService<ReportsAnalyticsViewModel>();
        Loaded += async (_, _) =>
        {
            if (DataContext is ReportsAnalyticsViewModel vm)
                await vm.LoadAsync();
        };
    }
}
