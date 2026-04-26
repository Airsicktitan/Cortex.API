using Cortex.API.Services;

namespace Cortex.API.Tests;

public class TicketTriageFallbackPolicyTests
{
    [Fact]
    public void Apply_UsesConfiguredFallbacks_AndPreservesSafeFields()
    {
        var fallbackPolicy = new TicketTriageFallbackPolicy();
        var validationResult = new TicketTriageValidatedResult
        {
            Summary = "Clarify the intake scope.",
            Priority = null,
            PriorityReason = null,
            Status = null,
            MissingDetails = ["Confirm the requester.", "Confirm the deadline."],
            PotentialSlaRisk = null,
            SlaRiskReason = null,
            IsValid = false,
            ValidationErrors =
            [
                "priority must match a configured value.",
                "status must match a configured value when statuses are configured.",
            ],
        };

        var result = fallbackPolicy.Apply(validationResult, new TicketTriageVocabularySnapshot
        {
            Priorities =
            [
                new TicketTriagePriorityOption("Routine", 72, 36),
                new TicketTriagePriorityOption("Urgent", 8, 4),
            ],
            Statuses =
            [
                new TicketTriageStatusOption("New", null, 2),
                new TicketTriageStatusOption("Needs Review", null, 1),
            ],
        });

        Assert.True(result.IsValid);
        Assert.True(result.UsedFallback);
        Assert.Equal("Clarify the intake scope.", result.Summary);
        Assert.Equal("Routine", result.Priority);
        Assert.Equal("Needs Review", result.Status);
        Assert.Equal(
            new[] { "Confirm the requester.", "Confirm the deadline." },
            result.MissingDetails);
        Assert.Equal("Medium", result.PotentialSlaRisk);
        Assert.Equal(
            "Default priority applied — reviewer assessment required.",
            result.PriorityReason);
        Assert.Equal(
            "Clarification needed to assess delivery pressure.",
            result.SlaRiskReason);
        Assert.NotEmpty(result.ValidationErrors);
    }
}
