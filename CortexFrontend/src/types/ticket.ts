/** Result of saving from the ticket modal (parent may keep the modal open after a conflict reload). */
export type TicketSaveOutcome = "saved" | "reloaded";

/** Intake lifecycle; future work may add notifications per transition (e.g. approved, returned, rejected). */
export type ApprovalStatus =
  | "PendingApproval"
  | "Approved"
  | "NeedsMoreInfo"
  | "Rejected";

/** Operational boards and SLA apply only after approval (missing ⇒ legacy Approved). */
export function isTicketApproved(ticket: Ticket): boolean {
  return (ticket.approvalStatus ?? "Approved") === "Approved";
}

/** Populated when the API provides AI-assisted triage hints; all fields optional. */
export type AdvisorySlaRiskTier = "Low" | "Medium" | "High";

export interface ApprovalTriagePreview {
  summary?: string;
  suggestedPriority?: string;
  /** Short sentence explaining the suggested priority (Phase 1 advisory). */
  priorityReason?: string;
  /** AI-suggested workflow status from Cortex-configured vocabulary (advisory). */
  suggestedStatus?: string;
  missingDetailHints?: string[];
  /** Advisory: delivery-pressure signal from ticket clarity / complexity (not a breach prediction). */
  potentialSlaRisk?: AdvisorySlaRiskTier | string;
  slaRiskReason?: string;
}

/** Response from POST /api/tickets/{id}/triage (camelCase JSON). */
export interface TicketTriageGenerateApiResponse {
  summary?: string | null;
  suggestedPriority?: string | null;
  priorityReason?: string | null;
  suggestedStatus?: string | null;
  missingDetails?: string[] | null;
  potentialSlaRisk?: string | null;
  slaRiskReason?: string | null;
  unavailable?: boolean;
  unavailableReason?: string | null;
}

export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: string;
  approvalStatus?: ApprovalStatus;
  /** Future: server-provided advisory triage; omit until backend sends real data. */
  approvalTriagePreview?: ApprovalTriagePreview | null;
  priority: string;
  department?: string;
  boardId: number;
  boardName: string;
  storyPoints?: number;
  synitiOwner?: string;
  businessOwner?: string;
  /** Resolved display labels from API for read-only UI (opaque values remain in synitiOwner/businessOwner). */
  synitiOwnerDisplayName?: string;
  businessOwnerDisplayName?: string;
  createdBy: string;
  createdByUser?:
    | {
        id: number;
        displayName: string;
      }
    | undefined;
  createdDate: string;
  lastModifiedBy?: string;
  lastModifiedDate?: string;
  createdByDisplayName?: string;
  createdByEmail?: string;
  createdByAuth0Id?: string;
  approvedAt?: string;
  approvedBy?: number;
  rejectedAt?: string;
  rejectedBy?: number;
  rejectionReason?: string;
  returnedForDetailAt?: string;
  returnedForDetailBy?: number;
  returnReason?: string;
  slaTargetDate: string;
  slaCompletedDate?: string;
  slaStatus: string;
  slaRemainingMinutes: number;
  isSlaBreached: boolean;
  /** Base64 row version from API; required when updating an existing ticket. */
  concurrencyToken?: string;
}

export interface TicketMutationInput {
  title?: string;
  description?: string;
  status?: string;
  priority?: string;
  department?: string;
  boardId?: number;
  storyPoints?: number;
  synitiOwner?: string;
  businessOwner?: string;
  changeReason?: string;
  concurrencyToken?: string;
}

export interface CreateTicketInput extends Omit<TicketMutationInput, "status"> {
  title: string;
  description: string;
  priority: string;
}
