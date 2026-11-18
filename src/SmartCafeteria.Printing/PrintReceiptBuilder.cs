using SmartCafeteria.Core.Models;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;
using System.Text.Json;

namespace SmartCafeteria.Printing;

public class PrintReceiptBuilder
{
    public static TicketPrintModel BuildReceipt(Order order, AppSettings settings, string? sectionName = null, List<OrderItem>? sectionItems = null, bool minimal = false)
    {
        var model = new TicketPrintModel
        {
            Title = settings.CafeteriaNameEn,
            Subtitle = settings.CafeteriaAddress,
            Phone = $"TEL: {settings.CafeteriaPhone}",
            TicketNumber = order.Code,
            Date = order.Timestamp.ToString("yyyy/MM/dd HH:mm"),
            PaymentMethod = GetPaymentMethodArabic(order.PaymentMethod),
            OrderType = order.OrderType.ToArabic(),
            CurrencySymbolAr = settings.CurrencyAr,
            PickupTime = order.PickupTime ?? string.Empty,
            OrderId = order.Id.Length > 14 ? order.Id[..14] : order.Id,
            IsMinimal = minimal
        };

        var items = sectionItems ?? order.Items;
        foreach (var item in items)
        {
            var customizationText = "";
            if (!string.IsNullOrEmpty(item.SelectedCustomizationsJson))
            {
                try
                {
                    var customs = JsonSerializer.Deserialize<List<CustomizationOption>>(item.SelectedCustomizationsJson);
                    if (customs != null && customs.Any())
                    {
                        customizationText = string.Join(", ", customs.Select(c => c.NameAr));
                    }
                }
                catch (Exception ex)
                {
                    // A malformed customization snapshot must not block the
                    // ticket; the item still prints without its options.
                    System.Diagnostics.Trace.TraceWarning(
                        "Customizations for order item {0} could not be parsed: {1}",
                        item.ProductNameAr, ex);
                }
            }

            // The name is printed on the right margin, the price in its own column
            // on the left margin, so the prices of the ticket line up.
            model.Lines.Add(new TicketLine
            {
                Text = $"{item.Quantity}x {item.ProductNameAr}",
                PriceText = minimal ? string.Empty : $"{item.ProductPrice:F2}",
                IsBold = false
            });

            if (!string.IsNullOrEmpty(customizationText))
            {
                model.Lines.Add(new TicketLine { Text = $"  + {customizationText}", IsSmall = true });
            }
        }

        model.Total = sectionItems != null
            ? items.Sum(i => i.Quantity * i.ProductPrice + i.CustomizationsTotal)
            : order.Total;
        if (!string.IsNullOrEmpty(sectionName))
        {
            if (minimal)
                model.SectionName = sectionName;
            else
                model.Title += $" - {sectionName}";
        }
        return model;
    }

    /// <summary>
    /// Builds the cashier copy of a double receipt: the cafeteria name, the
    /// <c>إيصال استلام</c> label, the receipt number, the order type, the meals
    /// and the total. Every other header field, the prices of the items and their
    /// customizations are left out, so the copy stays as short as possible.
    /// </summary>
    public static TicketPrintModel BuildCashierSummaryReceipt(Order order, AppSettings settings)
    {
        var model = new TicketPrintModel
        {
            Title = settings.CafeteriaNameEn,
            SummaryTitle = "إيصال استلام",
            TicketNumber = order.Code,
            OrderType = order.OrderType.ToArabic(),
            Total = order.Total,
            CurrencySymbolAr = settings.CurrencyAr,
            IsCashierSummary = true
        };

        foreach (var item in order.Items)
        {
            model.Lines.Add(new TicketLine { Text = $"{item.Quantity}x {item.ProductNameAr}" });
        }

        return model;
    }

    /// <summary>Prints an order exactly the way <see cref="AppSettings.PrintMethod"/> states:
    /// twice for customer+cashier, sections only (no customer receipt), a single
    /// receipt, or otherwise full receipt plus one kitchen receipt per section.</summary>
    public static async Task PrintOrderWithMethodAsync(
        IPrintService printService,
        Order order,
        AppSettings settings,
        IReadOnlyDictionary<string, string>? categoryNames = null)
    {
        if (settings.PrintMethod == PrintMethod.TotalReceiptTwice)
        {
            await printService.PrintReceiptAsync(order, settings);
            await printService.PrintCashierSummaryAsync(order, settings);
            return;
        }

        if (settings.PrintMethod == PrintMethod.SectionsOnly)
        {
            await PrintSectionReceiptsAsync(printService, order, settings, categoryNames, minimal: true);
            return;
        }

        await printService.PrintReceiptAsync(order, settings);
        if (settings.PrintMethod == PrintMethod.SingleReceipt)
            return;
        await PrintSectionReceiptsAsync(printService, order, settings, categoryNames, minimal: true);
    }

    private static async Task PrintSectionReceiptsAsync(
        IPrintService printService,
        Order order,
        AppSettings settings,
        IReadOnlyDictionary<string, string>? categoryNames = null,
        bool minimal = false)
    {
        foreach (var group in order.Items.GroupBy(i => i.ProductCategory))
        {
            var sectionName = group.Key;
            if (categoryNames != null && categoryNames.TryGetValue(group.Key, out var display))
                sectionName = display;
            else
                sectionName = GetCategoryNameArabic(group.Key);
            await printService.PrintKitchenReceiptAsync(order, sectionName, group.ToList(), settings, minimal);
        }
    }

    public static List<TicketPrintModel> BuildReceiptsForOrder(Order order, AppSettings settings, PrintMethod method)
    {
        var results = new List<TicketPrintModel>();

        switch (method)
        {
            case PrintMethod.FullReceiptAndSectionReceipts:
                results.Add(BuildReceipt(order, settings));

                var grouped = order.Items.GroupBy(i => i.ProductCategory);
                foreach (var group in grouped)
                {
                    results.Add(BuildReceipt(order, settings, GetCategoryNameArabic(group.Key), group.ToList(), minimal: true));
                }
                break;

            case PrintMethod.TotalReceiptTwice:
                var customerReceipt = BuildReceipt(order, settings);
                customerReceipt.Subtitle += " - نسخة العميل";
                results.Add(customerReceipt);

                results.Add(BuildCashierSummaryReceipt(order, settings));
                break;

            case PrintMethod.SingleReceipt:
                results.Add(BuildReceipt(order, settings));
                break;

            case PrintMethod.SectionsOnly:
                foreach (var group in order.Items.GroupBy(i => i.ProductCategory))
                {
                    results.Add(BuildReceipt(order, settings, GetCategoryNameArabic(group.Key), group.ToList(), minimal: true));
                }
                break;
        }

        return results;
    }

    private static string GetPaymentMethodArabic(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Bankak => "بنكك",
            _ => method.ToString()
        };
    }

    private static string GetCategoryNameArabic(string category)
    {
        return category switch
        {
            "HotDrinks" => "مشروبات ساخنة",
            "ColdDrinks" => "مشروبات باردة",
            "Breakfast" => "إفطار",
            "Sandwiches" => "ساندويتشات",
            "SweetPies" => "حلويات",
            "SavoryPies" => "فطائر مالحة",
            "Desserts" => "حلوى",
            "Juices" => "عصائر",
            "Grills" => "مشويات",
            _ => category
        };
    }
}
