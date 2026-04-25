import { useRef, useState } from "react";
import type { Ticket } from "../types/ticket";
import type { AttentionFilterValue } from "../utils/ticketAttention";
import { DashboardSkeleton } from "./LoadingSkeletons";
import ExecutiveAttentionPanel from "./ExecutiveAttentionPanel";
import { ScrollableViewport } from "./ui/ScrollableViewport";
import { formatSlaSummary, getSlaBadgeClass, getSlaDisplayLabel } from "../utils/ticketSla";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";

interface DashboardPageProps {
  tickets: Ticket[];
  loading: boolean;
  error: string | null;
  activeAttentionFilterValue: string;
  onRefresh: () => void;
  onOpenTicket: (ticket: Ticket) => void;
  onAttentionDrillDown: (filterValue: AttentionFilterValue) => void;
}

const CLOSED_STATUSES = new Set(["resolved", "closed"]);
const PRIORITY_ORDER = ["Critical", "High", "Medium", "Low"];
const IMPACT_LOOKBACK_MS = 7 * 24 * 60 * 60 * 1000;

function normalize(value: string | undefined) {
  return value?.trim().toLowerCase() ?? "";
}

function isClosedTicket(ticket: Ticket) {
  return CLOSED_STATUSES.has(normalize(ticket.status));
}

function formatPercentage(count: number, total: number) {
  if (total === 0) {
    return "0%";
  }

  return `${Math.round((count / total) * 100)}%`;
}

function getOwnerLabel(ticket: Ticket) {
  const synitiOwner = formatDisplayValue(readOnlySynitiOwnerLabel(ticket));
  if (synitiOwner !== "—") {
    return synitiOwner;
  }

  return formatDisplayValue(readOnlyBusinessOwnerLabel(ticket));
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

function isWithinImpactWindow(value?: string) {
  if (!value) {
    return false;
  }

  const timestamp = new Date(value).getTime();
  if (Number.isNaN(timestamp)) {
    return false;
  }

  return Date.now() - timestamp <= IMPACT_LOOKBACK_MS;
}

function formatEstimatedHours(value: number) {
  if (value <= 0) {
    return "0h";
  }

  return `${Number.isInteger(value) ? value : value.toFixed(1)}h`;
}

function buildImpactMetrics(tickets: Ticket[]) {
  const impactsThisWeek = tickets
    .map((ticket) => ticket.decisionImpact)
    .filter(
      (impact) =>
        impact?.hasImpact && isWithinImpactWindow(impact.appliedAtUtc),
    );
  const workloadOptimizations = impactsThisWeek.filter(
    (impact) => impact?.workloadImproved || impact?.pressureImproved,
  ).length;
  const risksReduced = impactsThisWeek.filter(
    (impact) => impact?.riskImproved,
  ).length;
  const intakePreviewTickets = tickets.filter((ticket) => ticket.approvalTriagePreview);
  const intakeReady = intakePreviewTickets.filter(
    (ticket) =>
      (ticket.approvalTriagePreview?.missingDetailHints?.length ?? 0) === 0,
  ).length;
  const estimatedHours =
    Math.round(
      (workloadOptimizations * 0.75 + risksReduced * 1.5 + intakeReady * 0.25) *
        10,
    ) / 10;

  return {
    rebalanceActions: workloadOptimizations,
    risksReduced,
    estimatedHours,
    intakeQuality:
      intakePreviewTickets.length > 0
        ? formatPercentage(intakeReady, intakePreviewTickets.length)
        : "Building",
    hasSignal:
      impactsThisWeek.length > 0 ||
      intakeReady > 0 ||
      intakePreviewTickets.length > 0,
  };
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
            No operational signal yet. Create or approve tickets to see queue
            health.
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
                        {formatTicketIdentifier(ticket.id)}
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
                        className={`inline-flex w-fit rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(getSlaDisplayLabel(ticket))}`}
                      >
                        {getSlaDisplayLabel(ticket)}
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

function CortexImpactCard({ tickets }: { tickets: Ticket[] }) {
  const metrics = buildImpactMetrics(tickets);
  const proofPoints = [
    {
      label: "Rebalance Actions",
      value: metrics.rebalanceActions.toString(),
      detail: "Workload optimization moves with improved pressure signals.",
    },
    {
      label: "SLA Risks Reduced",
      value: metrics.risksReduced.toString(),
      detail: "Approved decisions that lowered risk this week.",
    },
    {
      label: "Estimated Follow-up Hours Saved",
      value: formatEstimatedHours(metrics.estimatedHours),
      detail: "Based on avoided follow-up cycles and workload optimization signals.",
    },
    {
      label: "Intake Quality",
      value: metrics.intakeQuality,
      detail: "Share of AI-reviewed intake with no missing detail hints.",
    },
  ];

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
            Cortex Impact This Week
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Operational proof points from approved, improved, and optimized work.
          </p>
        </div>
        <span className="w-fit rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-100">
          Estimated impact
        </span>
      </div>

      {metrics.hasSignal ? (
        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {proofPoints.map((point) => (
            <div
              key={point.label}
              className="rounded-md border border-gray-100 bg-gray-50 px-4 py-3 dark:border-slate-800 dark:bg-slate-950/40"
            >
              <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                {point.label}
              </p>
              <p className="mt-2 text-2xl font-semibold text-gray-950 dark:text-white">
                {point.value}
              </p>
              <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                {point.detail}
              </p>
            </div>
          ))}
        </div>
      ) : (
        <p className="mt-5 rounded-md border border-dashed border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-600 dark:border-slate-700 dark:bg-slate-950/30 dark:text-slate-300">
          Cortex Impact will appear after tickets are approved, improved, or optimized.
        </p>
      )}
    </section>
  );
}

type DashboardTab = "overview" | "analytics" | "activity";

const TABS: { id: DashboardTab; label: string }[] = [
  { id: "overview", label: "Overview" },
  { id: "analytics", label: "Analytics" },
  { id: "activity", label: "Activity" },
];

export default function DashboardPage({
  tickets,
  loading,
  error,
  activeAttentionFilterValue,
  onRefresh,
  onOpenTicket,
  onAttentionDrillDown,
}: DashboardPageProps) {
  const [activeTab, setActiveTab] = useState<DashboardTab>("overview");
  const tabScrollRef = useRef<HTMLDivElement | null>(null);

  if (loading) {
    return <DashboardSkeleton />;
  }

  const activeTickets = tickets.filter((ticket) => !isClosedTicket(ticket));
  const closedTickets = tickets.filter(isClosedTicket);

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
    <div className="flex min-h-0 flex-col gap-6 lg:h-full lg:overflow-hidden">
      <section className="shrink-0 rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Dashboard
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Operational health, ownership gaps, and the work that needs management attention.
            </p>
          </div>

          <button
            onClick={onRefresh}
            className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
          >
            Refresh
          </button>
        </div>

        <div className="mt-5 flex gap-1 border-t border-gray-100 pt-4 dark:border-slate-800">
          {TABS.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`rounded-md px-4 py-1.5 text-sm font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-cortex-blue/40 ${
                  isActive
                    ? "bg-cortex-blue text-white"
                    : "text-gray-500 hover:bg-gray-100 hover:text-gray-800 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-100"
                }`}
              >
                {tab.label}
              </button>
            );
          })}
        </div>
      </section>

      <div className="min-h-0 flex-1 lg:overflow-hidden">
        {error ? (
          <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{error}</p>
          </div>
        ) : tickets.length === 0 ? (
          <div className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
            No dashboard signal yet. Create or approve a ticket to start showing
            risk, ownership, and SLA health.
          </div>
        ) : activeTab === "overview" ? (
          <ScrollableViewport
            viewportRef={tabScrollRef}
            outerClassName="lg:h-full"
            viewportClassName="scroll-chain-auto space-y-6 lg:h-full lg:min-h-0 lg:overflow-y-auto lg:pr-1"
            affordanceAriaLabel="Scroll dashboard overview to bottom"
          >
            <CortexImpactCard tickets={tickets} />
            <ExecutiveAttentionPanel
              tickets={tickets}
              activeFilterValue={activeAttentionFilterValue}
              onDrillDown={onAttentionDrillDown}
              onOpenTicket={onOpenTicket}
            />
          </ScrollableViewport>
        ) : activeTab === "analytics" ? (
          <ScrollableViewport
            viewportRef={tabScrollRef}
            outerClassName="lg:h-full"
            viewportClassName="scroll-chain-auto space-y-6 lg:h-full lg:min-h-0 lg:overflow-y-auto lg:pr-1"
            affordanceAriaLabel="Scroll dashboard analytics to bottom"
          >
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
          </ScrollableViewport>
        ) : (
          <ScrollableViewport
            viewportRef={tabScrollRef}
            outerClassName="lg:h-full"
            viewportClassName="scroll-chain-auto lg:h-full lg:min-h-0 lg:overflow-y-auto lg:pr-1"
            affordanceAriaLabel="Scroll dashboard activity to bottom"
          >
            <div className="grid gap-6 xl:grid-cols-2">
              <TicketTable
                title="Needs Attention"
                description="Overdue and at-risk tickets sorted by urgency."
                tickets={attentionTickets}
                emptyMessage="No active tickets currently need urgent SLA attention."
                rightColumnLabel="Due"
                renderRightColumn={(ticket) => formatDisplayDateTime(ticket.slaTargetDate)}
                onOpenTicket={onOpenTicket}
              />
              <TicketTable
                title="Recently Updated"
                description="The most recently changed tickets in your visible set."
                tickets={recentlyUpdatedTickets}
                emptyMessage="No recently updated tickets found."
                rightColumnLabel="Updated"
                renderRightColumn={(ticket) =>
                  formatDisplayDateTime(ticket.lastModifiedDate ?? ticket.createdDate)
                }
                onOpenTicket={onOpenTicket}
              />
            </div>
          </ScrollableViewport>
        )}
      </div>
    </div>
  );
}
