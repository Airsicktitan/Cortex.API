using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// On startup, logs approved tickets whose stored owners violate role policy (no data mutation).
/// </summary>
public sealed class OwnerAssignmentRoleIntegrityAuditHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OwnerAssignmentRoleIntegrityAuditHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CortexDbContext>();
            var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
            var aliases = OwnerFieldResolution.BuildAliasLookup(users);
            var tickets = await db.Tickets
                .AsNoTracking()
                .Where(t => t.ApprovalStatus == ApprovalStatus.Approved)
                .Select(t => new { t.Id, t.SynitiOwner, t.BusinessOwner })
                .ToListAsync(cancellationToken);

            var issues = 0;
            var samples = new List<string>(8);
            foreach (var ticket in tickets)
            {
                if (!string.IsNullOrWhiteSpace(ticket.SynitiOwner))
                {
                    var u = OwnerFieldResolution.ResolveUser(ticket.SynitiOwner, aliases);
                    if (u is not null && !OwnerRoleAssignmentRules.IsValidSynitiOwnerAssignment(u))
                    {
                        issues++;
                        if (samples.Count < 6)
                        {
                            samples.Add($"{ticket.Id}: Syniti owner role '{u.Role}'.");
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(ticket.BusinessOwner))
                {
                    var u = OwnerFieldResolution.ResolveUser(ticket.BusinessOwner, aliases);
                    if (u is not null && !OwnerRoleAssignmentRules.IsValidBusinessOwnerAssignment(u))
                    {
                        issues++;
                        if (samples.Count < 6)
                        {
                            samples.Add($"{ticket.Id}: Business owner role '{u.Role}'.");
                        }
                    }
                }
            }

            if (issues > 0)
            {
                logger.LogWarning(
                    "Owner role integrity audit: {IssueCount} owner slot(s) on approved tickets violate policy (Syniti=active+Syniti department+eligibility; Business=non-developer, non-guest). Samples: {Samples}",
                    issues,
                    string.Join(" ", samples));
            }

            var developerDeptSamples = new List<string>(8);
            var developerDeptIssues = 0;
            foreach (var u in users)
            {
                if (!string.Equals(u.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dept = u.Department?.Trim();
                if (string.IsNullOrEmpty(dept)
                    || !dept.Equals(UserDepartmentPolicy.DefaultDeveloperDepartment, StringComparison.OrdinalIgnoreCase))
                {
                    developerDeptIssues++;
                    if (developerDeptSamples.Count < 6)
                    {
                        developerDeptSamples.Add(
                            $"{u.Id}: department '{(string.IsNullOrEmpty(dept) ? "(empty)" : dept)}'.");
                    }
                }
            }

            if (developerDeptIssues > 0)
            {
                logger.LogInformation(
                    "Developer department alignment audit: {Count} Developer user(s) are not assigned to department '{Expected}' (empty or override). Samples: {Samples}",
                    developerDeptIssues,
                    UserDepartmentPolicy.DefaultDeveloperDepartment,
                    string.Join(" ", developerDeptSamples));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Owner role integrity audit failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
