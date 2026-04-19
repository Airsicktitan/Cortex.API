using System.Text.Json.Serialization;

namespace Cortex.API.Models;

public class Ticket
{
    public string Id { get; set; } = string.Empty; // Default to empty string
    public string Title { get; set; } = string.Empty; // Default to empty string
    public string Description { get; set; } = string.Empty; // Default to empty string
    public string Status { get; set; } = "New"; // Default status
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.PendingApproval;
    public string Priority { get; set; } = "Medium"; // Default priority
    public int BoardId { get; set; }
    public int? StoryPoints { get; set; }

    public string? SynitiOwner { get; set; } // Nullable
    public string? BusinessOwner { get; set; } // Nullable

    public int CreatedBy { get; set; } 
    public DateTime CreatedDate { get; set; } = DateTime.Now; // Default to now
    public int LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; } // Nullable

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReturnedForDetailAt { get; set; }
    public int? ReturnedForDetailBy { get; set; }
    public string? ReturnReason { get; set; }

    /// <summary>Persisted Phase 1 AI triage (advisory); cleared when absent.</summary>
    public string? AiTriageSummary { get; set; }

    public string? AiTriageSuggestedPriority { get; set; }
    public string? AiTriagePriorityReason { get; set; }

    /// <summary>Last AI-suggested status (canonical name from status definitions), when triage proposed a change.</summary>
    public string? AiTriageSuggestedStatus { get; set; }

    /// <summary>JSON array of strings (<c>missingDetails</c> from the model).</summary>
    public string? AiTriageMissingDetailsJson { get; set; }

    /// <summary>Advisory SLA delivery-pressure signal from AI triage (Low, Medium, High).</summary>
    public string? AiTriagePotentialSlaRisk { get; set; }

    /// <summary>One sentence explaining the advisory SLA risk assessment.</summary>
    public string? AiTriageSlaRiskReason { get; set; }

    public List<Comment> Comments { get; set; } = []; // Initialize to empty list 
    public List<TicketAttachment> Attachments { get; set; } = [];
    
    [JsonIgnore]
    public User? CreatedByUser { get; set; } = null; // Navigation property for creator

    [JsonIgnore]
    public TicketBoardDefinition? BoardDefinition { get; set; }

    /// <summary>SQL Server rowversion for optimistic concurrency (exposed via API as base64 on <c>TicketResponse</c>).</summary>
    [JsonIgnore]
    public byte[] RowVersion { get; set; } = [];
}
