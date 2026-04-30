namespace Cortex.API.Models;

/// <summary>
/// Subtype for an <see cref="ExternalWorkSource"/>: how work-like items are represented in the external system.
/// </summary>
/// <remarks>
/// Values describe list/project/table shapes used for <see cref="ExternalWorkItem"/> ingestion and board mappings.
/// Enterprise reference or metadata catalogs (for example future SAP dictionary/metadata surfaces) are not
/// expected to reuse this enum; they should live on purpose-built models tied to <see cref="IntegrationConnection"/>.
/// </remarks>
public enum ExternalSourceType
{
    SharePointList = 0,
    JiraProject = 1,
    ServiceNowTable = 2,
}
