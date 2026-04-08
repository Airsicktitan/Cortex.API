import type { Ticket } from "../types/ticket";
import { formatSlaSummary, getSlaBadgeClass } from "../utils/ticketSla";

interface DashboardPageProps {
  tickets: Ticket[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onOpenTicket: (ticket: Ticket) => void;
}

const CLOSED_STATUSES = new Set(["resolved", "closed"]);
const PRIORITY_ORDER = ["Critical", "High", "Medium", "Low"];

function normalize(value: string | undefined) {
  return value?.trim().toLowerCase() ?? "";
}

function isClosedTicket(ticket: Ticket) {
  return CLOSED_STATUSES.has(normalize(ticket.status));
}

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

function formatPercentage(count: number, total: number) {
  if (total === 0) {
    return "0%";
  }

  return `${Math.round((count / total) * 100)}%`;
}

function getOwnerLabel(ticket: Ticket) {
  const owner = ticket.synitiOwner || ticket.businessOwner;
  return owner?.trim() ? owner : "Unassigned";
}

function buildCounts(values: string[], preferredOrder?: string[]) {
  const counts = new Map<string, number>();

  values.forEach((value) => {
    const label = value?.trim() || "Unknown";
    counts.set(label, (counts.get(label) ?? 0) + 1);
  });

  const entries = [...counts.entries()].map(([label, count]) => ({ label, count }));

  if (preferredOrder?.length) {
    const order = new Map(preferredOrder.map((label, index) => [label, index]));
    return entries.sort((left, right) => {
      const leftOrder = order.get(left.label);
      const rightOrder = order.get(right.label);

      if (leftOrder !== undefined && rightOrder !== undefined) {
        return leftOrder - rightOrder;
      }

      if (leftOrder !== undefined) {
        return -1;
      }

      if (rightOrder !== undefined) {
        return 1;
      }

      return right.count - left.count;
    });
  }

  return entries.sort((left, right) => right.count - left.count);
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

function sortByMostRecent(tickets: Ticket[]) {
  return [...tickets].sort((leftTicket, rightTicket) => {
    const leftDate = new Date(leftTicket.lastModifiedDate ?? leftTicket.createdDate).getTime();
    const rightDate = new Date(
      rightTicket.lastModifiedDate ?? rightTicket.createdDate,
    ).getTime();

    return rightDate - leftDate;
  });
}

function SummaryCard({
  title,
  value,
  description,
  className,
}: {
  title: string;
  value: number;
  description: string;
  className: string;
}) {
  return (
    <div className={`rounded-lg border p-5 ${className}`}>
      <p className="text-sm font-medium">{title}</p>
      <p className="mt-3 text-3xl font-semibold">{value}</p>
      <p className="mt-2 text-sm opacity-80">{description}</p>
    </div>
  );
}

function DistributionCard({
  title,
  description,
  items,
  total,
}: {
  title: string;
  description: string;
  items: Array<{ label: string; count: number }>;
  total: number;
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

      <div className="space-y-4 px-6 py-5">
        {items.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-slate-400">
            No data available yet.
          </p>
        ) : (
          items.map((item) => (
            <div key={item.label} className="space-y-2">
              <div className="flex items-center justify-between gap-4 text-sm">
                <span className="font-medium text-gray-900 dark:text-slate-100">
                  {item.label}
                </span>
                <span className="text-gray-500 dark:text-slate-400">
                  {item.count} ({formatPercentage(item.count, total)})
                </span>
              </div>
              <div className="h-2 rounded-full bg-gray-100 dark:bg-slate-800">
                <div
                  className="h-2 rounded-full bg-cortex-blue"
                  style={{
                    width: total === 0 ? "0%" : `${(item.count / total) * 100}%`,
                  }}
                />
              </div>
            </div>
          ))
        )}
      </div>
    </section>
  );
}

function TicketTable({
  title,
  description,
  tickets,
  emptyMessage,
  rightColumnLabel,
  renderRightColumn,
  onOpenTicket,
}: {
  title: string;
  description: string;
  tickets: Ticket[];
  emptyMessage: string;
  rightColumnLabel: string;
  renderRightColumn: (ticket: Ticket) => string;
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
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 font-medium">Owner</th>
                <th className="px-4 py-3 font-medium">SLA</th>
                <th className="px-4 py-3 font-medium">{rightColumnLabel}</th>
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
                  <td className="px-4 py-3 align-top">{ticket.status}</td>
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
                    {renderRightColumn(ticket)}
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

export default function DashboardPage({
  tickets,
  loading,
  error,
  onRefresh,
  onOpenTicket,
}: DashboardPageProps) {
  const activeTickets = tickets.filter((ticket) => !isClosedTicket(ticket));
  const closedTickets = tickets.filter(isClosedTicket);
  const breachedTickets = activeTickets.filter((ticket) => ticket.slaStatus === "Breached");
  const atRiskTickets = activeTickets.filter((ticket) => ticket.slaStatus === "At Risk");
  const unassignedTickets = activeTickets.filter(
    (ticket) => getOwnerLabel(ticket) === "Unassigned",
  );

  const statusBreakdown = buildCounts(tickets.map((ticket) => ticket.status));
  const priorityBreakdown = buildCounts(
    activeTickets.map((ticket) => ticket.priority),
    PRIORITY_ORDER,
  );
  const ownerBreakdown = buildCounts(activeTickets.map(getOwnerLabel)).slice(0, 6);

  const attentionTickets = sortByUrgency(
    activeTickets.filter(
      (ticket) => ticket.slaStatus === "Breached" || ticket.slaStatus === "At Risk",
    ),
  ).slice(0, 8);
  const recentlyUpdatedTickets = sortByMostRecent(tickets).slice(0, 8);

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Dashboard
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              A quick view of queue health, ownership, and the tickets that need attention.
            </p>
          </div>

          <button
            onClick={onRefresh}
            className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-blue-700"
          >
            Refresh
          </button>
        </div>
      </section>

      {loading ? (
        <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center dark:border-slate-800 dark:bg-slate-900">
          <div className="mx-auto h-10 w-10 animate-spin rounded-full border-b-2 border-cortex-blue" />
          <p className="mt-4 text-gray-600 dark:text-slate-400">
            Loading dashboard...
          </p>
        </div>
      ) : error ? (
        <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      ) : tickets.length === 0 ? (
        <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
          No ticket data is available for the dashboard yet.
        </div>
      ) : (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <SummaryCard
              title="Visible Tickets"
              value={tickets.length}
              description="All tickets available to your current role."
              className="border-gray-200 bg-white text-gray-900 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100"
            />
            <SummaryCard
              title="Active Queue"
              value={activeTickets.length}
              description="Tickets still being worked."
              className="border-blue-200 bg-blue-50 text-blue-900 dark:border-blue-900/40 dark:bg-blue-950/20 dark:text-blue-100"
            />
            <SummaryCard
              title="Breached"
              value={breachedTickets.length}
              description="Open tickets already outside SLA."
              className="border-red-200 bg-red-50 text-red-900 dark:border-red-900/40 dark:bg-red-950/20 dark:text-red-100"
            />
            <SummaryCard
              title="At Risk"
              value={atRiskTickets.length}
              description="Open tickets inside the warning window."
              className="border-yellow-200 bg-yellow-50 text-yellow-900 dark:border-yellow-900/40 dark:bg-yellow-950/20 dark:text-yellow-100"
            />
            <SummaryCard
              title="Unassigned"
              value={unassignedTickets.length}
              description="Active tickets without an owner."
              className="border-gray-300 bg-gray-50 text-gray-900 dark:border-slate-700 dark:bg-slate-800/70 dark:text-slate-100"
            />
          </section>

          <div className="grid gap-6 xl:grid-cols-3">
            <DistributionCard
              title="Status Mix"
              description="Current ticket statuses across your visible set."
              items={statusBreakdown}
              total={tickets.length}
            />
            <DistributionCard
              title="Priority Mix"
              description="Priority distribution for the active queue."
              items={priorityBreakdown}
              total={activeTickets.length}
            />
            <DistributionCard
              title="Owner Workload"
              description="Top active-ticket ownership counts."
              items={ownerBreakdown}
              total={activeTickets.length}
            />
          </div>

          <div className="grid gap-6 xl:grid-cols-2">
            <TicketTable
              title="Needs Attention"
              description="Breached and at-risk tickets sorted by urgency."
              tickets={attentionTickets}
              emptyMessage="No active tickets currently need urgent SLA attention."
              rightColumnLabel="Due"
              renderRightColumn={(ticket) => formatDateTime(ticket.slaTargetDate)}
              onOpenTicket={onOpenTicket}
            />
            <TicketTable
              title="Recently Updated"
              description="The most recently changed tickets in your visible set."
              tickets={recentlyUpdatedTickets}
              emptyMessage="No recently updated tickets found."
              rightColumnLabel="Updated"
              renderRightColumn={(ticket) =>
                formatDateTime(ticket.lastModifiedDate ?? ticket.createdDate)
              }
              onOpenTicket={onOpenTicket}
            />
          </div>

          <section className="grid gap-4 md:grid-cols-3">
            <div className="rounded-lg border border-green-200 bg-green-50 p-5 dark:border-green-900/40 dark:bg-green-950/20">
              <p className="text-sm font-medium text-green-700 dark:text-green-300">
                Closed in SLA
              </p>
              <p className="mt-3 text-3xl font-semibold text-green-900 dark:text-green-100">
                {tickets.filter((ticket) => ticket.slaStatus === "Met").length}
              </p>
              <p className="mt-2 text-sm text-green-700/80 dark:text-green-300/80">
                Resolved or closed within the target window.
              </p>
            </div>

            <div className="rounded-lg border border-orange-200 bg-orange-50 p-5 dark:border-orange-900/40 dark:bg-orange-950/20">
              <p className="text-sm font-medium text-orange-700 dark:text-orange-300">
                Resolved Late
              </p>
              <p className="mt-3 text-3xl font-semibold text-orange-900 dark:text-orange-100">
                {tickets.filter((ticket) => ticket.slaStatus === "Resolved Late").length}
              </p>
              <p className="mt-2 text-sm text-orange-700/80 dark:text-orange-300/80">
                Completed after the SLA target was missed.
              </p>
            </div>

            <div className="rounded-lg border border-slate-300 bg-slate-50 p-5 dark:border-slate-700 dark:bg-slate-900/80">
              <p className="text-sm font-medium text-slate-700 dark:text-slate-300">
                Closed / Resolved
              </p>
              <p className="mt-3 text-3xl font-semibold text-slate-900 dark:text-slate-100">
                {closedTickets.length}
              </p>
              <p className="mt-2 text-sm text-slate-700/80 dark:text-slate-300/80">
                Tickets no longer in the active working queue.
              </p>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
