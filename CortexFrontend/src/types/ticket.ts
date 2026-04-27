import type { ScreenshotInsightPersisted } from "./screenshotInsight";

/** @deprecated Use `TicketSaveResult` — supports stay-open on edit save. */
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

/**
 * Reviewer apply request for POST /api/tickets/{id}/triage/apply.
 * Applies persisted AI triage suggestions to canonical fields without a new AI call.
 * At least one of applyPriority / applyStatus must be true.
 */
export interface TicketTriageApplyRequest {
  applyPriority: boolean;
  applyStatus: boolean;
  /** Optional short reviewer rationale recorded in ticket audit history. */
  changeReason?: string;
}

export type OperationalRiskLevel = "low" | "moderate" | "high" | "critical";
export type OwnerPressureLevel = "low" | "moderate" | "high" | "critical";

export interface OwnerPressureAssessment {
  workloadScore: number;
  pressureLevel: OwnerPressureLevel;
}

export interface OperationalRiskAssessment {
  operationalRiskScore: number;
  riskLevel: OperationalRiskLevel | string;
  reasons: string[];
  recommendedAction: string;
  ownerPressure: OwnerPressureAssessment;
  isAssignmentSafe: boolean;
  isOwnerOverloaded: boolean;
  isOwnershipComplete: boolean;
}

export interface ReassignmentOwnerSnapshot {
  userId?: number | null;
  ownerKey: string;
  displayName: string;
  workloadScore: number;
  pressureLevel: OwnerPressureLevel | string;
}

export interface ReassignmentTarget extends ReassignmentOwnerSnapshot {
  isBetterThanCurrent: boolean;
  improvementReason: string;
}

export interface ReassignmentRecommendation {
  shouldSuggestReassignment: boolean;
  reason: string;
  assignmentField?: "synitiOwner" | "businessOwner" | "unassigned" | string;
  currentOwner?: ReassignmentOwnerSnapshot | null;
  suggestedTargets: ReassignmentTarget[];
}

export interface DecisionImpact {
  hasImpact: boolean;
  previousRiskLevel: OperationalRiskLevel | string;
  currentRiskLevel: OperationalRiskLevel | string;
  riskImproved: boolean;
  previousOwnerWorkload: number;
  currentOwnerWorkload: number;
  workloadImproved: boolean;
  previousPressureLevel: OwnerPressureLevel | string;
  currentPressureLevel: OwnerPressureLevel | string;
  pressureImproved: boolean;
  summary: string;
  appliedAtUtc: string;
  source: string;
}

export interface ReassignmentApplyRequest {
  ticketId: string;
  selectedOwnerId: number;
  reason?: string;
  source?: string;
  concurrencyToken?: string;
  expectedCurrentOwnerKey?: string;
}

export interface ReassignmentApplyResponse {
  ticketId: string;
  previousOwner: string;
  newOwner: string;
  applied: boolean;
  appliedAtUtc: string;
  auditMessage: string;
  reassignmentSource: string;
  ticket?: Ticket | null;
}

export interface Ticket {
  id: string;
  title: string;
  description: string;
  status: string;
  approvalStatus?: ApprovalStatus;
  /** Future: server-provided advisory triage; omit until backend sends real data. */
  approvalTriagePreview?: ApprovalTriagePreview | null;
  /** Last successful attachment (screenshot) AI insight persisted on the ticket. */
  screenshotInsight?: ScreenshotInsightPersisted | null;
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
  operationalRisk?: OperationalRiskAssessment | null;
  reassignmentRecommendation?: ReassignmentRecommendation | null;
  decisionImpact?: DecisionImpact | null;
  /** Base64 row version from API; required when updating an existing ticket. */
  concurrencyToken?: string;
}

/** Result of saving from the ticket modal (parent coordinates list + modal lifecycle). */
export type TicketSaveResult =
  | { outcome: "saved"; savedTicket: Ticket; shouldCloseModal: boolean }
  | { outcome: "reloaded" };

/** Optional payload for POST/PUT ticket saves (workflow metrics only). */
export interface IntakeAssistSaveMetrics {
  intakeAssistUsedBeforeSave: boolean;
  lastIntakeClarityState?: string;
  lastIntakeMissingDetailCount?: number;
}

/** POST /api/tickets/{id}/metrics/reviewer-quality-signal */
export type ReviewerQualitySignalKind = "none" | "ready" | "gaps" | "needs_detail";

export interface ReviewerQualitySignalMetricsPayload {
  reviewerSignal: ReviewerQualitySignalKind;
  missingDetailHintCount?: number;
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
  intakeAssistSave?: IntakeAssistSaveMetrics;
}

export interface CreateTicketInput extends Omit<TicketMutationInput, "status"> {
  title: string;
  description: string;
  priority: string;
}
