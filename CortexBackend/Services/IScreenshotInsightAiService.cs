using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>Vision-based screenshot insight for approvers. Advisory only.</summary>
public interface IScreenshotInsightAiService
{
    Task<ScreenshotInsightResponse> AnalyzeAsync(
        string ticketTitle,
        IReadOnlyList<(string FileName, string ContentType, byte[] Content)> images,
        CancellationToken cancellationToken = default);
}
