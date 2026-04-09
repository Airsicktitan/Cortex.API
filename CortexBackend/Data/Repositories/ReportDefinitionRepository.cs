using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class ReportDefinitionRepository(CortexDbContext context) : IReportDefinitionRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<ReportDefinition>> GetAllAsync()
    {
        return await _context.ReportDefinitions
            .OrderBy(definition => definition.Name)
            .ToListAsync();
    }

    public async Task<ReportDefinition?> GetByIdAsync(int id)
    {
        return await _context.ReportDefinitions.FirstOrDefaultAsync(definition => definition.Id == id);
    }

    public async Task<ReportDefinition?> GetByNameAsync(string name)
    {
        var normalizedName = name.Trim();
        return await _context.ReportDefinitions.FirstOrDefaultAsync(definition => definition.Name == normalizedName);
    }

    public async Task<ReportDefinition?> GetByViewNameAsync(string viewName)
    {
        var normalizedViewName = viewName.Trim();
        return await _context.ReportDefinitions.FirstOrDefaultAsync(definition => definition.ViewName == normalizedViewName);
    }

    public async Task AddAsync(ReportDefinition definition)
    {
        await _context.ReportDefinitions.AddAsync(definition);
    }

    public void Delete(ReportDefinition definition)
    {
        _context.ReportDefinitions.Remove(definition);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
