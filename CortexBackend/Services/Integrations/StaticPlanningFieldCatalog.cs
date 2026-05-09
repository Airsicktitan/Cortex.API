using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Static, advisory field guidance for providers without live discovery (v1).</summary>
public static class StaticPlanningFieldCatalog
{
    public static IReadOnlyList<PlanningFieldDefinitionDto> ForProvider(IntegrationProvider provider) =>
        provider switch
        {
            IntegrationProvider.Jira => JiraFields,
            IntegrationProvider.ServiceNow => ServiceNowFields,
            _ => [],
        };

    private static readonly PlanningFieldDefinitionDto[] JiraFields =
    [
        new("summary", "Summary", "string", false, true, CortexField.Title,
            "Common default for issue titles in Jira.", "Strong"),
        new("description", "Description", "richtext", false, false, CortexField.Description,
            "Maps to Cortex ticket narrative / body.", "Strong"),
        new("priority", "Priority", "priority", false, false, CortexField.Priority,
            "Priority object or name; interpreted after mapping.", "Suggested"),
        new("components", "Components", "array", true, false, CortexField.Department,
            "Often used for component / team slices; maps to department-style context.", "Suggested"),
        new("labels", "Labels", "array", true, false, CortexField.Category,
            "Labels frequently align with categories or tags.", "Possible"),
        new("issuetype", "Issue type", "issuetype", false, false, CortexField.Category,
            "Supports filtering and context; not a routing control.", "Possible"),
    ];

    private static readonly PlanningFieldDefinitionDto[] ServiceNowFields =
    [
        new("short_description", "Short description", "string", false, true, CortexField.Title,
            "Typical short title on incidents and tasks.", "Strong"),
        new("description", "Description", "long string", false, false, CortexField.Description,
            "Longer narrative for the record.", "Strong"),
        new("impact", "Impact", "string", false, false, CortexField.Priority,
            "Review together with urgency when deriving Cortex priority.", "Suggested"),
        new("urgency", "Urgency", "string", false, false, CortexField.Priority,
            "Review together with impact when deriving Cortex priority.", "Suggested"),
        new("assignment_group", "Assignment group", "reference", false, false, CortexField.BusinessOwner,
            "Often reflects owning team; advisory context only.", "Suggested"),
        new("assigned_to", "Assigned to", "reference", false, false, CortexField.SynitiOwner,
            "Maps as owner-style context; does not change Cortex routing by itself.", "Possible"),
        new("category", "Category", "string", true, false, CortexField.Category,
            "Service categorization.", "Suggested"),
        new("subcategory", "Subcategory", "string", true, false, CortexField.Category,
            "Refines category context.", "Possible"),
    ];
}
