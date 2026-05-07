using System.Text.Json;

using Cortex.API.Data;

using Cortex.API.DTO;

using Cortex.API.Handlers;

using Cortex.API.Models;

using Cortex.API.Services;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;

using Moq;



namespace Cortex.API.Tests;



public class TicketHandlersGetSynitiKnowledgeContextTests

{

    [Fact]

    public async Task HiddenTicket_Returns404_DoesNotCallService()

    {

        var ticket = new Ticket

        {

            Id = "T-99",

            Title = "DSP",

            Description = "Private",

            Status = "New",

            ApprovalStatus = ApprovalStatus.Approved,

            Priority = "Medium",

            BoardId = 1,

            CreatedBy = 999,

            LastModifiedBy = 999,

        };



        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);

        repo.Setup(r => r.GetTicketByIdAsync("T-99")).ReturnsAsync(ticket);



        var visibility = new Mock<ITicketVisibilityService>(MockBehavior.Strict);

        visibility.Setup(s => s.GetCurrentVisibilityAsync())

            .ReturnsAsync(new TicketVisibilityContext(1, "U", "u@example.com", TicketVisibilityScope.CreatedByCurrentUser));



        var syniti = new Mock<ISynitiKnowledgeContextService>(MockBehavior.Strict);



        var result = await TicketHandlers.GetTicketSynitiKnowledgeContext(

            "T-99",

            repo.Object,

            visibility.Object,

            syniti.Object,

            CancellationToken.None);



        var httpContext = CreateHttpContext();

        await result.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);

        syniti.Verify(

            s => s.GetContextForTicketAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()),

            Times.Never);

    }



    [Fact]

    public async Task VisibleTicket_ReturnsDto_FromService()

    {

        var ticket = new Ticket

        {

            Id = "T-1",

            Title = "DSP",

            Description = "",

            Status = "New",

            ApprovalStatus = ApprovalStatus.Approved,

            Priority = "Medium",

            BoardId = 1,

            CreatedBy = 1,

            LastModifiedBy = 1,

        };



        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);

        repo.Setup(r => r.GetTicketByIdAsync("T-1")).ReturnsAsync(ticket);



        var visibility = new Mock<ITicketVisibilityService>(MockBehavior.Strict);

        visibility.Setup(s => s.GetCurrentVisibilityAsync())

            .ReturnsAsync(new TicketVisibilityContext(1, "R", "r@example.com", TicketVisibilityScope.All));



        var dto = new SynitiKnowledgeContextDto("T-1", []);

        var syniti = new Mock<ISynitiKnowledgeContextService>(MockBehavior.Strict);

        syniti

            .Setup(s => s.GetContextForTicketAsync(ticket, It.IsAny<CancellationToken>()))

            .ReturnsAsync(dto);



        var result = await TicketHandlers.GetTicketSynitiKnowledgeContext(

            "T-1",

            repo.Object,

            visibility.Object,

            syniti.Object,

            CancellationToken.None);



        var httpContext = CreateHttpContext();

        await result.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        httpContext.Response.Body.Position = 0;

        var parsed = await JsonSerializer.DeserializeAsync<SynitiKnowledgeContextDto>(

            httpContext.Response.Body,

            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(parsed);

        Assert.Equal("T-1", parsed!.TicketId);

    }



    private static DefaultHttpContext CreateHttpContext()

    {

        var services = new ServiceCollection();

        services.AddLogging();

        return new DefaultHttpContext

        {

            RequestServices = services.BuildServiceProvider(),

            Response = { Body = new MemoryStream() },

        };

    }

}


