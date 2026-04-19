namespace Cortex.API.DTO;

/// <summary>Result of importing Auth0 tenant roles into Cortex (additive: creates missing definitions only).</summary>
public class SyncRoleDefinitionsFromAuth0Response
{
    /// <summary>Number of new Cortex role definition rows created from Auth0 roles.</summary>
    public int Created { get; set; }

    /// <summary>Auth0 roles that already had a matching Cortex definition (by name, case-insensitive).</summary>
    public int SkippedExisting { get; set; }

    /// <summary>Auth0 tenant roles with a non-empty name that were considered during this import (additive merge only).</summary>
    public int TotalFromAuth0 { get; set; }
}
