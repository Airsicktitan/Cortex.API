using Cortex.API.DTO;

namespace Cortex.API.Services;

public sealed class OwnerWorkloadPreviewService(
    IOwnerWorkloadScoringService ownerWorkloadScoringService) : IOwnerWorkloadPreviewService
{
    private const int MaxOwnerKeys = 10;

    public async Task<OwnerWorkloadPreviewResponse> GetSummariesAsync(
        OwnerWorkloadPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var rawKeys = request.OwnerKeys?
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxOwnerKeys)
            .ToList() ?? [];

        if (rawKeys.Count == 0)
        {
            return new OwnerWorkloadPreviewResponse();
        }

        var scores = await ownerWorkloadScoringService.GetScoresAsync(
            rawKeys,
            request.ExcludeTicketId,
            respectCurrentVisibility: true,
            cancellationToken);

        return new OwnerWorkloadPreviewResponse
        {
            Summaries = scores
                .Select(score => new OwnerWorkloadSummaryDto
                {
                    OwnerKey = score.OwnerKey,
                    ActiveTicketCount = score.ActiveTicketCount,
                    HighPriorityTicketCount = score.HighPriorityTicketCount,
                    AtRiskTicketCount = score.AtRiskTicketCount,
                    OutsideSlaOpenCount = score.OutsideSlaOpenCount,
                    SlaRiskTicketCount = score.SlaRiskTicketCount,
                    WorkloadScore = score.WorkloadScore
                })
                .ToList()
        };
    }
}
