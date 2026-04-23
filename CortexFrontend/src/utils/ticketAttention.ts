import type { Ticket } from "../types/ticket";
import { getActivitySignal, getWaitingOnLabel } from "./ticketActivity";
import { isOpenTicket } from "./ticketLifecycle";
import { getSlaDisplayLabel } from "./ticketSla";

export const ATTENTION_FILTER_OPTIONS = [
  "overdue",
  "sla-risk",
  "stale",
  "unassigned",
  "waiting-business",
  "waiting-reviewer",
  "immediate",
  "ownership-gaps",
  "blocked-waiting",
] as const;

export type AttentionFilterValue = (typeof ATTENTION_FILTER_OPTIONS)[number];

export const ATTENTION_FILTER_LABELS: Record<AttentionFilterValue, string> = {
  overdue: "Overdue",
  "sla-risk": "SLA Risk",
  stale: "Stale",
  unassigned: "Unassigned",
  "waiting-business": "Waiting on Business",
  "waiting-reviewer": "Waiting on Reviewer",
  immediate: "Needs Immediate Attention",
  "ownership-gaps": "Ownership Gaps",
  "blocked-waiting": "Blocked / Waiting",
};

export type ExecutiveSummaryCounts = {
  overdue: number;
  slaRisk: number;
  stale: number;
  unassigned: number;
  waitingBusiness: number;
  waitingReviewer: number;
};

const ATTENTION_FILTER_VALUE_SET = new Set<string>(ATTENTION_FILTER_OPTIONS);
const URGENT_PRIORITIES = new Set(["critical", "high"]);

function normalize(value?: string | null) {
  return value?.trim().toLowerCase() ?? "";
}

export function isAttentionFilterValue(
  value: string,
): value is AttentionFilterValue {
  return ATTENTION_FILTER_VALUE_SET.has(value);
}

export function isTicketOverdue(ticket: Ticket) {
  return isOpenTicket(ticket) && getSlaDisplayLabel(ticket) === "Overdue";
}

export function isTicketSlaRisk(ticket: Ticket) {
  return isOpenTicket(ticket) && getSlaDisplayLabel(ticket) === "At Risk";
}

export function isTicketStale(ticket: Ticket) {
  return isOpenTicket(ticket) && getActivitySignal(ticket)?.isStale === true;
}

export function isTicketUnassigned(ticket: Ticket) {
  const approvalStatus = ticket.approvalStatus ?? "Approved";
  return (
    isOpenTicket(ticket) &&
    approvalStatus === "Approved" &&
    normalize(ticket.synitiOwner) === ""
  );
}

export function isWaitingOnAssignment(ticket: Ticket) {
  return (
    isTicketUnassigned(ticket) ||
    getWaitingOnLabel(ticket) === "Waiting on Assignment"
  );
}

export function isWaitingOnBusiness(ticket: Ticket) {
  return (
    isOpenTicket(ticket) &&
    getWaitingOnLabel(ticket) === "Waiting on Business Owner"
  );
}

export function isWaitingOnReviewer(ticket: Ticket) {
  return getWaitingOnLabel(ticket) === "Waiting on Reviewer";
}

export function isTicketUrgent(ticket: Ticket) {
  return (
    URGENT_PRIORITIES.has(normalize(ticket.priority)) ||
    isTicketOverdue(ticket) ||
    isTicketSlaRisk(ticket)
  );
}

export function needsImmediateAttention(ticket: Ticket) {
  return isTicketOverdue(ticket) || (isTicketStale(ticket) && isTicketUrgent(ticket));
}

export function hasOwnershipGap(ticket: Ticket) {
  return isTicketUnassigned(ticket) || isWaitingOnAssignment(ticket);
}

export function isBlockedOrWaiting(ticket: Ticket) {
  return isWaitingOnBusiness(ticket) || isWaitingOnReviewer(ticket);
}

export function ticketMatchesAttentionFilter(
  ticket: Ticket,
  filterValue: AttentionFilterValue,
) {
  switch (filterValue) {
    case "overdue":
      return isTicketOverdue(ticket);
    case "sla-risk":
      return isTicketSlaRisk(ticket);
    case "stale":
      return isTicketStale(ticket);
    case "unassigned":
      return isTicketUnassigned(ticket);
    case "waiting-business":
      return isWaitingOnBusiness(ticket);
    case "waiting-reviewer":
      return isWaitingOnReviewer(ticket);
    case "immediate":
      return needsImmediateAttention(ticket);
    case "ownership-gaps":
      return hasOwnershipGap(ticket);
    case "blocked-waiting":
      return isBlockedOrWaiting(ticket);
  }
}

export function buildExecutiveSummaryCounts(
  tickets: Ticket[],
): ExecutiveSummaryCounts {
  return tickets.reduce<ExecutiveSummaryCounts>(
    (counts, ticket) => {
      if (isTicketOverdue(ticket)) counts.overdue += 1;
      if (isTicketSlaRisk(ticket)) counts.slaRisk += 1;
      if (isTicketStale(ticket)) counts.stale += 1;
      if (isTicketUnassigned(ticket)) counts.unassigned += 1;
      if (isWaitingOnBusiness(ticket)) counts.waitingBusiness += 1;
      if (isWaitingOnReviewer(ticket)) counts.waitingReviewer += 1;
      return counts;
    },
    {
      overdue: 0,
      slaRisk: 0,
      stale: 0,
      unassigned: 0,
      waitingBusiness: 0,
      waitingReviewer: 0,
    },
  );
}
