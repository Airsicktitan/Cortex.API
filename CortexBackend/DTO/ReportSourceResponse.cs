namespace Cortex.API.DTO;

public class ReportSourceColumnResponse
{
    public required string Key { get; set; }
    public required string Label { get; set; }
}

public class ReportSourceResponse
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public required string Description { get; set; }
    public required IReadOnlyList<ReportSourceColumnResponse> Columns { get; set; }
}
