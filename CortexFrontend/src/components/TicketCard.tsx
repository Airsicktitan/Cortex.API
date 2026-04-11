import type { Ticket } from "../types/ticket";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaAccentClass,
  getSlaBadgeClass,
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
  const slaAccentClass = getSlaAccentClass(ticket.slaStatus);
  const slaBadgeClass = getSlaBadgeClass(ticket.slaStatus);
  const slaTooltip = buildSlaTooltip(ticket);

  return (
    <div
      onClick={onClick}
      title={slaTooltip}
      className={`bg-white dark:bg-slate-900 rounded-lg shadow-md hover:shadow-lg transition-shadow cursor-pointer border-l-4 ${slaAccentClass} p-4 ring-1 ring-inset ring-gray-100 dark:ring-slate-800`}
    >
      {/* Header */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100 mb-1">
            {ticket.title}
          </h3>
          <p className="text-sm text-gray-500 dark:text-slate-400">
            {typeof ticket.id === "string" && ticket.id.startsWith("TICKET-")
              ? ticket.id
              : `TICKET-${String(ticket.id).padStart(3, "0")}`}
          </p>
        </div>
        <span
          className={`px-3 py-1 rounded-full text-xs font-medium ${priorityColor} border`}
        >
          {ticket.priority}
        </span>
      </div>

      {/* Description */}
      <p className="text-gray-600 dark:text-slate-300 text-sm mb-4 line-clamp-2">
        {ticket.description}
      </p>

      <div className="flex items-center justify-between mb-4">
        <span
          className={`px-3 py-1 rounded-full text-xs font-medium ${statusColor}`}
        >
          {ticket.status}
        </span>

        <span
          className={`px-3 py-1 rounded-full text-xs font-medium ${slaBadgeClass}`}
          title={slaTooltip}
        >
          {ticket.slaStatus}
        </span>
      </div>

      <p
        className="text-xs text-gray-500 dark:text-slate-400 mb-4"
        title={slaTooltip}
      >
        {formatSlaSummary(ticket)}
      </p>

      {/* Footer */}
      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-500 dark:text-slate-400">
          Due {new Date(ticket.slaTargetDate).toLocaleString()}
        </span>

        <div className="flex flex-col items-end text-xs text-gray-500 dark:text-slate-400">
          {ticket.synitiOwner && <span>👤 {ticket.synitiOwner}</span>}
          {ticket.businessOwner && <span>🏢 {ticket.businessOwner}</span>}
        </div>
      </div>
    </div>
  );
}
