namespace Cortex.API.Services;

/// <summary>
/// Optional <strong>display name</strong> updates only: null, missing, empty, or whitespace means
/// leave the existing Cortex display name unchanged.
/// </summary>
public static class OptionalProfileFieldNormalization
{
    /// <summary>
    /// For display name: returns null when <paramref name="value"/> is null, empty, or whitespace.
    /// Otherwise returns the trimmed non-empty string.
    /// </summary>
    public static string? NormalizeOptionalProfileUpdate(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
