using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Tests.TestDoubles;

public sealed class CapturingFakeTicketCreationApplicationService : ITicketCreationApplicationService
{
    public Exception? ThrowOnCreate { get; set; }

    public List<CreateTicketRequest> CapturedRequests { get; } = [];

    public TicketResponse? NextResponse { get; set; }

    public Task<TicketResponse> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnCreate is not null)
        {
            throw ThrowOnCreate;
        }

        CapturedRequests.Add(request);
        return Task.FromResult(
            NextResponse ?? new TicketResponse
            {
                Id = "T-TEST-1",
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                BoardId = request.BoardId ?? 1,
                BoardName = "Test board",
                Priority = request.Priority ?? "Medium",
                Status = "New",
                ApprovalStatus = ApprovalStatus.PendingApproval,
                CreatedDate = DateTime.UtcNow,
            });
    }
}
