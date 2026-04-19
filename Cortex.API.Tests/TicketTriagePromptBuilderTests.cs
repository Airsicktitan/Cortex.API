using Cortex.API.Services;

namespace Cortex.API.Tests;

public class TicketTriagePromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesConfiguredVocabularyAndStrictJsonContract()
    {
        var builder = new TicketTriagePromptBuilder();

        var prompt = builder.BuildSystemPrompt(new TicketTriageVocabularySnapshot
        {
            Priorities =
            [
                new TicketTriagePriorityOption("High", 8, 4),
                new TicketTriagePriorityOption("Medium", 24, 12),
            ],
            Statuses =
            [
                new TicketTriageStatusOption("New", "Fresh intake item.", 1),
                new TicketTriageStatusOption("In Review", "Reviewer is assessing scope.", 2),
            ],
        });

        Assert.Contains("reviewer-first", prompt);
        Assert.Contains("Do not invent, normalize, approximate", prompt);
        Assert.Contains("- High (target: 8h, warning: 4h)", prompt);
        Assert.Contains("- In Review: Reviewer is assessing scope.", prompt);
        Assert.Contains("\"priority\": string", prompt);
        Assert.Contains("\"status\": string", prompt);
        Assert.Contains("\"potentialSlaRisk\": \"Low\" | \"Medium\" | \"High\"", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_OmitsStatusRequirement_WhenNoStatusesExist()
    {
        var builder = new TicketTriagePromptBuilder();

        var prompt = builder.BuildSystemPrompt(new TicketTriageVocabularySnapshot
        {
            Priorities = [new TicketTriagePriorityOption("Low", 48, 24)],
            Statuses = [],
        });

        Assert.Contains("No valid statuses are configured in this environment.", prompt);
        Assert.Contains("Do not include this property because no statuses are configured.", prompt);
        Assert.DoesNotContain("\"status\": string", prompt);
    }
}
