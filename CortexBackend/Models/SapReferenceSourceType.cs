namespace Cortex.API.Models;

/// <summary>How SAP reference metadata was loaded. Not a live SAP connector indicator.</summary>
public enum SapReferenceSourceType
{
    Manual,
    CsvImport,
    MetadataExport,
    SynitiExport,
    FutureLiveSap,
}
