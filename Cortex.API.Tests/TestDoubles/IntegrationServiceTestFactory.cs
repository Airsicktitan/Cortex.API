using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.API.Tests.TestDoubles;

public static class IntegrationServiceTestFactory
{
    public static ExternalIntegrationService Create(
        CortexDbContext context,
        FakeSharePointGraphClient? graph = null,
        SharePointGraphOptions? graphOptions = null,
        ITicketCreationApplicationService? ticketCreation = null,
        ITicketBoardService? ticketBoard = null,
        IUserContextService? userContext = null,
        ITicketAuditService? ticketAuditService = null)
    {
        graph ??= new FakeSharePointGraphClient();
        var opts = Options.Create(graphOptions ?? new SharePointGraphOptions());
        var boards = ticketBoard ?? new TicketBoardService(new TicketBoardDefinitionRepository(context));
        var tickets = ticketCreation ?? new CapturingFakeTicketCreationApplicationService();
        EnsureAtLeastOneUser(context);
        userContext ??= new FirstUserContextService(context);
        ticketAuditService ??= new TicketAuditService(context);
        return new ExternalIntegrationService(
            context,
            graph,
            [new SharePointExternalWorkSourceAdapter(graph)],
            opts,
            tickets,
            boards,
            userContext,
            ticketAuditService,
            NullLogger<ExternalIntegrationService>.Instance);
    }

    private static void EnsureAtLeastOneUser(CortexDbContext context)
    {
        if (context.Users.Any())
        {
            return;
        }

        context.Users.Add(
            new User
            {
                DisplayName = "Integration test user",
                Email = "integration-test@cortex.local",
                Role = Auth0Roles.Admin,
                CreatedDate = DateTime.UtcNow,
            });
        context.SaveChanges();
    }
}
