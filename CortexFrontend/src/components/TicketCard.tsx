import type { Ticket } from "../types/ticket";
import {
  buildSlaTooltip,
  getSlaAccentClass,
  getSlaDisplayLabel,
} from "../utils/ticketSla";
import {
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";

interface TicketCardProps {
  ticket: Ticket;
  onClick?: () => void;
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
  return `${formatDuePhrase(ticket.slaTargetDate)} · ${cardTimingTail(ticket)}`;
}

export default function TicketCard({ ticket, onClick }: TicketCardProps) {
  const priorityColor =
    priorityColors[ticket.priority as keyof typeof priorityColors] ||
    "bg-gray-100 text-gray-800 border-gray-300";
  const statusColor =
    statusColors[ticket.status as keyof typeof statusColors] ||
    "bg-gray-100 text-gray-800";
  const slaAccentClass = getSlaAccentClass(getSlaDisplayLabel(ticket));
  const slaTooltip = buildSlaTooltip(ticket);
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
      className={`cursor-pointer rounded-xl border-l-4 border-t border-r border-b border-gray-100 bg-white p-4 shadow-sm outline-none transition-all duration-150 ease-in-out hover:-translate-y-[2px] hover:border-t-gray-300 hover:border-r-gray-300 hover:border-b-gray-300 hover:shadow-md focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 active:translate-y-0 active:shadow-sm dark:border-t-slate-700 dark:border-r-slate-700 dark:border-b-slate-700 dark:bg-slate-900 dark:hover:border-t-slate-600 dark:hover:border-r-slate-600 dark:hover:border-b-slate-600 dark:focus-visible:ring-offset-slate-900 ${slaAccentClass}`}
    >
      <div className="min-w-0 text-left">
        <h3
          className="line-clamp-2 text-left text-base font-semibold leading-snug tracking-tight text-gray-900 dark:text-slate-100"
          title={descriptionTitlePreview}
        >
          {ticket.title}
        </h3>
        <p className="mt-1 text-[11px] tabular-nums text-gray-400 dark:text-slate-500">
          #{formatTicketIdentifier(ticket.id)}
        </p>
      </div>

      <div className="mt-3 flex min-w-0 flex-wrap items-center gap-1.5">
        <span className={`${chipBaseClass} border-transparent ${statusColor}`}>
          {ticket.status}
        </span>
        <span className={`${chipBaseClass} ${priorityColor}`}>
          {ticket.priority}
        </span>
      </div>

      <p className="mt-3 text-left text-xs leading-snug text-gray-500 dark:text-slate-500">
        {formatDisplayValue(ticket.boardName)}
        {ticket.storyPoints !== undefined && ticket.storyPoints !== null
          ? ` · ${ticket.storyPoints} SP`
          : ""}
      </p>

      <p
        className="mt-3 line-clamp-2 text-left text-xs leading-snug text-gray-500 dark:text-slate-500"
        title={slaTooltip}
      >
        {mergedTimingLine(ticket)}
      </p>

      <div className="mt-3 space-y-1 border-t border-gray-100 pt-3 text-left text-[11px] leading-snug text-gray-600 dark:border-slate-800 dark:text-slate-400">
        <p
          className="truncate"
          title={formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}
        >
          <span className="text-gray-500 dark:text-slate-500">Syniti:</span>{" "}
          {formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}
        </p>
        <p
          className="truncate"
          title={formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}
        >
          <span className="text-gray-500 dark:text-slate-500">Business:</span>{" "}
          {formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}
        </p>
      </div>
    </div>
  );
}
