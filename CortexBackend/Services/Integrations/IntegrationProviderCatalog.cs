using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Authoritative provider profiles for governed integration setup.</summary>
public static class IntegrationProviderCatalog
{
    private static readonly IReadOnlyList<IntegrationProviderProfile> Profiles = Build();

    public static IReadOnlyList<IntegrationProviderProfile> All => Profiles;

    public static IntegrationProviderProfile? TryGet(IntegrationProvider provider) =>
        Profiles.FirstOrDefault(p => p.Provider == provider);

    public static IntegrationProviderProfile Get(IntegrationProvider provider) =>
        TryGet(provider) ?? throw new IntegrationApiException(400, "Unknown integration provider.");

    private static IReadOnlyList<IntegrationProviderProfile> Build() =>
    [
        new IntegrationProviderProfile(
            IntegrationProvider.SharePoint,
            "SharePoint",
            "Connect a SharePoint list for read-only work item intake and field discovery using Microsoft Graph.",
            [
                IntegrationAuthMode.AppRegistration,
                IntegrationAuthMode.OAuth,
                IntegrationAuthMode.Manual,
            ],
            [IntegrationSyncMode.ReadOnly, IntegrationSyncMode.Manual],
            [
                new IntegrationProviderFieldRule(
                    key: "tenantId",
                    label: "Microsoft 365 tenant ID",
                    helpText: "Directory (tenant) GUID for the Microsoft 365 organization that hosts the SharePoint site.",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
                    mapsToConnectionColumn: nameof(IntegrationConnection.TenantId),
                    validationHint: "Non-empty identifier (GUID recommended)."),
                new IntegrationProviderFieldRule(
                    key: "siteUrl",
                    label: "SharePoint site URL or site ID",
                    helpText: "Site URL, Graph site id, or path — used together with your work source configuration.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    placeholder: "https://contoso.sharepoint.com/sites/MySite",
                    mapsToConnectionColumn: nameof(IntegrationConnection.OrganizationId),
                    validationHint: "Must look like a URL or a site identifier."),
                new IntegrationProviderFieldRule(
                    key: "graphPermissionContext",
                    label: "Permission context",
                    helpText:
                    "Describes the Microsoft Graph permission posture expected for read-only list access (configured on the app registration).",
                    fieldType: "select",
                    required: false,
                    isSecret: false,
                    allowedValues: ["ReadOnlyWorkItems", "SitesSelected"],
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "clientSecret",
                    label: "App client secret",
                    helpText:
                    "Not stored in Cortex per connection. Configure the app registration secret in secure host configuration. This field captures intent only when storage ships.",
                    fieldType: "secret",
                    required: false,
                    isSecret: true,
                    validationHint: "Never send real secrets in provider settings JSON."),
            ],
            true,
            true,
            true,
            false),

        new IntegrationProviderProfile(
            IntegrationProvider.Jira,
            "Jira",
            "Connect a Jira project for read-only issue discovery and future field mapping. No automatic sync in this version.",
            [IntegrationAuthMode.ApiToken, IntegrationAuthMode.OAuth, IntegrationAuthMode.Manual],
            [IntegrationSyncMode.ReadOnly, IntegrationSyncMode.Manual],
            [
                new IntegrationProviderFieldRule(
                    key: "baseUrl",
                    label: "Jira base URL",
                    helpText: "Cloud or Data Center site base URL (for example https://your-domain.atlassian.net).",
                    fieldType: "url",
                    required: true,
                    isSecret: false,
                    placeholder: "https://your-domain.atlassian.net",
                    validationHint: "https URL."),
                new IntegrationProviderFieldRule(
                    key: "projectKey",
                    label: "Project key",
                    helpText: "Short project key for issues that should surface in Cortex (for example PROJ).",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "PROJ",
                    validationHint: "Letters, numbers, underscore — typical Jira project key shape."),
                new IntegrationProviderFieldRule(
                    key: "issueType",
                    label: "Primary issue type",
                    helpText: "Default issue type name or id used when reviewing imported issues (for example Task).",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "Task",
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "jqlFilter",
                    label: "JQL filter (optional)",
                    helpText: "Optional JQL fragment to limit which issues are in scope for discovery previews.",
                    fieldType: "textarea",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "cloudId",
                    label: "Cloud ID (optional)",
                    helpText: "Optional Atlassian cloud id for advanced OAuth scenarios.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "apiToken",
                    label: "API token",
                    helpText:
                    "Not stored by Cortex in this version. Use a future credential vault integration; never paste secrets into general settings.",
                    fieldType: "secret",
                    required: false,
                    isSecret: true,
                    validationHint: null),
            ],
            false,
            false,
            false,
            false),

        new IntegrationProviderProfile(
            IntegrationProvider.ServiceNow,
            "ServiceNow",
            "Connect a ServiceNow table for read-only record discovery and future field mapping. Use least-privilege service credentials.",
            [
                IntegrationAuthMode.OAuthClientCredentials,
                IntegrationAuthMode.ApiToken,
                IntegrationAuthMode.Manual,
            ],
            [IntegrationSyncMode.ReadOnly, IntegrationSyncMode.Manual],
            [
                new IntegrationProviderFieldRule(
                    key: "instanceUrl",
                    label: "Instance URL",
                    helpText: "ServiceNow instance base URL (for example https://yourinstance.service-now.com).",
                    fieldType: "url",
                    required: true,
                    isSecret: false,
                    placeholder: "https://yourinstance.service-now.com",
                    validationHint: "https URL."),
                new IntegrationProviderFieldRule(
                    key: "tableName",
                    label: "Table name",
                    helpText: "Table API name (for example incident, sc_req_item).",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "incident",
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "assignmentGroup",
                    label: "Assignment group (optional)",
                    helpText: "Filter hint for scoped imports (sys_id or display name, depending on your process).",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "category",
                    label: "Category (optional)",
                    helpText: "Optional category filter for scoped discovery.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "subcategory",
                    label: "Subcategory (optional)",
                    helpText: "Optional subcategory filter.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "encodedQuery",
                    label: "Encoded query (optional)",
                    helpText: "Optional ServiceNow encoded query string for advanced read-only filters.",
                    fieldType: "textarea",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "clientSecret",
                    label: "OAuth client secret",
                    helpText: "Not persisted in Cortex in this version. Use secure host configuration or a vault when available.",
                    fieldType: "secret",
                    required: false,
                    isSecret: true,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "apiToken",
                    label: "API token / password",
                    helpText: "Not persisted in Cortex in this version.",
                    fieldType: "secret",
                    required: false,
                    isSecret: true,
                    validationHint: null),
            ],
            false,
            false,
            false,
            false),

        new IntegrationProviderProfile(
            IntegrationProvider.SapReference,
            "SAP Reference",
            "Configure stored SAP table and field reference metadata. This is not a live SAP ERP connection.",
            [IntegrationAuthMode.ReferenceMetadata, IntegrationAuthMode.Manual],
            [IntegrationSyncMode.ReadOnly, IntegrationSyncMode.Manual],
            [
                new IntegrationProviderFieldRule(
                    key: "sourceName",
                    label: "Reference source name",
                    helpText: "Logical name for the stored SAP reference catalog in Cortex (matches administrator seeding or import targets).",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "Production metadata",
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "systemAlias",
                    label: "System alias",
                    helpText: "Short label for this logical SAP system context (for example ECC, S4DEV).",
                    fieldType: "text",
                    required: true,
                    isSecret: false,
                    placeholder: "ECC",
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "client",
                    label: "Client (optional)",
                    helpText: "Optional SAP client for documentation and scope labeling only.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "moduleScope",
                    label: "Module scope (optional)",
                    helpText: "Optional module filter label (for example MM, SD) for administrator context.",
                    fieldType: "text",
                    required: false,
                    isSecret: false,
                    validationHint: null),
                new IntegrationProviderFieldRule(
                    key: "tableScope",
                    label: "Table scope (optional)",
                    helpText: "Optional comma-separated table names or pattern hints for catalog focus.",
                    fieldType: "textarea",
                    required: false,
                    isSecret: false,
                    validationHint: null),
            ],
            false,
            false,
            false,
            true),
    ];
}
