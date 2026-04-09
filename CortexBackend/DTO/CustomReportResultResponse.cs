namespace Cortex.API.DTO;

public class CustomReportResultResponse
{
    public required string ReportName { get; set; }
    public required IReadOnlyList<string> Columns { get; set; }
    public required IReadOnlyList<Dictionary<string, object?>> Rows { get; set; }
    public DateTime GeneratedDateUtc { get; set; }
    public bool IsTruncated { get; set; }
}
