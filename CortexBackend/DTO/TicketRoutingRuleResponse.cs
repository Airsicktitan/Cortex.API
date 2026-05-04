namespace Cortex.API.DTO;

public class TicketRoutingRuleResponse
{
    public int Id { get; set; }
    public string BoardId { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequesterDepartment { get; set; } = string.Empty;
    public string RequesterRole { get; set; } = string.Empty;
    public int RulePriority { get; set; }
    public int Weight { get; set; }
    public string Department { get; set; } = string.Empty;
    public string TitleContains { get; set; } = string.Empty;
    public string SynitiOwner { get; set; } = string.Empty;
    public string BusinessOwner { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    /// <summary>
    /// True when both Syniti and business owners (where set) resolve to currently eligible users.
    /// Legacy rules saved before owner-eligibility validation may report false; the rule is still
    /// stored as-is and is silently skipped at evaluation time.
    /// </summary>
    public bool IsValidConfiguration { get; set; } = true;

    /// <summary>Why the rule is invalid, e.g. "Syniti owner is not eligible". Null when valid.</summary>
    public string? InvalidReason { get; set; }

    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
