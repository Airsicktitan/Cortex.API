using Cortex.API.Database;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;

namespace Cortex.API.Tests.TestDoubles;

public static class IntegrationServiceTestFactory
{
    public static ExternalIntegrationService Create(
        CortexDbContext context,
        FakeSharePointGraphClient? graph = null)
    {
        graph ??= new FakeSharePointGraphClient();
        return new ExternalIntegrationService(context, graph, [new SharePointExternalWorkSourceAdapter(graph)]);
    }
}
