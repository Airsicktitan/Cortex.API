using System.Text.RegularExpressions;

namespace Cortex.API.Services;

/// <summary>
/// Conservative SAP-intent detection from ticket text when no catalog match exists.
/// Intake/readiness only — does not infer tables, fields, or business objects.
/// </summary>
public static class SapTicketSapIntentAnalyzer
{
    private static readonly Regex SapWord = new(@"\bSAP\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EccWord = new(@"\bECC\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex S4Standalone = new(@"\bS4\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns true when the ticket text plausibly references SAP work without naming a catalog object.
    /// </summary>
    public static bool HasSapIntent(string? combinedText)
    {
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return false;
        }

        var t = combinedText;
        var upper = t.ToUpperInvariant();

        if (SapWord.IsMatch(t))
        {
            return true;
        }

        if (upper.Contains("S/4", StringComparison.Ordinal) ||
            upper.Contains("S4HANA", StringComparison.Ordinal))
        {
            return true;
        }

        if (S4Standalone.IsMatch(t))
        {
            return true;
        }

        if (EccWord.IsMatch(t))
        {
            return true;
        }

        string[] phrases =
        [
            "SAP FIELD",
            "SAP TABLE",
            "SAP DATA",
            "SAP MASTER DATA",
            "SAP RECORD",
            "SAP TRANSACTION",
            "SAP CONFIGURATION",
        ];

        foreach (var p in phrases)
        {
            if (upper.Contains(p, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
