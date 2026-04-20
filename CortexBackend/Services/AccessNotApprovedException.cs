namespace Cortex.API.Services;

/// <summary>
/// Thrown when an identity is successfully authenticated by Auth0 but is not approved
/// to use Cortex. Translated to HTTP 403 with code ACCESS_NOT_APPROVED by the global
/// exception handler; consumers must not treat this as a generic unauthorized condition.
/// </summary>
public sealed class AccessNotApprovedException : Exception
{
    /// <summary>Canonical reason codes for logging/audit. Not exposed to end users.</summary>
    public static class Reasons
    {
        public const string UnknownUser = "UnknownUser";
        public const string Inactive = "Inactive";
        public const string Expired = "Expired";
    }

    public string Reason { get; }
    public string? Email { get; }
    public string? Auth0Id { get; }

    public AccessNotApprovedException(string reason, string? email, string? auth0Id)
        : base("Access to Cortex has not been approved for this account.")
    {
        Reason = reason;
        Email = email;
        Auth0Id = auth0Id;
    }
}
