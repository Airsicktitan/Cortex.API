namespace Cortex.API.DTO;

/// <summary>Outcome when Cortex attempts to synchronize display name/nickname back to Auth0 (Management API).</summary>
public enum Auth0ProfileSyncStatus
{
    /// <summary>Auth0 write-back disabled or tenant management client not configured.</summary>
    NotConfigured = 0,

    /// <summary>Name fields were PATCHed successfully on the Auth0 user.</summary>
    Synced = 1,

    /// <summary>No name fields supplied, no Auth0 link, or write-back irrelevant for this request.</summary>
    Skipped = 2,

    /// <summary>Tenant reachable but update was rejected — local Cortex profile still saved.</summary>
    Failed = 3,
}

/// <summary>HTTP 200 envelope for PUT /api/users/profile (includes local user plus optional Auth0 outcome).</summary>
public sealed record UpdateUserProfileResponse(
    UserResponse User,
    Auth0ProfileSyncStatus Auth0ProfileSyncStatus,
    string? Auth0ProfileSyncMessage,
    string DiagnosticsTraceId);

/// <summary>HTTP 200 envelope for PUT /api/users/{id} admin updates (local user saved; optional Auth0 profile mirror).</summary>
public sealed record AdminUpdateUserResponse(
    AdminUserResponse User,
    Auth0ProfileSyncStatus Auth0ProfileSyncStatus,
    string? Auth0ProfileSyncMessage,
    string DiagnosticsTraceId);
