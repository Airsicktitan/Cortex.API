using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IReportDefinitionService
{
    Task<IReadOnlyList<ReportDefinition>> GetAllAsync();
    Task<IReadOnlyList<DatabaseViewDefinition>> GetAvailableViewsAsync();
    Task<ReportDefinition> CreateAsync(ReportDefinition definition);
    Task<ReportDefinition> UpdateAsync(int id, ReportDefinition definition);
    Task DeleteAsync(int id);
    Task<CustomReportResultResponse> ExecuteAsync(int id);
}
