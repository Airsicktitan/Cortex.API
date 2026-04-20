using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Default implementation of <see cref="IAccessApprovalService"/>.
/// Keep this deliberately small — one rule, one place.
/// </summary>
public sealed class AccessApprovalService : IAccessApprovalService
{
    /// <summary>
    /// The preserved pilot/demo email. Intentionally a constant (not configuration) so the
    /// bypass surface is obvious in code review. Additional exempt emails or allowlist rules
    /// should be introduced here with a clear rationale.
    /// </summary>
    public const string DemoEmail = "demo@cortex.com";

    public bool IsDemoCaller(string? email, bool emailVerified)
    {
        if (!emailVerified)
        {
            return false;
        }

        return MatchesDemoEmail(email);
    }

    public AccessApprovalDecision Evaluate(User? existingLocalUser, string? email, bool emailVerified)
    {
        if (IsDemoCaller(email, emailVerified))
        {
            return AccessApprovalDecision.Approved;
        }

        if (existingLocalUser is null)
        {
            return AccessApprovalDecision.DeniedUnknownUser;
        }

        if (!existingLocalUser.IsActive)
        {
            return AccessApprovalDecision.DeniedInactive;
        }

        if (existingLocalUser.ExpiryDate.HasValue &&
            existingLocalUser.ExpiryDate.Value <= DateTime.UtcNow)
        {
            return AccessApprovalDecision.DeniedExpired;
        }

        return AccessApprovalDecision.Approved;
    }

    /// <summary>
    /// Diagnostic helper: pure email match against the demo address, ignoring verification.
    /// Must not be used to grant access — use <see cref="IsDemoCaller"/> for decisions.
    /// </summary>
    internal static bool MatchesDemoEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return string.Equals(email.Trim(), DemoEmail, StringComparison.OrdinalIgnoreCase);
    }
}
