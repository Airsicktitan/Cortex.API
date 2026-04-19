using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class TicketTriageResponseValidatorTests
{
    [Fact]
    public void Validate_CanonicalizesConfiguredValues_UsingOrdinalIgnoreCase()
    {
        var validator = new TicketTriageResponseValidator();
        var response = new TicketTriageGenerateResponse
        {
            Summary = "Clarify the approval scope.",
            SuggestedPriority = " high ",
            PriorityReason = "This blocks intake progress.",
            SuggestedStatus = " in review ",
            MissingDetails = ["Confirm the requester.", "Confirm the target workflow."],
            PotentialSlaRisk = "medium",
            SlaRiskReason = "Extra clarification loops will slow delivery.",
        };

        var result = validator.Validate(response, new TicketTriageVocabularySnapshot
        {
            Priorities =
            [
                new TicketTriagePriorityOption("Low", 48, 24),
                new TicketTriagePriorityOption("High", 8, 4),
            ],
            Statuses =
            [
                new TicketTriageStatusOption("New", null, 1),
                new TicketTriageStatusOption("In Review", null, 2),
            ],
        });

        Assert.True(result.IsValid);
        Assert.Equal("High", result.Priority);
        Assert.Equal("In Review", result.Status);
        Assert.Equal("Medium", result.PotentialSlaRisk);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void Validate_ReturnsStructuredErrors_ForMissingOrInvalidFields()
    {
        var validator = new TicketTriageResponseValidator();
        var response = new TicketTriageGenerateResponse
        {
            Summary = " ",
            SuggestedPriority = "Urgent",
            PriorityReason = "",
            SuggestedStatus = null,
            MissingDetails = ["Only one item"],
            PotentialSlaRisk = "Critical",
            SlaRiskReason = " ",
        };

        var result = validator.Validate(response, new TicketTriageVocabularySnapshot
        {
            Priorities = [new TicketTriagePriorityOption("Low", 48, 24)],
            Statuses = [new TicketTriageStatusOption("New", null, 1)],
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, error => error.Contains("summary", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("priority", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("status", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("missingDetails", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("potentialSlaRisk", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("slaRiskReason", StringComparison.OrdinalIgnoreCase));
    }
}
