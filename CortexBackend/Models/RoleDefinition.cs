using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Cortex.API.Models;

public class RoleDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PermissionsJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDateUtc { get; set; }

    [NotMapped]
    public List<string> Permissions
    {
        get => DeserializePermissions(PermissionsJson);
        set => PermissionsJson = SerializePermissions(value);
    }

    private static List<string> DeserializePermissions(string? json)
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

    private static string SerializePermissions(IEnumerable<string>? permissions)
    {
        var normalized = (permissions ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalized);
    }
}
