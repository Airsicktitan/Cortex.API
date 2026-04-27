using Cortex.API.Services;

namespace Cortex.API.Tests;

public sealed class AiOutputSanitizerTests
{
    private readonly AiOutputSanitizer _sut = new();

    [Fact]
    public void Sanitize_RedactsConnectionStringValues()
    {
        var input = "Server=myserver;Initial Catalog=CortexDb;User Id=sa;Password=SuperSecret!;";

        var result = _sut.Sanitize(input);

        Assert.Contains("[sensitive value hidden]", result);
        Assert.DoesNotContain("SuperSecret!", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_RedactsBearerAndJwt()
    {
        var input = "Authorization: Bearer eyJabc1234567890.payload1234567890.signature1234567890";

        var result = _sut.Sanitize(input);

        Assert.DoesNotContain("eyJabc1234567890", result, StringComparison.Ordinal);
        Assert.Contains("[sensitive value hidden]", result);
    }

    [Fact]
    public void Sanitize_RedactsSqlAndSchemaHints()
    {
        var input = "SELECT * FROM dbo.Tickets WHERE Status = 'Open'";

        var result = _sut.Sanitize(input);

        Assert.Contains("[technical detail hidden]", result);
    }

    [Fact]
    public void Sanitize_RedactsStackTraceGuidAndHost()
    {
        var input =
            "at Cortex.API.Services.Triage() in C:\\repo\\file.cs:line 10 id=550e8400-e29b-41d4-a716-446655440000 host=myapp.azurewebsites.net";

        var result = _sut.Sanitize(input);

        Assert.DoesNotContain("550e8400-e29b-41d4-a716-446655440000", result, StringComparison.Ordinal);
        Assert.DoesNotContain("azurewebsites.net", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_PreservesBusinessSafeText()
    {
        const string input = "Recommend follow-up with payroll owner to verify missing approval context.";

        var result = _sut.Sanitize(input);

        Assert.Equal(input, result);
    }
}
