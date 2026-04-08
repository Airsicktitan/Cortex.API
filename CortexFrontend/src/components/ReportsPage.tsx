import type { Ticket } from "../types/ticket";
import SlaLegend from "./SlaLegend";
import { formatSlaSummary, getSlaBadgeClass } from "../utils/ticketSla";
import { downloadSlaReportWorkbook } from "../utils/reportExcel";

interface ReportsPageProps {
  tickets: Ticket[];
  showSlaLegend: boolean;
  onToggleSlaLegend: () => void;
  onRefresh: () => void;
  onOpenTicket: (ticket: Ticket) => void;
}

const STATUS_ORDER = [
  "On Track",
  "At Risk",
  "Breached",
  "Met",
  "Resolved Late",
] as const;

const STATUS_DESCRIPTIONS: Record<(typeof STATUS_ORDER)[number], string> = {
  "On Track": "Open tickets comfortably inside their SLA window.",
  "At Risk": "Open tickets inside the warning window before breach.",
  Breached: "Open tickets that are already past their SLA target.",
  Met: "Resolved or closed tickets completed within SLA.",
  "Resolved Late": "Resolved or closed tickets completed after the SLA target.",
};

function formatPercentage(count: number, total: number) {
  if (total === 0) {
    return "0%";
  }

  return `${Math.round((count / total) * 100)}%`;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function getOwnerLabel(ticket: Ticket) {
  return ticket.synitiOwner || ticket.businessOwner || "Unassigned";
}

function sortByUrgency(tickets: Ticket[]) {
  return [...tickets].sort((leftTicket, rightTicket) => {
    if (leftTicket.slaStatus === "Breached" && rightTicket.slaStatus !== "Breached") {
      return -1;
    }

    if (leftTicket.slaStatus !== "Breached" && rightTicket.slaStatus === "Breached") {
      return 1;
    }

    return leftTicket.slaRemainingMinutes - rightTicket.slaRemainingMinutes;
  });
}

function RiskTable({
  title,
  description,
  tickets,
  emptyMessage,
  onOpenTicket,
}: {
  title: string;
  description: string;
  tickets: Ticket[];
  emptyMessage: string;
  onOpenTicket: (ticket: Ticket) => void;
}) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
          {title}
        </h3>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          {description}
        </p>
      </div>

      {tickets.length === 0 ? (
        <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
          {emptyMessage}
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
              <tr>
                <th className="px-4 py-3 font-medium">Ticket</th>
                <th className="px-4 py-3 font-medium">Priority</th>
                <th className="px-4 py-3 font-medium">Owner</th>
                <th className="px-4 py-3 font-medium">SLA</th>
                <th className="px-4 py-3 font-medium">Due</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((ticket) => (
                <tr
                  key={ticket.id}
                  className="cursor-pointer border-t border-gray-100 text-gray-700 transition-colors hover:bg-gray-50 dark:border-slate-800 dark:text-slate-200 dark:hover:bg-slate-800/50"
                  onClick={() => onOpenTicket(ticket)}
                >
                  <td className="px-4 py-3 align-top">
                    <div>
                      <p className="font-medium text-gray-900 dark:text-slate-100">
                        {ticket.id}
                      </p>
                      <p className="max-w-xs truncate text-gray-500 dark:text-slate-400">
                        {ticket.title}
                      </p>
                    </div>
                  </td>
                  <td className="px-4 py-3 align-top">{ticket.priority}</td>
                  <td className="px-4 py-3 align-top">{getOwnerLabel(ticket)}</td>
                  <td className="px-4 py-3 align-top">
                    <div className="flex flex-col gap-1">
                      <span
                        className={`inline-flex w-fit rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(ticket.slaStatus)}`}
                      >
                        {ticket.slaStatus}
                      </span>
                      <span className="text-xs text-gray-500 dark:text-slate-400">
                        {formatSlaSummary(ticket)}
                      </span>
                    </div>
                  </td>
                  <td className="px-4 py-3 align-top whitespace-nowrap">
                    {formatDateTime(ticket.slaTargetDate)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default function ReportsPage({
  tickets,
  showSlaLegend,
  onToggleSlaLegend,
  onRefresh,
  onOpenTicket,
}: ReportsPageProps) {
  const totalTickets = tickets.length;
  const statusCounts = Object.fromEntries(
    STATUS_ORDER.map((status) => [
      status,
      tickets.filter((ticket) => ticket.slaStatus === status).length,
    ]),
  ) as Record<(typeof STATUS_ORDER)[number], number>;

  const inSlaCount = statusCounts["On Track"] + statusCounts.Met;
  const atRiskCount = statusCounts["At Risk"];
  const outsideSlaCount = statusCounts.Breached + statusCounts["Resolved Late"];

  const actionableTickets = sortByUrgency(
    tickets.filter(
      (ticket) => ticket.slaStatus === "At Risk" || ticket.slaStatus === "Breached",
    ),
  );
  const resolvedLateTickets = sortByUrgency(
    tickets.filter((ticket) => ticket.slaStatus === "Resolved Late"),
  );
  const exportReport = () => {
    downloadSlaReportWorkbook({
      tickets,
      statusOrder: STATUS_ORDER,
      statusCounts,
      statusDescriptions: STATUS_DESCRIPTIONS,
      actionableTickets,
      resolvedLateTickets,
    });
  };

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Reports
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              SLA reporting for the tickets you can currently access.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              onClick={exportReport}
              className="rounded-md bg-emerald-600 px-4 py-2 text-white transition-colors hover:bg-emerald-700"
            >
              Export Excel
            </button>
            <button
              onClick={onToggleSlaLegend}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              {showSlaLegend ? "Hide SLA Legend" : "Show SLA Legend"}
            </button>
            <button
              onClick={onRefresh}
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-blue-700"
            >
              Refresh
            </button>
          </div>
        </div>
      </section>

      {showSlaLegend && <SlaLegend />}

      {totalTickets === 0 ? (
        <section className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
          No ticket data is available for reporting yet.
        </section>
      ) : (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
              <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
                Total Tickets
              </p>
              <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
                {totalTickets}
              </p>
              <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                Visible to your current role and permissions.
              </p>
            </div>

            <div className="rounded-lg border border-green-200 bg-green-50 p-5 dark:border-green-900/40 dark:bg-green-950/20">
              <p className="text-sm font-medium text-green-700 dark:text-green-300">
                In SLA
              </p>
              <p className="mt-3 text-3xl font-semibold text-green-900 dark:text-green-100">
                {inSlaCount}
              </p>
              <p className="mt-2 text-sm text-green-700/80 dark:text-green-300/80">
                On track or resolved within SLA.
              </p>
            </div>

            <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-5 dark:border-yellow-900/40 dark:bg-yellow-950/20">
              <p className="text-sm font-medium text-yellow-800 dark:text-yellow-300">
                At Risk
              </p>
              <p className="mt-3 text-3xl font-semibold text-yellow-900 dark:text-yellow-100">
                {atRiskCount}
              </p>
              <p className="mt-2 text-sm text-yellow-800/80 dark:text-yellow-300/80">
                Tickets inside the warning window.
              </p>
            </div>

            <div className="rounded-lg border border-red-200 bg-red-50 p-5 dark:border-red-900/40 dark:bg-red-950/20">
              <p className="text-sm font-medium text-red-700 dark:text-red-300">
                Outside SLA
              </p>
              <p className="mt-3 text-3xl font-semibold text-red-900 dark:text-red-100">
                {outsideSlaCount}
              </p>
              <p className="mt-2 text-sm text-red-700/80 dark:text-red-300/80">
                Breached or resolved after the SLA target.
              </p>
            </div>
          </section>

          <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
            <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                SLA Status Breakdown
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Exact SLA outcomes across the current ticket set.
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                  <tr>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Count</th>
                    <th className="px-4 py-3 font-medium">Share</th>
                    <th className="px-4 py-3 font-medium">Meaning</th>
                  </tr>
                </thead>
                <tbody>
                  {STATUS_ORDER.map((status) => (
                    <tr
                      key={status}
                      className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                    >
                      <td className="px-4 py-3 align-top">
                        <span
                          className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(status)}`}
                        >
                          {status}
                        </span>
                      </td>
                      <td className="px-4 py-3 align-top font-medium text-gray-900 dark:text-slate-100">
                        {statusCounts[status]}
                      </td>
                      <td className="px-4 py-3 align-top">
                        {formatPercentage(statusCounts[status], totalTickets)}
                      </td>
                      <td className="px-4 py-3 align-top text-gray-500 dark:text-slate-400">
                        {STATUS_DESCRIPTIONS[status]}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <div className="grid gap-6 xl:grid-cols-2">
            <RiskTable
              title="Attention Needed"
              description="Open tickets that are nearing or already past their SLA target."
              tickets={actionableTickets}
              emptyMessage="No tickets currently need SLA attention."
              onOpenTicket={onOpenTicket}
            />

            <RiskTable
              title="Resolved Late"
              description="Tickets that were completed after their SLA target."
              tickets={resolvedLateTickets}
              emptyMessage="No tickets have been resolved late."
              onOpenTicket={onOpenTicket}
            />
          </div>
        </>
      )}
    </div>
  );
}
