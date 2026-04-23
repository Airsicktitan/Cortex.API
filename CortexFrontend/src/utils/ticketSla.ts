import type { Ticket } from "../types/ticket";

/** User-facing SLA state (derived from API fields + dates; API contract unchanged). */
export type SlaDisplayLabel =
  | "On Track"
  | "At Risk"
  | "Overdue"
  | "Resolved On Time"
  | "Resolved Late";

const SLA_DISPLAY_LABELS = new Set<SlaDisplayLabel>([
  "On Track",
  "At Risk",
  "Overdue",
  "Resolved On Time",
  "Resolved Late",
]);

type SlaLabelInput = Pick<
  Ticket,
  "slaStatus" | "slaTargetDate" | "slaCompletedDate" | "slaRemainingMinutes" | "isSlaBreached"
>;

/** Left-edge accent for ticket cards: light + dark pairs so SLA state stays visible on slate backgrounds. */
const slaAccentClasses: Record<SlaDisplayLabel, string> = {
  "On Track":
    "border-l-green-500 dark:border-l-emerald-400",
  "At Risk":
    "border-l-amber-500 dark:border-l-amber-400",
  Overdue: "border-l-red-500 dark:border-l-red-400",
  "Resolved On Time":
    "border-l-emerald-600 dark:border-l-emerald-400",
  "Resolved Late": "border-l-rose-600 dark:border-l-rose-400",
};

const slaBadgeClasses: Record<SlaDisplayLabel, string> = {
  "On Track": "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-200",
  "At Risk": "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-200",
  Overdue: "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200",
  "Resolved On Time":
    "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200",
  "Resolved Late": "bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200",
};

/** Map API `slaStatus` bucket (report rows) to the label shown in the UI. */
export function mapBackendSlaStatusToDisplayLabel(raw: string): SlaDisplayLabel {
  const status = raw.trim();
  switch (status) {
    case "Met":
      return "Resolved On Time";
    case "Breached":
      return "Overdue";
    case "On Track":
    case "At Risk":
    case "Resolved Late":
      return status;
    default:
      return "On Track";
  }
}

/**
 * Derives a clear SLA label from `slaStatus`, breach flag, and due/completed timestamps.
 * Backend still sends the original `slaStatus` string; this is display-only.
 */
export function getSlaDisplayLabel(ticket: SlaLabelInput): SlaDisplayLabel {
  const raw = ticket.slaStatus?.trim() ?? "";
  const targetMs = new Date(ticket.slaTargetDate).getTime();
  const completedMs = ticket.slaCompletedDate
    ? new Date(ticket.slaCompletedDate).getTime()
    : null;

  if (completedMs !== null && !Number.isNaN(completedMs) && !Number.isNaN(targetMs)) {
    if (raw === "Met") return "Resolved On Time";
    if (raw === "Resolved Late") return "Resolved Late";
    if (raw === "Breached") return "Resolved Late";
    return completedMs <= targetMs ? "Resolved On Time" : "Resolved Late";
  }

  if (raw === "Breached" || ticket.isSlaBreached === true) return "Overdue";
  if (raw === "At Risk") return "At Risk";
  if (raw === "On Track") return "On Track";

  if (!Number.isNaN(targetMs) && Date.now() > targetMs) return "Overdue";

  return "On Track";
}

function normalizeSlaStyleKey(statusOrLabel: string): SlaDisplayLabel {
  const trimmed = statusOrLabel.trim();
  if (SLA_DISPLAY_LABELS.has(trimmed as SlaDisplayLabel)) {
    return trimmed as SlaDisplayLabel;
  }
  return mapBackendSlaStatusToDisplayLabel(trimmed);
}

export function getSlaAccentClass(statusOrLabel: string) {
  return (
    slaAccentClasses[normalizeSlaStyleKey(statusOrLabel)] ??
    "border-l-gray-300 dark:border-l-slate-500"
  );
}

export function getSlaBadgeClass(statusOrLabel: string) {
  return (
    slaBadgeClasses[normalizeSlaStyleKey(statusOrLabel)] ?? "bg-gray-100 text-gray-700"
  );
}

export function formatSlaSummary(ticket: SlaLabelInput) {
  const label = getSlaDisplayLabel(ticket);
  const minutes = ticket.slaRemainingMinutes;
  const duration = formatDuration(minutes);

  switch (label) {
    case "Resolved On Time":
      return minutes > 0
        ? `Resolved on time · ${duration} before deadline`
        : "Resolved on time · met SLA deadline";
    case "Resolved Late":
      return `Resolved late · ${duration} after deadline`;
    case "Overdue":
      return `Overdue · ${duration} past deadline`;
    case "At Risk":
      return `At risk · ${duration} until deadline`;
    case "On Track":
    default:
      return `On track · ${duration} until deadline`;
  }
}

/**
 * Single-sentence SLA tooltip. Combines state, remaining/elapsed duration, and the deadline or
 * completion timestamp — suitable for `CortexTooltip` content. Never returns multi-line text.
 */
export function buildSlaTooltip(ticket: SlaLabelInput) {
  const display = getSlaDisplayLabel(ticket);
  const duration = formatDuration(ticket.slaRemainingMinutes);
  const target = formatDateTime(ticket.slaTargetDate);
  const completed = ticket.slaCompletedDate
    ? formatDateTime(ticket.slaCompletedDate)
    : null;

  switch (display) {
    case "Resolved On Time":
      return completed
        ? `Resolved on time at ${completed} — deadline was ${target}.`
        : `Resolved on time — deadline was ${target}.`;
    case "Resolved Late":
      return completed
        ? `Resolved late at ${completed} — ${duration} past the ${target} deadline.`
        : `Resolved late — ${duration} past the ${target} deadline.`;
    case "Overdue":
      return `Overdue by ${duration} — deadline was ${target}.`;
    case "At Risk":
      return `At risk — ${duration} until the ${target} deadline.`;
    case "On Track":
    default:
      return `On track — ${duration} until the ${target} deadline.`;
  }
}

export type UrgencyChip = {
  label: string;
  chipClass: string;
  timingClass: string;
} | null;

/**
 * Returns urgency chip config for Overdue and At Risk tickets.
 * Returns null for healthy / resolved states so callers can render nothing.
 */
export function getUrgencyChip(ticket: SlaLabelInput): UrgencyChip {
  const label = getSlaDisplayLabel(ticket);
  if (label === "Overdue") {
    return {
      label: "Overdue",
      chipClass:
        "bg-red-100 text-red-800 border-red-300 dark:bg-red-900/40 dark:text-red-200 dark:border-red-800",
      timingClass: "text-red-600 dark:text-red-400",
    };
  }
  if (label === "At Risk") {
    return {
      label: "SLA Risk",
      chipClass:
        "bg-amber-100 text-amber-800 border-amber-300 dark:bg-amber-900/40 dark:text-amber-200 dark:border-amber-700",
      timingClass: "text-amber-600 dark:text-amber-400",
    };
  }
  return null;
}

function formatDuration(totalMinutes: number) {
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

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}
