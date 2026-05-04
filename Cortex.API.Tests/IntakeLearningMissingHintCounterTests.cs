using Cortex.API.Services;

namespace Cortex.API.Tests;

public sealed class IntakeLearningMissingHintCounterTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("not json", 0)]
    [InlineData("{}", 0)]
    [InlineData("[]", 0)]
    [InlineData("[\"\"]", 0)]
    [InlineData("[\"  \"]", 0)]
    [InlineData("[\"a\",\"b\"]", 2)]
    public void CountMissingHints_handles_edge_cases(string? json, int expected)
    {
        Assert.Equal(expected, IntakeLearningMissingHintCounter.CountMissingHints(json));
    }
}
