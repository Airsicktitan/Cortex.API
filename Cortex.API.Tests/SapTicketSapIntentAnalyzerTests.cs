using Cortex.API.Services;

namespace Cortex.API.Tests;

public class SapTicketSapIntentAnalyzerTests
{
    [Theory]
    [InlineData("Need SAP field fixed")]
    [InlineData("There is a problem with a field in SAP. Please fix it.")]
    [InlineData("SAP master data needs correction but the table and field are unknown.")]
    [InlineData("Issue in S/4 purchasing")]
    [InlineData("ECC vendor block")]
    [InlineData("Update SAP configuration for pricing")]
    public void HasSapIntent_Positive(string text) =>
        Assert.True(SapTicketSapIntentAnalyzer.HasSapIntent(text));

    [Theory]
    [InlineData("Fix field on intake form")]
    [InlineData("The request form has a field that is not saving correctly.")]
    [InlineData("")]
    [InlineData("   ")]
    public void HasSapIntent_Negative(string text) =>
        Assert.False(SapTicketSapIntentAnalyzer.HasSapIntent(text));

    [Fact]
    public void HasSapIntent_Null_IsFalse() =>
        Assert.False(SapTicketSapIntentAnalyzer.HasSapIntent(null));
}
