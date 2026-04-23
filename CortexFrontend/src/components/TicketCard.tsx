import type { ReactNode } from "react";
import type { ApprovalStatus, Ticket } from "../types/ticket";
import {
  buildSlaTooltip,
  getUrgencyChip,
  getSlaAccentClass,
  getSlaDisplayLabel,
} from "../utils/ticketSla";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";
import { CortexTooltip } from "./ui/Tooltip";
import {
  getActivitySignal,
  getOwnershipGapChip,
} from "../utils/ticketActivity";

interface TicketCardProps {
  ticket: Ticket;
  onClick?: () => void;
  /** Extra chip (e.g. intake / approval state) shown next to status. */
  secondaryStatusLabel?: string;
  /** Rendered below the card; clicks do not trigger `onClick`. */
  footerSlot?: ReactNode;
  /** Controls whether approval lifecycle state is shown for this ticket card. */
  approvalDisplayContext?: "active" | "requester" | "approvalQueue";
}

const priorityColors = {
  Critical: "bg-red-100 text-red-800 border-red-300",
  High: "bg-orange-100 text-orange-800 border-orange-300",
  Medium: "bg-yellow-100 text-yellow-800 border-yellow-300",
  Low: "bg-green-100 text-green-800 border-green-300",
};

const statusColors = {
  New: "bg-cortex-blue-soft text-cortex-ink",
  "In Progress": "bg-purple-100 text-purple-800",
  "Pending Business Review": "bg-amber-100 text-amber-800",
  Resolved: "bg-green-100 text-green-800",
  Closed: "bg-gray-100 text-gray-800",
};

function getTicketApprovalStatus(ticket: Ticket): ApprovalStatus {
  return ticket.approvalStatus ?? "Approved";
}

/** Intake / approval chip when `secondaryStatusLabel` is not passed explicitly. */
function approvalIntakeChipLabel(ticket: Ticket): string | undefined {
  switch (getTicketApprovalStatus(ticket)) {
    case "PendingApproval":
      return "Pending Approval";
    case "Approved":
      return "Approved";
    case "NeedsMoreInfo":
      return "Needs More Info";
    case "Rejected":
      return "Rejected";
    default:
      return undefined;
  }
}

function approvalChipClassName(ticket: Ticket): string {
  const chipBaseClass =
    "inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium";
  switch (getTicketApprovalStatus(ticket)) {
    case "Approved":
      return `${chipBaseClass} border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-100`;
    case "Rejected":
      return `${chipBaseClass} border-red-200 bg-red-50 text-red-900 dark:border-red-900/50 dark:bg-red-950/40 dark:text-red-100`;
    case "PendingApproval":
    case "NeedsMoreInfo":
      return `${chipBaseClass} border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-100`;
    default:
      return `${chipBaseClass} border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-100`;
  }
}

/** Stronger chip for default intake approval labels so lifecycle reads as primary. */
function approvalLifecyclePrimaryChipClass(ticket: Ticket): string {
  const base =
    "inline-flex items-center rounded-full border px-3 py-1.5 text-sm font-semibold tracking-tight";
  switch (getTicketApprovalStatus(ticket)) {
    case "Approved":
      return `${base} border-emerald-300 bg-emerald-50 text-emerald-950 shadow-sm dark:border-emerald-700 dark:bg-emerald-950/45 dark:text-emerald-100`;
    case "Rejected":
      return `${base} border-red-300 bg-red-50 text-red-950 shadow-sm dark:border-red-800 dark:bg-red-950/45 dark:text-red-100`;
    case "PendingApproval":
    case "NeedsMoreInfo":
      return `${base} border-amber-300 bg-amber-50 text-amber-950 shadow-sm dark:border-amber-700 dark:bg-amber-950/45 dark:text-amber-100`;
    default:
      return approvalChipClassName(ticket);
  }
}

function approvalLifecycleHelperLine(
  ticket: Ticket,
  context: TicketCardProps["approvalDisplayContext"],
): string | null {
  switch (getTicketApprovalStatus(ticket)) {
    case "PendingApproval":
      return "Awaiting reviewer approval before active work begins.";
    case "NeedsMoreInfo":
      return "More information is required before this request can be approved.";
    case "Rejected":
      return "This request was not approved.";
    case "Approved":
      return context === "requester"
        ? "Approved and now part of active work."
        : "Approved for active work.";
    default:
      return null;
  }
}

function requesterLifecycleAccentClass(ticket: Ticket): string {
  switch (getTicketApprovalStatus(ticket)) {
    case "NeedsMoreInfo":
      return "border-l-amber-500 dark:border-l-amber-400";
    case "PendingApproval":
      return "border-l-cortex-blue dark:border-l-cortex-blue";
    case "Rejected":
      return "border-l-rose-500 dark:border-l-rose-400";
    case "Approved":
    default:
      return "border-l-emerald-500 dark:border-l-emerald-400";
  }
}

function requesterLifecycleCardToneClass(ticket: Ticket): string {
  switch (getTicketApprovalStatus(ticket)) {
    case "NeedsMoreInfo":
      return "border-amber-200/80 bg-amber-50/50 dark:border-amber-800/60 dark:bg-amber-950/15";
    case "PendingApproval":
      return "border-cortex-blue/20 bg-cortex-blue/5 dark:border-cortex-blue/30 dark:bg-cortex-blue/10";
    case "Rejected":
      return "border-rose-200/80 bg-rose-50/40 dark:border-rose-900/40 dark:bg-rose-950/15";
    default:
      return "";
  }
}

function formatCardDate(value: string | undefined): string | null {
  if (!value) {
    return null;
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return null;
  }

  const now = new Date();
  const options: Intl.DateTimeFormatOptions =
    parsed.getFullYear() === now.getFullYear()
      ? { month: "short", day: "numeric" }
      : { month: "short", day: "numeric", year: "numeric" };

  return parsed.toLocaleDateString(undefined, options);
}

function requesterSubmittedLine(ticket: Ticket): string | null {
  const submitted = formatCardDate(ticket.createdDate);
  if (!submitted) {
    return null;
  }

  const updated = formatCardDate(ticket.lastModifiedDate);
  if (updated && ticket.lastModifiedDate !== ticket.createdDate) {
    return `Submitted ${submitted} · Updated ${updated}`;
  }

  return `Submitted ${submitted}`;
}

function requesterReasonPreview(ticket: Ticket): string | null {
  const approvalStatus = getTicketApprovalStatus(ticket);
  if (approvalStatus === "NeedsMoreInfo") {
    return ticket.returnReason?.trim() || null;
  }
  if (approvalStatus === "Rejected") {
    return ticket.rejectionReason?.trim() || null;
  }
  return null;
}

/** Native `title` tooltips become unwieldy for very long descriptions. */
const DESCRIPTION_TITLE_MAX_LEN = 280;

/** Compact duration for card timing tail (mirrors ticketSla formatDuration). */
function formatDurationShort(totalMinutes: number): string {
  const absoluteMinutes = Math.abs(totalMinutes);

  if (absoluteMinutes === 0) {
    return "0m";
  }

  if (absoluteMinutes < 60) {
    return `${absoluteMinutes}m`;
  }

  if (absoluteMinutes < 24 * 60) {
    const hours = Math.round((absoluteMinutes / 60) * 10) / 10;
    return Number.isInteger(hours) ? `${hours}h` : `${hours.toFixed(1)}h`;
  }

  const days = Math.round((absoluteMinutes / (24 * 60)) * 10) / 10;
  return Number.isInteger(days) ? `${days}d` : `${days.toFixed(1)}d`;
}

function formatDuePhrase(slaTargetDate: string): string {
  const d = new Date(slaTargetDate);
  const now = new Date();
  const startOf = (x: Date) =>
    new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const d0 = startOf(d);
  const t0 = startOf(now);
  const dayDiff = Math.round((d0 - t0) / 86_400_000);

  if (dayDiff === 0) {
    return "Due today";
  }
  if (dayDiff === 1) {
    return "Due tomorrow";
  }
  if (dayDiff === -1) {
    return "Due yesterday";
  }

  const withYear =
    d.getFullYear() !== now.getFullYear()
      ? ({ month: "short", day: "numeric", year: "numeric" } as const)
      : ({ month: "short", day: "numeric" } as const);

  return `Due ${d.toLocaleDateString(undefined, withYear)}`;
}

/** Second segment of the merged timing line (after ·). */
function cardTimingTail(ticket: Ticket): string {
  const label = getSlaDisplayLabel(ticket);
  const minutes = ticket.slaRemainingMinutes;
  const d = formatDurationShort(minutes);

  switch (label) {
    case "Resolved On Time":
      return "Resolved on time";
    case "Resolved Late":
      return `${d} late`;
    case "Overdue":
      return `${d} late`;
    case "At Risk":
      return minutes > 0 ? `${d} remaining` : "At risk";
    case "On Track":
    default:
      if (minutes > 0) {
        return `${d} remaining`;
      }
      return "On track";
  }
}

function mergedTimingLine(ticket: Ticket): string {
  const duePhrase = formatDuePhrase(ticket.slaTargetDate);
  if (duePhrase === "No SLA target") {
    return `SLA target unavailable · ${cardTimingTail(ticket)}`;
  }

  return `SLA ${duePhrase.toLowerCase()} · ${cardTimingTail(ticket)}`;
}

function urgencyTooltip(label: string): string {
  if (label === "Overdue") {
    return "Past SLA target - work is already late.";
  }

  return "Approaching SLA target - work may miss deadline without action.";
}

export default function TicketCard({
  ticket,
  onClick,
  secondaryStatusLabel,
  footerSlot,
  approvalDisplayContext = "requester",
}: TicketCardProps) {
  const shouldSuppressApprovedApprovalState =
    approvalDisplayContext === "active" &&
    getTicketApprovalStatus(ticket) === "Approved";
  const resolvedSecondary =
    secondaryStatusLabel ??
    (shouldSuppressApprovedApprovalState
      ? undefined
      : approvalIntakeChipLabel(ticket));
  const approvalHelper = shouldSuppressApprovedApprovalState
    ? null
    : approvalLifecycleHelperLine(ticket, approvalDisplayContext);
  const approvalStatus = getTicketApprovalStatus(ticket);
  const isRequesterContext = approvalDisplayContext === "requester";
  const isApprovalQueueContext = approvalDisplayContext === "approvalQueue";
  const isRequesterIntakeTicket =
    isRequesterContext && approvalStatus !== "Approved";
  const needsAttention = approvalStatus === "NeedsMoreInfo";
  const requesterMetaLine = isRequesterContext
    ? requesterSubmittedLine(ticket)
    : null;
  const reasonPreview = isRequesterContext ? requesterReasonPreview(ticket) : null;
  const priorityColor =
    priorityColors[ticket.priority as keyof typeof priorityColors] ||
    "bg-gray-100 text-gray-800 border-gray-300";
  const statusColor =
    statusColors[ticket.status as keyof typeof statusColors] ||
    "bg-gray-100 text-gray-800";
  const slaAccentClass = isRequesterContext
    ? requesterLifecycleAccentClass(ticket)
    : getSlaAccentClass(getSlaDisplayLabel(ticket));
  const cardToneClass =
    isRequesterContext && approvalStatus !== "Approved"
      ? requesterLifecycleCardToneClass(ticket)
      : "";
  const slaTooltip = buildSlaTooltip(ticket);
  const urgencyChip = isRequesterContext ? null : getUrgencyChip(ticket);
  const ownershipGapChip = isRequesterIntakeTicket ? null : getOwnershipGapChip(ticket);
  const activity = isRequesterIntakeTicket ? null : getActivitySignal(ticket);
  const showStaleChip = Boolean(activity?.isStale && !isRequesterIntakeTicket);
  const chipBaseClass =
    "inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium";

  const descriptionTitlePreview = (() => {
    const raw = ticket.description?.trim();
    if (!raw) {
      return undefined;
    }
    if (raw.length <= DESCRIPTION_TITLE_MAX_LEN) {
      return raw;
    }
    return `${raw.slice(0, DESCRIPTION_TITLE_MAX_LEN).trimEnd()}…`;
  })();

  return (
    <div
      tabIndex={onClick ? 0 : undefined}
      aria-label={
        onClick ? `Open ticket ${ticket.id}: ${ticket.title}` : undefined
      }
      onClick={onClick}
      onKeyDown={(e) => {
        if (!onClick) {
          return;
        }
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick();
        }
      }}
      className={`cursor-pointer rounded-xl border-l-4 border-t border-r border-b border-gray-100 bg-white p-4 shadow-sm outline-none transition-all duration-150 ease-in-out hover:-translate-y-[2px] hover:border-t-gray-300 hover:border-r-gray-300 hover:border-b-gray-300 hover:shadow-md focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 active:translate-y-0 active:shadow-sm dark:border-t-slate-700 dark:border-r-slate-700 dark:border-b-slate-700 dark:bg-slate-900 dark:hover:border-t-slate-600 dark:hover:border-r-slate-600 dark:hover:border-b-slate-600 dark:focus-visible:ring-offset-slate-900 ${slaAccentClass} ${cardToneClass}`}
    >
      <div className="min-w-0 text-left">
        {descriptionTitlePreview ? (
          <CortexTooltip content={descriptionTitlePreview}>
            <h3
              className="line-clamp-2 cursor-help text-left text-base font-semibold leading-snug tracking-tight text-gray-900 dark:text-slate-100"
              tabIndex={0}
            >
              {ticket.title}
            </h3>
          </CortexTooltip>
        ) : (
          <h3 className="line-clamp-2 text-left text-base font-semibold leading-snug tracking-tight text-gray-900 dark:text-slate-100">
            {ticket.title}
          </h3>
        )}
        <p className="mt-1 text-[11px] tabular-nums text-gray-400 dark:text-slate-500">
          #{formatTicketIdentifier(ticket.id)}
        </p>
        {requesterMetaLine ? (
          <p className="mt-1 text-[11px] leading-snug text-gray-500 dark:text-slate-400">
            {requesterMetaLine}
          </p>
        ) : null}
        {isApprovalQueueContext ? (
          <p className="mt-1 text-[11px] leading-snug text-gray-500 dark:text-slate-400">
            Requester: {formatDisplayValue(ticket.createdByDisplayName)} · Submitted{" "}
            {formatDisplayDateTime(ticket.createdDate)}
          </p>
        ) : null}
      </div>

      <div className="mt-3 flex min-w-0 flex-wrap items-center gap-1.5">
        {resolvedSecondary ? (
          <span
            className={
              secondaryStatusLabel
                ? `${chipBaseClass} border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-100`
                : approvalLifecyclePrimaryChipClass(ticket)
            }
          >
            {resolvedSecondary}
          </span>
        ) : null}
        {isRequesterContext && needsAttention && !secondaryStatusLabel ? (
          <span className="inline-flex items-center rounded-full border border-amber-300 bg-amber-100 px-2.5 py-1 text-xs font-semibold text-amber-950 dark:border-amber-700 dark:bg-amber-900/50 dark:text-amber-100">
            Action needed
          </span>
        ) : null}
        {!isRequesterIntakeTicket ? (
          <span className={`${chipBaseClass} border-transparent ${statusColor}`}>
            {ticket.status}
          </span>
        ) : null}
        {!isRequesterIntakeTicket ? (
          <span className={`${chipBaseClass} ${priorityColor}`}>
            {ticket.priority}
          </span>
        ) : null}
        {!isRequesterIntakeTicket && urgencyChip ? (
          <CortexTooltip content={urgencyTooltip(urgencyChip.label)}>
            <span
              className={`${chipBaseClass} cursor-help ${urgencyChip.chipClass}`}
              tabIndex={0}
            >
              {urgencyChip.label}
            </span>
          </CortexTooltip>
        ) : null}
        {ownershipGapChip ? (
          <CortexTooltip content="No Syniti Owner - no one is currently responsible.">
            <span
              className={`${chipBaseClass} cursor-help ${ownershipGapChip.chipClass}`}
              tabIndex={0}
            >
              {ownershipGapChip.label}
            </span>
          </CortexTooltip>
        ) : null}
        {showStaleChip ? (
          <CortexTooltip content="No update in 48h - confirm status or next action.">
            <span
              className={`${chipBaseClass} cursor-help border-amber-300 bg-amber-50 text-amber-800 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200`}
              tabIndex={0}
            >
              Stale
            </span>
          </CortexTooltip>
        ) : null}
      </div>
      {approvalHelper && !secondaryStatusLabel ? (
        <p
          className={`mt-2 text-left text-[11px] leading-snug ${
            isRequesterContext && needsAttention
              ? "rounded-md border border-amber-200/80 bg-amber-50/80 px-3 py-2 text-amber-950 dark:border-amber-800/70 dark:bg-amber-950/30 dark:text-amber-100"
              : "text-gray-600 dark:text-slate-400"
          }`}
        >
          {approvalHelper}
        </p>
      ) : null}
      {reasonPreview ? (
        <div className="mt-2 rounded-md border border-gray-200/80 bg-white/80 px-3 py-2 text-left dark:border-slate-700 dark:bg-slate-950/35">
          <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-gray-500 dark:text-slate-400">
            {approvalStatus === "NeedsMoreInfo" ? "More information requested" : "Reason"}
          </p>
          <p className="mt-1 line-clamp-2 text-xs leading-snug text-gray-700 dark:text-slate-200">
            {reasonPreview}
          </p>
        </div>
      ) : null}

      <p className="mt-3 text-left text-xs leading-snug text-gray-500 dark:text-slate-500">
        {isRequesterIntakeTicket ? "Requested board: " : ""}
        {formatDisplayValue(ticket.boardName)}
        {ticket.storyPoints !== undefined && ticket.storyPoints !== null
          ? ` · ${ticket.storyPoints} SP`
          : ""}
      </p>

      {!isRequesterIntakeTicket ? (
        <CortexTooltip content={slaTooltip}>
          <p
            className={`mt-3 line-clamp-2 cursor-help text-left text-xs leading-snug ${urgencyChip?.timingClass ?? "text-gray-500 dark:text-slate-500"}`}
            tabIndex={0}
          >
            {mergedTimingLine(ticket)}
          </p>
        </CortexTooltip>
      ) : null}

      {!isRequesterIntakeTicket ? (
        <div className="mt-3 space-y-1 border-t border-gray-100 pt-3 text-left text-[11px] leading-snug text-gray-600 dark:border-slate-800 dark:text-slate-400">
          <CortexTooltip content={formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}>
            <p className="truncate cursor-help" tabIndex={0}>
              <span className="text-gray-500 dark:text-slate-500">Syniti:</span>{" "}
              {formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}
            </p>
          </CortexTooltip>
          <CortexTooltip content={formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}>
            <p className="truncate cursor-help" tabIndex={0}>
              <span className="text-gray-500 dark:text-slate-500">Business:</span>{" "}
              {formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}
            </p>
          </CortexTooltip>
          {activity ? (
            <p className={`truncate ${activity.textClass}`}>
              <span className="text-gray-500 dark:text-slate-500">Updated:</span>{" "}
              {activity.label}
            </p>
          ) : null}
        </div>
      ) : null}

      {footerSlot ? (
        <div
          className="mt-3 border-t border-gray-100 pt-3 dark:border-slate-800"
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
        >
          {footerSlot}
        </div>
      ) : null}
    </div>
  );
}
