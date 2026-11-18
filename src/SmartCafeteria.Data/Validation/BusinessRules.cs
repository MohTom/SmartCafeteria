using System.Net.Mail;
using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Data.Validation;

/// <summary>
/// Business rules enforced below the UI so no caller (screen, import, future
/// API) can persist invalid menu, supplier or purchase-order data.
/// </summary>
public static class BusinessRules
{
    public const int MaxNameLength = 120;
    public const int MaxCategoryLength = 60;
    public const int MaxDescriptionLength = 500;
    public const int MaxPhoneLength = 30;
    public const int MaxAddressLength = 250;
    public const int MaxNotesLength = 500;

    public static void ValidateProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        RequireText(product.NameAr, "اسم المنتج", MaxNameLength);
        RequireOptionalText(product.NameEn, "اسم المنتج بالإنجليزية", MaxNameLength);
        RequireOptionalText(product.DescriptionAr, "وصف المنتج", MaxDescriptionLength);
        RequireOptionalText(product.DescriptionEn, "وصف المنتج بالإنجليزية", MaxDescriptionLength);
        RequireText(product.Category, "التصنيف", MaxCategoryLength);

        if (product.Price < 0)
            throw new ArgumentException("سعر المنتج لا يمكن أن يكون سالباً.", nameof(product));
        if (product.Inventory < 0)
            throw new ArgumentException("كمية المخزون لا يمكن أن تكون سالبة.", nameof(product));
        if (product.MaxInventory < 0)
            throw new ArgumentException("الحد الأقصى للمخزون لا يمكن أن يكون سالباً.", nameof(product));
        if (product.MaxInventory > 0 && product.Inventory > product.MaxInventory)
            throw new ArgumentException("كمية المخزون لا يمكن أن تتجاوز الحد الأقصى.", nameof(product));
    }

    public static void ValidateSupplier(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        RequireText(supplier.Name, "اسم المورد", MaxNameLength);
        RequireOptionalText(supplier.Phone, "رقم الهاتف", MaxPhoneLength);
        RequireOptionalText(supplier.Email, "البريد الإلكتروني", MaxNameLength);
        RequireOptionalText(supplier.Address, "العنوان", MaxAddressLength);
        RequireOptionalText(supplier.Notes, "الملاحظات", MaxNotesLength);

        if (!string.IsNullOrWhiteSpace(supplier.Email))
        {
            try
            {
                var address = new MailAddress(supplier.Email.Trim());
                if (!string.Equals(address.Address, supplier.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة.", nameof(supplier));
            }
            catch (FormatException)
            {
                throw new ArgumentException("صيغة البريد الإلكتروني غير صحيحة.", nameof(supplier));
            }
        }
    }

    public static void ValidatePurchaseOrder(PurchaseOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        RequireOptionalText(order.Notes, "الملاحظات", MaxNotesLength);

        if (order.Items == null || order.Items.Count == 0)
            throw new ArgumentException("أمر الشراء يجب أن يحتوي على بند واحد على الأقل.", nameof(order));
        if (order.Items.Any(i => i.Quantity <= 0))
            throw new ArgumentException("كمية كل بند في أمر الشراء يجب أن تكون أكبر من صفر.", nameof(order));
        if (order.Items.Any(i => i.UnitCost < 0))
            throw new ArgumentException("تكلفة الوحدة لا يمكن أن تكون سالبة.", nameof(order));
    }

    public static void ValidateSupplierPayment(SupplierPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        RequireOptionalText(payment.Notes, "الملاحظات", MaxNotesLength);

        if (payment.Amount <= 0)
            throw new ArgumentException("مبلغ الدفعة يجب أن يكون أكبر من صفر.", nameof(payment));
    }

    private static void RequireText(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} مطلوب.", label);
        if (value.Trim().Length > maxLength)
            throw new ArgumentException($"{label} أطول من الحد المسموح ({maxLength} حرفاً).", label);
    }

    private static void RequireOptionalText(string? value, string label, int maxLength)
    {
        if (!string.IsNullOrEmpty(value) && value.Trim().Length > maxLength)
            throw new ArgumentException($"{label} أطول من الحد المسموح ({maxLength} حرفاً).", label);
    }
}
