using Cortex.API.Data.Repositories;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class TicketTriageVocabularyProviderTests
{
    [Fact]
    public async Task GetAsync_TrimsValues_AndFiltersBlankNames()
    {
        var statusRepository = new Mock<ITicketStatusDefinitionRepository>(MockBehavior.Strict);
        statusRepository
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(
            [
                new TicketStatusDefinition { Id = 2, Name = "  New  ", Description = "  Fresh intake  ", IsEnabled = true },
                new TicketStatusDefinition { Id = 3, Name = "   ", Description = "Ignored", IsEnabled = true },
                new TicketStatusDefinition { Id = 4, Name = "Closed", Description = null, IsEnabled = false },
            ]);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(
            [
                new SlaConfiguration { Priority = "  High  ", TargetHours = 8, WarningHours = 4 },
                new SlaConfiguration { Priority = "high", TargetHours = 8, WarningHours = 4 },
                new SlaConfiguration { Priority = "   ", TargetHours = 48, WarningHours = 24 },
            ]);

        var provider = new TicketTriageVocabularyProvider(
            statusRepository.Object,
            slaConfigurationService.Object);

        var result = await provider.GetAsync();

        Assert.Equal(new[] { "New" }, result.Statuses.Select(x => x.Name).ToArray());
        Assert.Equal("Fresh intake", result.Statuses[0].Description);
        Assert.Equal(new[] { "High" }, result.Priorities.Select(x => x.Name).ToArray());
    }
}
