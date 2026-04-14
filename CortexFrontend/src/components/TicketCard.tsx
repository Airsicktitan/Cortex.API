import type { Ticket } from "../types/ticket";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaAccentClass,
  getSlaBadgeClass,
  getSlaDisplayLabel,
} from "../utils/ticketSla";

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

export default function TicketCard({ ticket, onClick }: TicketCardProps) {
  const priorityColor =
    priorityColors[ticket.priority as keyof typeof priorityColors] ||
    "bg-gray-100 text-gray-800 border-gray-300";
  const statusColor =
    statusColors[ticket.status as keyof typeof statusColors] ||
    "bg-gray-100 text-gray-800";
  const slaDisplayLabel = getSlaDisplayLabel(ticket);
  const slaAccentClass = getSlaAccentClass(slaDisplayLabel);
  const slaBadgeClass = getSlaBadgeClass(slaDisplayLabel);
  const slaTooltip = buildSlaTooltip(ticket);
  const dueDateLabel = new Date(ticket.slaTargetDate).toLocaleString();
  const chipBaseClass =
    "inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium";
  const ownerTextClass = "truncate text-xs text-gray-600 dark:text-slate-400";

  return (
    <div
      onClick={onClick}
      title={slaTooltip}
      className={`cursor-pointer rounded-xl border-l-4 bg-white p-3.5 shadow-sm ring-1 ring-inset ring-gray-100 transition-shadow hover:shadow-md dark:bg-slate-900 dark:ring-slate-800 ${slaAccentClass}`}
    >
      <div className="flex items-start justify-between gap-2 sm:gap-3">
        <div className="min-w-0 flex-1">
          <h3 className="line-clamp-2 text-base font-semibold leading-5 text-gray-900 dark:text-slate-100">
            {ticket.title}
          </h3>
          <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">#{ticket.id}</p>
        </div>
        <div className="min-w-0 shrink-0 text-right">
          <p className="text-[11px] font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Due
          </p>
          <p className="truncate text-xs font-medium text-gray-700 dark:text-slate-300" title={dueDateLabel}>
            {dueDateLabel}
          </p>
        </div>
      </div>

      <div className="mt-3 flex min-w-0 flex-wrap items-center gap-1.5">
        <span className={`${chipBaseClass} border-transparent ${statusColor}`}>
          {ticket.status}
        </span>
        <span className={`${chipBaseClass} ${priorityColor}`}>{ticket.priority}</span>
        <span className={`${chipBaseClass} border-transparent bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100`}>
          {ticket.boardName}
        </span>
        {ticket.storyPoints ? (
          <span className={`${chipBaseClass} border-transparent bg-violet-100 text-violet-800 dark:bg-violet-950/30 dark:text-violet-200`}>
            {ticket.storyPoints} SP
          </span>
        ) : null}
        <span className={`${chipBaseClass} border-transparent ${slaBadgeClass}`} title={slaTooltip}>
          {slaDisplayLabel}
        </span>
      </div>

      <p className="mt-3 line-clamp-2 text-sm text-gray-600 dark:text-slate-300">
        {ticket.description}
      </p>

      <p className="mt-2 text-xs text-gray-500 dark:text-slate-400" title={slaTooltip}>
        {formatSlaSummary(ticket)}
      </p>

      <div className="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 border-t border-gray-100 pt-2.5 text-xs dark:border-slate-800">
        <div className="min-w-0">
          <p className="text-[11px] font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Syniti Owner
          </p>
          <p className={ownerTextClass}>{ticket.synitiOwner || "Unassigned"}</p>
        </div>
        <div className="min-w-0">
          <p className="text-[11px] font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Business Owner
          </p>
          <p className={ownerTextClass}>{ticket.businessOwner || "Unassigned"}</p>
        </div>
      </div>
    </div>
  );
}
