using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Cortex.API.Models;

public class ArchiveConfiguration
{
    public int Id { get; set; }
    public int ArchiveAfterDays { get; set; }
    public string EligibleStatusesJson { get; set; } = "[]";

    [NotMapped]
    public List<string> EligibleStatuses
    {
        get => DeserializeEligibleStatuses(EligibleStatusesJson);
        set => EligibleStatusesJson = SerializeEligibleStatuses(value);
    }

    private static List<string> DeserializeEligibleStatuses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string SerializeEligibleStatuses(IEnumerable<string>? statuses)
    {
        var normalizedStatuses = (statuses ?? [])
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalizedStatuses);
    }
}
