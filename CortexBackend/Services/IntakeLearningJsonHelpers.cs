using System.Text.Json;

namespace Cortex.API.Services;

/// <summary>
/// Parses <see cref="Models.Ticket.AiTriageMissingDetailsJson"/> safely for reporting (invalid JSON ⇒ 0).
/// </summary>
public static class IntakeLearningMissingHintCounter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Counts non-empty strings in the JSON array; returns 0 for null, empty, or invalid payloads.
    /// </summary>
    public static int CountMissingHints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json, SerializerOptions);
            if (items is null)
            {
                return 0;
            }

            var n = 0;
            foreach (var s in items)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    n++;
                }
            }

            return n;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
