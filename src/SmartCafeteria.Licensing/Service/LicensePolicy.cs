using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Models;

namespace SmartCafeteria.Licensing.Service;

public static class LicensePolicy
{
    public const int TrialDays = 14;

    public static DateTimeOffset EffectiveNow(DateTimeOffset now, DateTimeOffset lastSeenUtc) =>
        now > lastSeenUtc ? now : lastSeenUtc;

    public static DateTimeOffset TrialEndUtc(DateTimeOffset firstRunUtc) =>
        firstRunUtc.AddDays(TrialDays);

    public static DateTimeOffset MonthlyExpiry(DateTimeOffset issueUtc) =>
        issueUtc.AddMonths(1);

    public static DateTimeOffset EndOfUtcDay(DateTimeOffset value) =>
        new(DateTime.SpecifyKind(value.UtcDateTime.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc));

    public static LicenseStatus Evaluate(LicenseState state, HardwareIdentity hardware, DateTimeOffset effectiveNow)
    {
        if (!string.Equals(state.DeviceId, hardware.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return Locked(
                hardware.DeviceId,
                LockReason.HardwareMismatch,
                "الترخيص الحالي غير مرتبط بهذا الجهاز.");
        }

        if (state.Kind == LicenseKind.Permanent)
        {
            return new LicenseStatus(
                LicenseKind.Permanent,
                false,
                false,
                LockReason.None,
                null,
                0,
                hardware.DeviceId,
                "مفعل بشكل دائم");
        }

        if (state.Kind == LicenseKind.Monthly)
        {
            if (!state.ExpiresUtc.HasValue)
            {
                return Locked(
                    hardware.DeviceId,
                    LockReason.StateCorrupt,
                    "بيانات الترخيص الشهري غير مكتملة.");
            }

            if (effectiveNow <= EndOfUtcDay(state.ExpiresUtc.Value))
            {
                return new LicenseStatus(
                    LicenseKind.Monthly,
                    false,
                    false,
                    LockReason.None,
                    state.ExpiresUtc,
                    0,
                    hardware.DeviceId,
                    $"مفعل شهرياً حتى {state.ExpiresUtc.Value.ToLocalTime():yyyy-MM-dd}");
            }

            return Locked(
                hardware.DeviceId,
                LockReason.LicenseExpired,
                "انتهت صلاحية الترخيص. يلزم التجديد.");
        }

        var trialEnd = TrialEndUtc(state.FirstRunUtc);
        if (effectiveNow <= trialEnd)
        {
            var remaining = Math.Max(0, (int)Math.Ceiling((trialEnd - effectiveNow).TotalDays));
            return new LicenseStatus(
                LicenseKind.None,
                true,
                false,
                LockReason.None,
                trialEnd,
                remaining,
                hardware.DeviceId,
                remaining == 0 ? "آخر يوم من الفترة التجريبية" : $"نسخة تجريبية — متبقي {remaining} يوم");
        }

        return Locked(
            hardware.DeviceId,
            LockReason.TrialExpired,
            "انتهت الفترة التجريبية. يلزم تفعيل البرنامج.");
    }

    public static LicenseStatus Locked(string deviceId, LockReason reason, string message) =>
        new(LicenseKind.None, false, true, reason, null, 0, deviceId, message);
}
