using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IAiSettingsConfigurationRepository
{
    Task<AiSettingsConfiguration?> GetAsync();
    Task UpsertAsync(AiSettingsConfiguration configuration);
    Task AddAuditEntryAsync(AiSettingsAuditEntry auditEntry);
    Task SaveChangesAsync();
}
