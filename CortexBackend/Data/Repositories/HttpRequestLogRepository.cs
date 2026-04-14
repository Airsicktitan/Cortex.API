using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class HttpRequestLogRepository(CortexDbContext context) : IHttpRequestLogRepository
{
    private const int MaxExportRows = 50_000;

    private readonly CortexDbContext _context = context;

    public async Task AddAsync(HttpRequestLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.HttpRequestLogEntries.AddAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HttpRequestLogEntry>> GetBetweenAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcInclusive,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var safeMaxRows = Math.Clamp(maxRows, 1, MaxExportRows);

        return await _context.HttpRequestLogEntries
            .AsNoTracking()
            .Where(entry => entry.OccurredUtc >= fromUtcInclusive && entry.OccurredUtc <= toUtcInclusive)
            .OrderBy(entry => entry.OccurredUtc)
            .ThenBy(entry => entry.Id)
            .Take(safeMaxRows)
            .ToListAsync(cancellationToken);
    }
}
