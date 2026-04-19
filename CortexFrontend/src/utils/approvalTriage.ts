import {
  isTicketApproved,
  type ApprovalTriagePreview,
  type Ticket,
} from "../types/ticket";

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
  if (isTicketApproved(ticket) && !triageHasContent(ticket.approvalTriagePreview)) {
    return false;
  }
  return true;
}
