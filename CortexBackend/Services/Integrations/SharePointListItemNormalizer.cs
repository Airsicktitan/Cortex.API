using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

internal static class SharePointListItemNormalizer
{
    internal sealed record NormalizedRow(
        string ExternalItemId,
        string Title,
        string? ExternalUrl,
        string? Description,
        string? Status,
        string? Priority,
        string? Requester,
        string? AssignedTo,
        string? Department,
        string? Category,
        DateTime? DueDateUtc,
        DateTime? LastModifiedUtc,
        string RawJson);

    public static bool TryNormalize(
        JsonElement listItem,
        IReadOnlyList<ExternalFieldMapping> mappings,
        string? fallbackSourceUrl,
        out NormalizedRow? row,
        out string? error)
    {
        row = null;
        error = null;

        if (!listItem.TryGetProperty("id", out var idEl))
        {
            error = "List item missing id.";
            return false;
        }

        var externalId = idEl.ToString();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            error = "List item id is empty.";
            return false;
        }

        string? webUrl = null;
        if (listItem.TryGetProperty("webUrl", out var wu) && wu.ValueKind == JsonValueKind.String)
        {
            webUrl = wu.GetString();
        }

        DateTime? lastMod = null;
        if (listItem.TryGetProperty("lastModifiedDateTime", out var lm)
            && lm.ValueKind == JsonValueKind.String
            && DateTime.TryParse(lm.GetString(), out var lmDt))
        {
            lastMod = DateTime.SpecifyKind(lmDt, DateTimeKind.Utc);
        }

        if (!listItem.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            error = "List item missing fields.";
            return false;
        }

        var title = string.Empty;
        string? description = null;
        string? status = null;
        string? priority = null;
        string? requester = null;
        string? assignedTo = null;
        string? department = null;
        string? category = null;
        DateTime? due = null;
        string? evidenceUrl = null;

        foreach (var map in mappings)
        {
            if (map.CortexField == CortexField.Unknown)
            {
                continue;
            }

            if (!TryGetFieldValue(fields, map.ExternalFieldName, map.ExternalFieldKey, out var raw))
            {
                continue;
            }

            var text = JsonValueToDisplayString(raw);
            switch (map.CortexField)
            {
                case CortexField.Title:
                    if (!string.IsNullOrEmpty(text))
                    {
                        title = text;
                    }

                    break;
                case CortexField.Description:
                    description = text;
                    break;
                case CortexField.Status:
                    status = text;
                    break;
                case CortexField.Priority:
                    priority = text;
                    break;
                case CortexField.Requester:
                    requester = text;
                    break;
                case CortexField.Department:
                    department = text;
                    break;
                case CortexField.SynitiOwner:
                    assignedTo = text;
                    break;
                case CortexField.Category:
                    category = text;
                    break;
                case CortexField.DueDate:
                    due = TryParseDate(raw);
                    break;
                case CortexField.EvidenceUrl:
                    evidenceUrl = TryGetUrl(raw);
                    if (string.IsNullOrEmpty(evidenceUrl))
                    {
                        evidenceUrl = text;
                    }

                    break;
                case CortexField.BusinessOwner:
                    // No dedicated column on ExternalWorkItem; raw JSON still contains field data.
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = $"SharePoint item {externalId}";
        }

        var url = !string.IsNullOrWhiteSpace(webUrl)
            ? webUrl
            : !string.IsNullOrWhiteSpace(evidenceUrl)
                ? evidenceUrl
                : fallbackSourceUrl;

        string rawJson;
        try
        {
            rawJson = JsonSerializer.Serialize(listItem);
        }
        catch
        {
            rawJson = "{}";
        }

        row = new NormalizedRow(
            externalId,
            title.Trim(),
            url?.Trim(),
            description,
            status,
            priority,
            requester,
            assignedTo,
            department,
            category,
            due,
            lastMod,
            rawJson);
        return true;
    }

    private static bool TryGetFieldValue(JsonElement fields, string name, string? key, out JsonElement value)
    {
        foreach (var prop in fields.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(key) && string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? JsonValueToDisplayString(JsonElement el)
    {
        try
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    return el.GetString();
                case JsonValueKind.Number:
                    return el.ToString();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                case JsonValueKind.Object:
                    if (el.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                    {
                        return dn.GetString();
                    }

                    if (el.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String)
                    {
                        return em.GetString();
                    }

                    if (el.TryGetProperty("lookupValue", out var lv) && lv.ValueKind == JsonValueKind.String)
                    {
                        return lv.GetString();
                    }

                    return null;
                case JsonValueKind.Array:
                    return string.Join(
                        "; ",
                        el.EnumerateArray().Select(JsonValueToDisplayString).Where(s => !string.IsNullOrEmpty(s)));
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? TryParseDate(JsonElement el)
    {
        try
        {
            if (el.ValueKind == JsonValueKind.String
                && DateTime.TryParse(el.GetString(), out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? TryGetUrl(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String)
        {
            return el.GetString();
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("Url", out var u1) && u1.ValueKind == JsonValueKind.String)
            {
                return u1.GetString();
            }

            if (el.TryGetProperty("url", out var u2) && u2.ValueKind == JsonValueKind.String)
            {
                return u2.GetString();
            }
        }

        return null;
    }
}
