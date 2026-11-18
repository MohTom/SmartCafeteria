using QuestPDF.Infrastructure;

namespace SmartCafeteria.Services;

/// <summary>Central QuestPDF configuration.
/// Assumption (business decision): the cafeteria operates below QuestPDF's
/// Community revenue threshold. If that ever changes, replace the license type
/// here only; no report code changes are needed.</summary>
public static class QuestPdfConfiguration
{
    private static int _configured;

    public static void EnsureCommunityLicense()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 0)
            QuestPDF.Settings.License = LicenseType.Community;
    }
}
