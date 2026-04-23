import type { Ticket } from "../types/ticket";

export type ActivitySignal = {
  minutesSince: number;
  /** Relative age: "2h ago", "3d ago" */
  label: string;
  isStale: boolean;
  textClass: string;
};

export type OwnershipGapChip = {
  label: string;
  chipClass: string;
};

/** No update for this long → "stale" chip and amber emphasis. */
const STALE_MINUTES = 48 * 60;
/** Suppress signal for very recently updated tickets. */
const MIN_SIGNAL_MINUTES = 4 * 60;

function formatAge(totalMinutes: number): string {
  if (totalMinutes < 60) return `${totalMinutes}m`;
  const hours = Math.floor(totalMinutes / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.floor(hours / 24);
  return `${days}d`;
}

/** Returns "Xh ago" / "Xd ago" from an ISO date string. */
export function formatTimeAgo(dateStr: string): string {
  const minutes = Math.floor((Date.now() - new Date(dateStr).getTime()) / 60000);
  return `${formatAge(minutes)} ago`;
}

/**
 * Inactivity signal for active, assigned tickets. Returns null for resolved/closed,
 * freshly updated, or unresolvable dates.
 */
export function getActivitySignal(ticket: Ticket): ActivitySignal | null {
  if (ticket.status === "Resolved" || ticket.status === "Closed") return null;
  const ref = ticket.lastModifiedDate ?? ticket.createdDate;
  if (!ref) return null;

  const minutesSince = Math.floor(
    (Date.now() - new Date(ref).getTime()) / 60000,
  );
  if (minutesSince < MIN_SIGNAL_MINUTES) return null;

  const isStale = minutesSince >= STALE_MINUTES;
  return {
    minutesSince,
    label: `${formatAge(minutesSince)} ago`,
    isStale,
    textClass: isStale
      ? "text-amber-600 dark:text-amber-400"
      : "text-gray-500 dark:text-slate-500",
  };
}

/**
 * Ownership gap chip for active approved tickets with no syniti owner.
 * Returns null when the ticket is assigned, resolved, or in the intake flow.
 */
export function getOwnershipGapChip(ticket: Ticket): OwnershipGapChip | null {
  const approvalStatus = ticket.approvalStatus ?? "Approved";
  if (approvalStatus !== "Approved") return null;
  if (ticket.status === "Resolved" || ticket.status === "Closed") return null;
  if (ticket.synitiOwner) return null;

  return {
    label: "Unassigned",
    chipClass:
      "border-slate-300 bg-slate-50 text-slate-700 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-300",
  };
}

/**
 * Human-readable label for who currently needs to act on this ticket.
 * Derived from approval lifecycle + operational status — no backend field required.
 */
export function getWaitingOnLabel(ticket: Ticket): string | null {
  const approvalStatus = ticket.approvalStatus ?? "Approved";
  if (approvalStatus === "PendingApproval") return "Waiting on Reviewer";
  if (approvalStatus === "NeedsMoreInfo") return "Waiting on Requester";
  if (ticket.status === "Resolved" || ticket.status === "Closed") return null;
  if (ticket.status === "Pending Business Review")
    return "Waiting on Business Owner";
  if (!ticket.synitiOwner) return "Waiting on Assignment";
  return null;
}
