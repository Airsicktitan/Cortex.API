using System.Text.Json;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.API.Tests;

public class TicketTriagePersistenceTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ApplyPersistedResult_KeepsCanonicalFieldsUnchanged_WhenAutoApplyIsDisabled(
        bool advisoryOnlyMode,
        bool suggestionOnlyMode)
    {
        var ticket = new Ticket
        {
            Id = "T-5001",
            Title = "Review intake",
            Description = "Approval workflow needs attention.",
            Priority = "Medium",
            Status = "New",
            ApprovalStatus = ApprovalStatus.PendingApproval,
            BoardId = 1,
            CreatedBy = 10,
            CreatedDate = DateTime.UtcNow,
        };

        var result = new TicketTriageGenerateResponse
        {
            Summary = "Clarify the routing outcome and approval owner.",
            SuggestedPriority = "High",
            PriorityReason = "The request is blocking approval throughput.",
            SuggestedStatus = "In Review",
            MissingDetails = ["Confirm the affected queue.", "Name the approver."],
            PotentialSlaRisk = "Medium",
            SlaRiskReason = "Clarification gaps will slow the first review cycle.",
        };

        var vocabulary = new TicketTriageVocabularySnapshot
        {
            Priorities =
            [
                new TicketTriagePriorityOption("Medium", 24, 12),
                new TicketTriagePriorityOption("High", 8, 4),
            ],
            Statuses =
            [
                new TicketTriageStatusOption("New", null, 1),
                new TicketTriageStatusOption("In Review", null, 2),
            ],
        };

        var aiSettings = new AiSettingsConfiguration
        {
            AdvisoryOnlyMode = advisoryOnlyMode,
            SuggestionOnlyMode = suggestionOnlyMode,
        };

        TicketTriagePersistence.ApplyPersistedResult(
            ticket,
            result,
            vocabulary,
            aiSettings,
            NullLogger.Instance);

        Assert.Equal("Medium", ticket.Priority);
        Assert.Equal("New", ticket.Status);
        Assert.Equal("High", ticket.AiTriageSuggestedPriority);
        Assert.Equal("In Review", ticket.AiTriageSuggestedStatus);
        Assert.Equal(
            ["Confirm the affected queue.", "Name the approver."],
            JsonSerializer.Deserialize<List<string>>(ticket.AiTriageMissingDetailsJson!) ?? []);
    }
}
