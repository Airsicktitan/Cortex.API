using Cortex.API.Database;
using Cortex.API.DTO;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class ResponseMappingContextFactory(CortexDbContext context)
    : IResponseMappingContextFactory
{
    private readonly CortexDbContext _context = context;

    public async Task<ResponseMappingContext> CreateAsync(
        IEnumerable<int> userIds,
        IEnumerable<int>? storedProcedureDefinitionIds = null,
        CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var distinctStoredProcedureIds = (storedProcedureDefinitionIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var userDisplayNames = distinctUserIds.Length == 0
            ? new Dictionary<int, string>()
            : await _context.Users
                .AsNoTracking()
                .Where(user => distinctUserIds.Contains(user.Id))
                .Select(user => new
                {
                    user.Id,
                    DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                        ? user.Email
                        : user.DisplayName
                })
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.DisplayName ?? "Unknown User",
                    cancellationToken);

        var storedProcedureLabels = distinctStoredProcedureIds.Length == 0
            ? new Dictionary<int, string>()
            : await _context.StoredProcedureDefinitions
                .AsNoTracking()
                .Where(definition => distinctStoredProcedureIds.Contains(definition.Id))
                .Select(definition => new
                {
                    definition.Id,
                    definition.Name
                })
                .ToDictionaryAsync(
                    definition => definition.Id,
                    definition => definition.Name,
                    cancellationToken);

        return new ResponseMappingContext(userDisplayNames, storedProcedureLabels);
    }
}
