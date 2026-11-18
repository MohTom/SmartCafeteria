using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartCafeteria.Core.Models;
public class CartItem : INotifyPropertyChanged
{
    public string CartId { get; set; } = string.Empty;
    public Product Product { get; set; } = null!;

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity != value)
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineTotal));
            }
        }
    }

    public List<CustomizationOption> SelectedCustomizations { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public decimal LineTotal => (Product.Price + SelectedCustomizations.Sum(c => c.Price)) * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}