using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Outcome of evaluating whether an authenticated identity is permitted to use Cortex.
/// Kept as a value-style record so callers can branch on <see cref="IsApproved"/> and
/// translate a denial reason into a logged + user-facing response without leaking details.
/// </summary>
public sealed record AccessApprovalDecision(bool IsApproved, string? DenialReason)
{
    public static AccessApprovalDecision Approved { get; } = new(true, null);

    public static AccessApprovalDecision DeniedUnknownUser { get; } =
        new(false, AccessNotApprovedException.Reasons.UnknownUser);

    public static AccessApprovalDecision DeniedInactive { get; } =
        new(false, AccessNotApprovedException.Reasons.Inactive);

    public static AccessApprovalDecision DeniedExpired { get; } =
        new(false, AccessNotApprovedException.Reasons.Expired);
}

/// <summary>
/// Centralized access approval rule. Currently enforces the v1 policy:
///   - the <c>demo@cortex.com</c> pilot account is always allowed, but only when the
///     token presents a verified email claim,
///   - any other caller must already exist as an active, non-expired Cortex user.
/// Future access models (invite flow, domain allowlist, SSO group mapping) should extend
/// this service rather than scatter checks across handlers.
/// </summary>
public interface IAccessApprovalService
{
    /// <summary>
    /// Returns true when the caller should be treated as the preserved pilot/demo account.
    /// Requires <paramref name="emailVerified"/> to be <c>true</c> so that a token simply
    /// claiming the demo email (without Auth0 verifying it) cannot impersonate the demo user.
    /// Do not use this for diagnostic/pure email matching — it is the access-decision gate.
    /// </summary>
    bool IsDemoCaller(string? email, bool emailVerified);

    /// <summary>
    /// Evaluates whether the authenticated caller is permitted to use Cortex.
    /// </summary>
    /// <param name="existingLocalUser">
    /// The local Cortex user record already associated with the caller (via Auth0 id or email),
    /// or <c>null</c> if no record exists yet.
    /// </param>
    /// <param name="email">The normalized email from the caller's token, if available.</param>
    /// <param name="emailVerified">
    /// Whether the Auth0 token asserted <c>email_verified == true</c>. Required for the demo
    /// bypass; otherwise informational.
    /// </param>
    AccessApprovalDecision Evaluate(User? existingLocalUser, string? email, bool emailVerified);
}
