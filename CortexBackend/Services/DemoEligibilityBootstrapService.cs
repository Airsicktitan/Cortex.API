using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class DemoEligibilityBootstrapService(
    CortexDbContext dbContext,
    ILogger<DemoEligibilityBootstrapService> logger) : IDemoEligibilityBootstrapService
{
    private static readonly string[] KnownDemoDisplayNames =
    [
        "adam hooper",
        "john snow",
    ];

    private static readonly string[] KnownDemoEmails =
    [
        "adamcwhooper@yahoo.com",
        "saharajax27@gmail.com",
    ];

    private readonly CortexDbContext _dbContext = dbContext;
    private readonly ILogger<DemoEligibilityBootstrapService> _logger = logger;

    public async Task<int> EnsureDemoEligibilityAsync(CancellationToken cancellationToken = default)
    {
        // Respect explicit admin configuration once any eligibility has been set.
        var anyEligibilityConfigured = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id > 0
                    && user.IsActive
                    && (user.IsSynitiOwnerEligible || user.IsBusinessOwnerEligible),
                cancellationToken);
        if (anyEligibilityConfigured)
        {
            return 0;
        }

        var updated = await _dbContext.Users
            .Where(user => user.Id > 0 && user.IsActive)
            .Where(user =>
                KnownDemoDisplayNames.Contains((user.DisplayName ?? string.Empty).ToLowerInvariant())
                || KnownDemoEmails.Contains((user.Email ?? string.Empty).ToLowerInvariant()))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsSynitiOwnerEligible, true)
                .SetProperty(user => user.IsBusinessOwnerEligible, true)
                .SetProperty(user => user.LastModifiedDate, DateTime.UtcNow), cancellationToken);

        if (updated > 0)
        {
            _logger.LogInformation(
                "Applied demo owner-eligibility bootstrap for {UpdatedCount} user(s).",
                updated);
        }

        return updated;
    }
}
