using Cortex.API.Services;

namespace Cortex.API.Tests;

public sealed class OptionalProfileFieldNormalizationTests
{
    [Fact]
    public void NormalizeOptionalProfileUpdate_Null_ReturnsNull()
    {
        Assert.Null(OptionalProfileFieldNormalization.NormalizeOptionalProfileUpdate(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void NormalizeOptionalProfileUpdate_BlankOrWhitespace_ReturnsNull(string input)
    {
        Assert.Null(OptionalProfileFieldNormalization.NormalizeOptionalProfileUpdate(input));
    }

    [Theory]
    [InlineData(" Adam ", "Adam")]
    [InlineData("x", "x")]
    public void NormalizeOptionalProfileUpdate_TrimsNonEmpty(string input, string expected)
    {
        Assert.Equal(
            expected,
            OptionalProfileFieldNormalization.NormalizeOptionalProfileUpdate(input));
    }
}
