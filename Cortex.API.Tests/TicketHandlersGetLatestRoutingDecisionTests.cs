using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cortex.API.Tests;

public class TicketHandlersGetLatestRoutingDecisionTests
{
    [Fact]
    public async Task GetLatestRoutingDecision_RoutingDetailThrows_ReturnsEmptyFallback()
    {
        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(repository => repository.GetTicketByIdAsync("T-404"))
            .ReturnsAsync(new Ticket
            {
                Id = "T-404",
                Title = "Routing detail fallback",
                Description = "Test",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "High",
                BoardId = 1,
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow,
                LastModifiedBy = 1,
                LastModifiedDate = DateTime.UtcNow,
            });

        var visibility = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibility.Setup(service => service.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(1, "Reviewer", "reviewer@example.com", TicketVisibilityScope.All));

        var routing = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        routing.Setup(service => service.GetLatestDecisionAsync("T-404", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Optional routing detail failed."));

        var result = await TicketHandlers.GetLatestRoutingDecision(
            "T-404",
            repo.Object,
            visibility.Object,
            routing.Object,
            Mock.Of<ILogger<TicketHandlersLogCategory>>());

        var response = await ExecuteAndReadJsonAsync<TicketRoutingLatestResponse>(result);

        Assert.NotNull(response);
        Assert.Null(response!.Decision);
        Assert.Null(response.Override);
    }

    private static async Task<T?> ExecuteAndReadJsonAsync<T>(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        await using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await result.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        body.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
