using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IReportDefinitionRepository
{
    Task<IReadOnlyList<ReportDefinition>> GetAllAsync();
    Task<ReportDefinition?> GetByIdAsync(int id);
    Task<ReportDefinition?> GetByNameAsync(string name);
    Task<ReportDefinition?> GetByViewNameAsync(string viewName);
    Task AddAsync(ReportDefinition definition);
    void Delete(ReportDefinition definition);
    Task SaveChangesAsync();
}
