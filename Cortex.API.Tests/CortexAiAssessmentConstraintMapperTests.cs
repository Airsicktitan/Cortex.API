using Cortex.API.Services;

namespace Cortex.API.Tests;

public class CortexAiAssessmentConstraintMapperTests
{
    [Theory]
    [InlineData("low", "Low")]
    [InlineData("HIGH", "High")]
    [InlineData(" medium ", "Medium")]
    public void NormalizeRisk_MapsKnownTiers(string raw, string expected)
    {
        var confidence = 0.9m;
        var result = CortexAiAssessmentConstraintMapper.NormalizeRisk(raw, ref confidence);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeRisk_InvalidReducesConfidenceAndFallsBackToLow()
    {
        var confidence = 0.9m;
        var result = CortexAiAssessmentConstraintMapper.NormalizeRisk("nope", ref confidence);
        Assert.Equal("Low", result);
        Assert.True(confidence < 0.9m);
    }

    [Fact]
    public void TryMatchConfiguredPriorityName_UsesExactConfiguredSpelling()
    {
        var priorities = new List<TicketTriagePriorityOption>
        {
            new("Medium", 24, 12),
            new("High", 8, 4),
        };

        var hit = CortexAiAssessmentConstraintMapper.TryMatchConfiguredPriorityName("high", priorities);
        Assert.Equal("High", hit);
    }

    [Fact]
    public void ResolvePrioritySynonym_UrgentMapsToUpperTier()
    {
        var priorities = new List<TicketTriagePriorityOption>
        {
            new("Low", 48, 24),
            new("Medium", 24, 12),
            new("High", 8, 4),
        };

        var mapped = CortexAiAssessmentConstraintMapper.ResolvePrioritySynonym("urgent!!!", priorities);
        Assert.Equal("High", mapped);
    }
}
