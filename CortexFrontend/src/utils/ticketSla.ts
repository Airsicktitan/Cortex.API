import type { Ticket } from "../types/ticket";

const slaAccentClasses: Record<string, string> = {
  "On Track": "border-l-green-500",
  "At Risk": "border-l-yellow-400",
  Breached: "border-l-red-500",
  Met: "border-l-green-500",
  "Resolved Late": "border-l-red-500",
};

const slaBadgeClasses: Record<string, string> = {
  "On Track": "bg-green-100 text-green-800",
  "At Risk": "bg-yellow-100 text-yellow-800",
  Breached: "bg-red-100 text-red-800",
  Met: "bg-emerald-100 text-emerald-800",
  "Resolved Late": "bg-rose-100 text-rose-800",
};

export function getSlaAccentClass(status: string) {
  return slaAccentClasses[status] ?? "border-l-gray-300";
}

export function getSlaBadgeClass(status: string) {
  return slaBadgeClasses[status] ?? "bg-gray-100 text-gray-700";
}

export function formatSlaSummary(ticket: Pick<Ticket, "slaRemainingMinutes" | "slaStatus">) {
  const duration = formatDuration(ticket.slaRemainingMinutes);

  switch (ticket.slaStatus) {
    case "Met":
      return `Resolved ${duration} early`;
    case "Resolved Late":
      return `Resolved ${duration} late`;
    case "Breached":
      return `${duration} overdue`;
    case "At Risk":
      return `${duration} remaining`;
    default:
      return `${duration} remaining`;
  }
}

export function buildSlaTooltip(ticket: Pick<
  Ticket,
  "slaStatus" | "slaTargetDate" | "slaCompletedDate" | "slaRemainingMinutes"
>) {
  const lines = [
    `SLA status: ${ticket.slaStatus}`,
    `Target: ${formatDateTime(ticket.slaTargetDate)}`,
    `Timing: ${formatSlaSummary(ticket)}`,
  ];

  if (ticket.slaCompletedDate) {
    lines.push(`Completed: ${formatDateTime(ticket.slaCompletedDate)}`);
  }

  return lines.join("\n");
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
