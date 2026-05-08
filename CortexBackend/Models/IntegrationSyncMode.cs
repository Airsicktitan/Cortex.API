namespace Cortex.API.Models;

public enum IntegrationSyncMode
{
    ReadOnly = 0,
    ImportToCortex = 1,
    TwoWay = 2,

    /// <summary>Operators trigger ingest or validation manually — no automatic schedule in this version.</summary>
    Manual = 3,
}
