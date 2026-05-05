namespace Cortex.API.Models;

/// <summary>How Syniti knowledge rows were loaded. Reference metadata only — not a live Syniti connector.</summary>
public enum SynitiKnowledgeSourceType
{
    Manual = 0,
    Seed = 1,
    FutureBundleImport = 2,
}
