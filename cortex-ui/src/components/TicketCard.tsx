import type { Ticket } from "../types/ticket";

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
  New: "bg-blue-100 text-blue-800",
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

  return (
    <div
      onClick={onClick}
      className="bg-white rounded-lg shadow-md hover:shadow-lg transition-shadow cursor-pointer border-l-4 border-cortex-blue p-4"
    >
      {/* Header */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1">
          <h3 className="text-lg font-semibold text-gray-900 mb-1">
            {ticket.title}
          </h3>
          <p className="text-sm text-gray-500">{ticket.id}</p>
        </div>
        <span
          className={`px-3 py-1 rounded-full text-xs font-medium ${priorityColor} border`}
        >
          {ticket.priority}
        </span>
      </div>

      {/* Description */}
      <p className="text-gray-600 text-sm mb-4 line-clamp-2">
        {ticket.description}
      </p>

      {/* Footer */}
      <div className="flex items-center justify-between">
        <span
          className={`px-3 py-1 rounded-full text-xs font-medium ${statusColor}`}
        >
          {ticket.status}
        </span>

        <div className="flex flex-col items-end text-xs text-gray-500">
          {ticket.synitiOwner && <span>👤 {ticket.synitiOwner}</span>}
          {ticket.businessOwner && <span>🏢 {ticket.businessOwner}</span>}
        </div>
      </div>
    </div>
  );
}
