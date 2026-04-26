using Cortex.API.Services;

namespace Cortex.API.Tests;

public class TicketIntakeAssistPromptBuilderTests
{
    private readonly TicketIntakeAssistPromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_IncludesBehaviorModel_WithUglyBadGoodTiers()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("TRANSFORM", prompt);
        Assert.Contains("REFINE", prompt);
        Assert.Contains("RESPECT", prompt);
        Assert.Contains("Ugly input", prompt);
        Assert.Contains("Good input", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_RejectsWeakTitlePhrases()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("not working", prompt);
        Assert.Contains("broken", prompt);
        Assert.Contains("help needed", prompt);
        Assert.Contains("issue", prompt);

        Assert.Contains("avoid", prompt.ToLowerInvariant());
    }

    [Fact]
    public void BuildSystemPrompt_IncludesActiveFormTitleInstruction()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("[System] [failure type] preventing [business outcome]", prompt);
        Assert.Contains("SAP posting failure preventing transaction processing", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_UsesNewDescriptionSectionStructure()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("Summary:", prompt);
        Assert.Contains("What's happening:", prompt);
        Assert.Contains("What's missing:", prompt);
        Assert.Contains("Next steps:", prompt);

        Assert.DoesNotContain("Issue:\n", prompt);
        Assert.DoesNotContain("What happened:\n", prompt);
        Assert.DoesNotContain("Impact:\n", prompt);
        Assert.DoesNotContain("\n  Notes:\n", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_RequiresDomainSpecificMissingDetails()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("Exact error message or system response", prompt);
        Assert.Contains("Transaction code, module, or process affected", prompt);
        Assert.Contains("When the issue started", prompt);
        Assert.Contains("Steps already attempted", prompt);
        Assert.Contains("Number of users or business processes impacted", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ForbidsGenericMissingDetailItems()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("More information", prompt);
        Assert.Contains("Additional context", prompt);
        Assert.Contains("Do NOT use generic items", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ForbidsInventingFacts()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("Do not invent specific facts", prompt);
        Assert.Contains("transaction codes", prompt);
        Assert.Contains("module names", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_GuidanceMessageIsCoachingNotCriticism()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("coaching not criticism", prompt);
        Assert.Contains("Add the missing details so reviewers can act without extra follow-up", prompt);
        Assert.Contains("This request is clear — reviewer can act on it as written", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_RespectsGoodInputInstruction()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("RESPECT", prompt);
        Assert.Contains("light polish only", prompt);
        Assert.Contains("return [] or at most one targeted item", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsValidJsonContract()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("\"suggestedSummary\"", prompt);
        Assert.Contains("\"improvedDescription\"", prompt);
        Assert.Contains("\"missingDetails\"", prompt);
        Assert.Contains("\"clarityState\"", prompt);
        Assert.Contains("\"guidanceMessage\"", prompt);
        Assert.Contains("ready_for_execution", prompt);
        Assert.Contains("requires_clarification", prompt);
        Assert.Contains("would_have_required_follow_up", prompt);
    }
}
