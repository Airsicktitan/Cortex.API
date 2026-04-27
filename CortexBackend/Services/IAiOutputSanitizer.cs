namespace Cortex.API.Services;

public interface IAiOutputSanitizer
{
    string? Sanitize(string? input);

    List<string> SanitizeList(IEnumerable<string>? values);
}
