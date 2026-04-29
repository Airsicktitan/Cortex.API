import { screenshotInsightPersistedHasContent } from "../types/screenshotInsight";
import {
  isTicketApproved,
  type ApprovalTriagePreview,
  type Ticket,
} from "../types/ticket";

/**
 * Reviewer-facing intake quality derived from persisted Phase 1 triage
 * (`missingDetailHints` — same missing-detail concept as intake assist, no new AI).
 */
export type ReviewerIntakeQualityKind =
  | "none"
  | "ready"
  | "gaps"
  | "needs_detail";

/**
 * Maps triage missing-detail count to display bands aligned with intake clarity labels:
 * 0 → ready, 1–2 → small gaps, 3+ → needs detail first.
 * Returns `"none"` when there is no triage content.
 */
export function deriveReviewerIntakeQualitySignal(
  preview: ApprovalTriagePreview | null | undefined,
): ReviewerIntakeQualityKind {
  if (!preview || !triageHasContent(preview)) {
    return "none";
  }
  const n = preview.missingDetailHints?.length ?? 0;
  if (n === 0) {
    return "ready";
  }
  if (n <= 2) {
    return "gaps";
  }
  return "needs_detail";
}

export function getReviewerIntakeQualityCopy(kind: ReviewerIntakeQualityKind): {
  title: string;
  body: string;
} {
  switch (kind) {
    case "needs_detail":
      return {
        title: "Cortex improvement available",
        body: "This request has gaps that would require reviewer follow-up. Fill in the missing details to submit review-ready.",
      };
    case "gaps":
      return {
        title: "Small gaps remain",
        body: "Some details may need clarification during review.",
      };
    case "ready":
      return {
        title: "Ready for review",
        body: "This request appears actionable without follow-up.",
      };
    default:
      return {
        title: "No intake analysis available",
        body: "Run reviewer analysis from Decision to evaluate completeness and missing details.",
      };
  }
}

export function triageHasContent(
  triage: ApprovalTriagePreview | null | undefined,
): boolean {
  if (!triage) {
    return false;
  }
  if (triage.summary?.trim()) {
    return true;
  }
  if (triage.suggestedPriority?.trim()) {
    return true;
  }
  if (triage.priorityReason?.trim()) {
    return true;
  }
  if (triage.suggestedStatus?.trim()) {
    return true;
  }
  if (triage.potentialSlaRisk?.trim()) {
    return true;
  }
  if (triage.slaRiskReason?.trim()) {
    return true;
  }
  return (triage.missingDetailHints?.length ?? 0) > 0;
}

/** Reviewer modal: hide the triage rail when approved and there is nothing to show. */
export function shouldShowApprovalTriageModalPanel(ticket: Ticket): boolean {
  if (
    isTicketApproved(ticket) &&
    !triageHasContent(ticket.approvalTriagePreview) &&
    !screenshotInsightPersistedHasContent(ticket.screenshotInsight)
  ) {
    return false;
  }
  return true;
}
