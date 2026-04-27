using System.Text.RegularExpressions;

namespace Cortex.API.Services;

public sealed partial class AiOutputSanitizer : IAiOutputSanitizer
{
    public string? Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim();
        value = ConnectionStringLikePattern().Replace(value, "[sensitive value hidden]");
        value = AuthHeaderPattern().Replace(value, "Authorization: [sensitive value hidden]");
        value = JwtPattern().Replace(value, "[sensitive value hidden]");
        value = ApiKeyPattern().Replace(value, "[sensitive value hidden]");
        value = SqlPattern().Replace(value, "[technical detail hidden]");
        value = DboPattern().Replace(value, "[internal reference hidden]");
        value = StackTracePattern().Replace(value, "[technical detail hidden]");
        value = GuidPattern().Replace(value, "[internal reference hidden]");
        value = HostPattern().Replace(value, "[internal reference hidden]");
        return value;
    }

    public List<string> SanitizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(Sanitize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    [GeneratedRegex(@"(?i)\b(?:password|pwd|user\s*id|uid|server|data\s*source|initial\s*catalog)\s*=\s*[^;,\n]+")]
    private static partial Regex ConnectionStringLikePattern();

    [GeneratedRegex(@"(?i)\bauthorization\s*:\s*bearer\s+[a-z0-9\-_\.=]+")]
    private static partial Regex AuthHeaderPattern();

    [GeneratedRegex(@"\beyJ[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]{10,}\b")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"(?i)\b(?:api[_-]?key|openai[_-]?api[_-]?key|managementclientsecret|clientsecret)\s*[:=]\s*['""]?[a-z0-9\-_]{8,}['""]?")]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"(?i)\b(select|insert|update|delete|merge|create\s+procedure|exec(?:ute)?)\b[\s\S]{0,220}")]
    private static partial Regex SqlPattern();

    [GeneratedRegex(@"(?i)\bdbo\.[a-z0-9_\[\]\.]+")]
    private static partial Regex DboPattern();

    [GeneratedRegex(@"(?i)\b(?:at\s+[a-z0-9_\.`]+(?:\.[a-z0-9_`]+)*\s*\([^\)]*\)|[a-z]:\\[^\n]+)")]
    private static partial Regex StackTracePattern();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}\-(?:[0-9a-fA-F]{4}\-){3}[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"(?i)\b(?:[a-z0-9\-]+\.)*(?:database\.windows\.net|azurewebsites\.net|azurecontainerapps\.io)\b|localhost:\d{2,5}")]
    private static partial Regex HostPattern();
}
