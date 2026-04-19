namespace Cortex.API.DTO;

/// <summary>Result of importing Auth0 tenant users into the local Cortex user directory (projection, not identity replacement).</summary>
public class SyncUsersFromAuth0Response
{
    /// <summary>Users returned from Auth0 Management API (after filtering).</summary>
    public int TotalFromAuth0 { get; set; }

    /// <summary>New local rows created for Auth0 <c>user_id</c>s not yet present.</summary>
    public int Created { get; set; }

    /// <summary>Existing local rows matched by email with no <c>Auth0Id</c>; linked to the Auth0 user.</summary>
    public int LinkedByEmail { get; set; }

    /// <summary>Existing users matched by <c>Auth0Id</c> with safe identity fields refreshed.</summary>
    public int Updated { get; set; }

    /// <summary>Matched by <c>Auth0Id</c> with nothing to change.</summary>
    public int Unchanged { get; set; }

    public int SkippedNoEmail { get; set; }

    /// <summary>Another local row already holds this email with a different Auth0 id.</summary>
    public int SkippedEmailConflict { get; set; }
}
