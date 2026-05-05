using Cortex.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace Cortex.API.Authorization;

public static class CortexAuthorizationExtensions
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ElevatedAccess = nameof(ElevatedAccess);
    public const string BusinessAccess = nameof(BusinessAccess);
    public const string StandardWriteAccess = nameof(StandardWriteAccess);
    public const string BusinessDataAccess = nameof(BusinessDataAccess);
    /// <summary>
    /// Intake approval reviewer surface (queue, triage advisory, approve/return/reject, AI assess).
    /// Excludes archive/delete configuration-style operations.
    /// </summary>
    public const string ReviewerApprovalAccess = nameof(ReviewerApprovalAccess);

    public static void AddCortexPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy =>
            policy.RequireRole(Auth0Roles.Admin));

        options.AddPolicy(ElevatedAccess, policy =>
            policy.RequireRole(Auth0Roles.Admin, Auth0Roles.Developer));

        options.AddPolicy(BusinessAccess, policy =>
            policy.RequireRole(
                Auth0Roles.Admin,
                Auth0Roles.Developer,
                Auth0Roles.BusinessManager));

        options.AddPolicy(StandardWriteAccess, policy =>
            policy.RequireAssertion(context =>
                context.User.IsInRole(Auth0Roles.Admin) ||
                context.User.IsInRole(Auth0Roles.Developer) ||
                context.User.IsInRole(Auth0Roles.BusinessManager) ||
                context.User.IsInRole(Auth0Roles.Approver) ||
                context.User.IsInRole(Auth0Roles.User)));

        options.AddPolicy(BusinessDataAccess, policy =>
            policy.RequireRole(
                Auth0Roles.Admin,
                Auth0Roles.Developer,
                Auth0Roles.BusinessManager));

        options.AddPolicy(ReviewerApprovalAccess, policy =>
            policy.RequireRole(
                Auth0Roles.Admin,
                Auth0Roles.Developer,
                Auth0Roles.BusinessManager,
                Auth0Roles.Approver));
    }
}
