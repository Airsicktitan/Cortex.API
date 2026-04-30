using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.Data.Repositories;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Microsoft.Extensions.Options;

namespace Cortex.API.Tests.TestDoubles;

public static class IntegrationServiceTestFactory
{
    public static ExternalIntegrationService Create(
        CortexDbContext context,
        FakeSharePointGraphClient? graph = null,
        SharePointGraphOptions? graphOptions = null,
        ITicketCreationApplicationService? ticketCreation = null,
        ITicketBoardService? ticketBoard = null)
    {
        graph ??= new FakeSharePointGraphClient();
        var opts = Options.Create(graphOptions ?? new SharePointGraphOptions());
        var boards = ticketBoard ?? new TicketBoardService(new TicketBoardDefinitionRepository(context));
        var tickets = ticketCreation ?? new CapturingFakeTicketCreationApplicationService();
        return new ExternalIntegrationService(
            context,
            graph,
            [new SharePointExternalWorkSourceAdapter(graph)],
            opts,
            tickets,
            boards);
    }
}
