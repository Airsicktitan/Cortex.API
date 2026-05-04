namespace Cortex.API.Models;

/// <summary>
/// Identifies which external system an <see cref="IntegrationConnection"/> talks to.
/// </summary>
/// <remarks>
/// <para>
/// SharePoint, Jira, and ServiceNow are modeled here as systems that feed <see cref="ExternalWorkSource"/>
/// (board- or list-style work ingestion). The same connection concept can later cover other provider
/// families that are <strong>not</strong> work-board sources—for example SAP as an enterprise reference
/// or context source (lookup tables, domain values, BAPI/table metadata, technical field names, enrichment).
/// </para>
/// <para>
/// Keep this enum as the <em>system identity</em>; do not overload it with “only work sources.”
/// When SAP (or similar) ships, expect additional <see cref="IntegrationProvider"/> values and/or
/// separate entity graphs keyed by <see cref="IntegrationConnection"/>, rather than forcing SAP into
/// <see cref="ExternalWorkSource"/> if it is not work-item ingestion.
/// </para>
/// </remarks>
public enum IntegrationProvider
{
    SharePoint = 0,
    Jira = 1,
    ServiceNow = 2,
}
