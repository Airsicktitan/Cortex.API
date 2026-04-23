import { useMemo } from "react";
import type { Ticket } from "../types/ticket";
import {
  ATTENTION_FILTER_LABELS,
  type AttentionFilterValue,
  buildExecutiveSummaryCounts,
  hasOwnershipGap,
  isBlockedOrWaiting,
  isAttentionFilterValue,
  isTicketOverdue,
  needsImmediateAttention,
} from "../utils/ticketAttention";
import { getActivitySignal, getWaitingOnLabel } from "../utils/ticketActivity";
import { formatDisplayValue, formatTicketIdentifier } from "../utils/presentation";
import { getSlaDisplayLabel } from "../utils/ticketSla";

type SummaryItem = {
  filterValue: AttentionFilterValue;
  title: string;
  reason: string;
  emptyText: string;
  tone: "critical" | "warning" | "neutral";
  getCount: (counts: ReturnType<typeof buildExecutiveSummaryCounts>) => number;
};

type AttentionBucket = {
  filterValue: AttentionFilterValue;
  title: string;
  reason: string;
  emptyText: string;
  tone: "critical" | "warning" | "neutral";
  getTickets: (tickets: Ticket[]) => Ticket[];
};

const SUMMARY_ITEMS: SummaryItem[] = [
  {
    filterValue: "overdue",
    title: "Overdue",
    reason: "past SLA target and still open",
    emptyText: "No open work is past target.",
    tone: "critical",
    getCount: (counts) => counts.overdue,
  },
  {
    filterValue: "sla-risk",
    title: "SLA Risk",
    reason: "approaching SLA target",
    emptyText: "No tickets are inside the warning window.",
    tone: "warning",
    getCount: (counts) => counts.slaRisk,
  },
  {
    filterValue: "stale",
    title: "Stale",
    reason: "no updates in 48h",
    emptyText: "All visible work has recent activity.",
    tone: "warning",
    getCount: (counts) => counts.stale,
  },
  {
    filterValue: "unassigned",
    title: "Unassigned",
    reason: "no Syniti Owner",
    emptyText: "Every visible ticket has a Syniti Owner.",
    tone: "neutral",
    getCount: (counts) => counts.unassigned,
  },
  {
    filterValue: "waiting-business",
    title: "Waiting on Business",
    reason: "progress depends on business response",
    emptyText: "No ticket is waiting on business response.",
    tone: "warning",
    getCount: (counts) => counts.waitingBusiness,
  },
  {
    filterValue: "waiting-reviewer",
    title: "Waiting on Reviewer",
    reason: "reviewer decision required",
    emptyText: "No ticket is waiting on reviewer decision.",
    tone: "warning",
    getCount: (counts) => counts.waitingReviewer,
  },
];

const ATTENTION_BUCKETS: AttentionBucket[] = [
  {
    filterValue: "immediate",
    title: "Needs Immediate Attention",
    reason: "work at risk of delay",
    emptyText: "No urgent delay signals in this board state.",
    tone: "critical",
    getTickets: (tickets) => tickets.filter(needsImmediateAttention),
  },
  {
    filterValue: "ownership-gaps",
    title: "Ownership Gaps",
    reason: "no one is currently responsible",
    emptyText: "Ownership is clear for visible work.",
    tone: "neutral",
    getTickets: (tickets) => tickets.filter(hasOwnershipGap),
  },
  {
    filterValue: "blocked-waiting",
    title: "Blocked / Waiting",
    reason: "progress depends on others",
    emptyText: "No visible work is blocked by another party.",
    tone: "warning",
    getTickets: (tickets) => tickets.filter(isBlockedOrWaiting),
  },
];

function getTicketTime(ticket: Ticket, field: "created" | "activity") {
  const value =
    field === "activity"
      ? ticket.lastModifiedDate ?? ticket.createdDate
      : ticket.createdDate;
  const parsed = new Date(value).getTime();
  return Number.isNaN(parsed) ? 0 : parsed;
}

function sortPreviewTickets(
  filterValue: AttentionFilterValue,
  tickets: Ticket[],
) {
  const copy = [...tickets];

  if (
    filterValue === "immediate" ||
    filterValue === "overdue" ||
    filterValue === "sla-risk"
  ) {
    return copy.sort((left, right) => {
      const overdueDelta =
        Number(isTicketOverdue(right)) - Number(isTicketOverdue(left));
      if (overdueDelta !== 0) return overdueDelta;

      const slaDelta = left.slaRemainingMinutes - right.slaRemainingMinutes;
      if (slaDelta !== 0) return slaDelta;

      return getTicketTime(left, "created") - getTicketTime(right, "created");
    });
  }

  if (filterValue === "stale") {
    return copy.sort((left, right) => {
      const leftActivity = getActivitySignal(left)?.minutesSince ?? 0;
      const rightActivity = getActivitySignal(right)?.minutesSince ?? 0;
      return rightActivity - leftActivity;
    });
  }

  if (filterValue === "blocked-waiting") {
    return copy.sort(
      (left, right) =>
        getTicketTime(left, "activity") - getTicketTime(right, "activity"),
    );
  }

  return copy.sort(
    (left, right) =>
      getTicketTime(left, "created") - getTicketTime(right, "created"),
  );
}

function getPreviewReason(ticket: Ticket) {
  const slaLabel = getSlaDisplayLabel(ticket);
  if (slaLabel === "Overdue") return "Overdue";
  if (slaLabel === "At Risk") return "SLA Risk";

  const activitySignal = getActivitySignal(ticket);
  if (activitySignal?.isStale) return `Stale ${activitySignal.label}`;

  const waitingOn = getWaitingOnLabel(ticket);
  if (waitingOn === "Waiting on Business Owner") return "Waiting on Business";
  if (waitingOn === "Waiting on Reviewer") return "Waiting on Reviewer";
  if (waitingOn === "Waiting on Assignment") return "Waiting on Assignment";

  return formatDisplayValue(ticket.status);
}

function getButtonClass(
  isActive: boolean,
  hasCount: boolean,
  tone: "critical" | "warning" | "neutral",
) {
  const base =
    "rounded-md border p-3 text-left transition-colors focus:outline-none focus:ring-2 focus:ring-cortex-blue/40";

  if (isActive) {
    return `${base} border-cortex-blue bg-blue-50 text-cortex-blue dark:border-blue-400 dark:bg-blue-950/40 dark:text-blue-200`;
  }

  if (!hasCount) {
    return `${base} cursor-not-allowed border-gray-200 bg-gray-50 text-gray-400 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-600`;
  }

  if (tone === "critical") {
    return `${base} border-red-200 bg-red-50/70 text-red-950 hover:border-red-300 hover:bg-red-50 dark:border-red-900/50 dark:bg-red-950/20 dark:text-red-100 dark:hover:border-red-700 dark:hover:bg-red-950/35`;
  }

  if (tone === "warning") {
    return `${base} border-amber-200 bg-amber-50/60 text-amber-950 hover:border-amber-300 hover:bg-amber-50 dark:border-amber-900/50 dark:bg-amber-950/20 dark:text-amber-100 dark:hover:border-amber-700 dark:hover:bg-amber-950/35`;
  }

  return `${base} border-gray-200 bg-white text-gray-900 hover:border-cortex-blue hover:bg-blue-50/70 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:hover:border-blue-400 dark:hover:bg-blue-950/30`;
}

interface ExecutiveAttentionPanelProps {
  tickets: Ticket[];
  activeFilterValue: string;
  onDrillDown: (filterValue: AttentionFilterValue) => void;
}

export default function ExecutiveAttentionPanel({
  tickets,
  activeFilterValue,
  onDrillDown,
}: ExecutiveAttentionPanelProps) {
  const counts = useMemo(() => buildExecutiveSummaryCounts(tickets), [tickets]);
  const buckets = useMemo(
    () =>
      ATTENTION_BUCKETS.map((bucket) => {
        const bucketTickets = bucket.getTickets(tickets);
        return {
          ...bucket,
          tickets: bucketTickets,
          preview: sortPreviewTickets(bucket.filterValue, bucketTickets).slice(0, 2),
        };
      }),
    [tickets],
  );
  const activeLabel = isAttentionFilterValue(activeFilterValue)
    ? ATTENTION_FILTER_LABELS[activeFilterValue]
    : null;
  const hasAnySignal =
    counts.overdue +
      counts.slaRisk +
      counts.stale +
      counts.unassigned +
      counts.waitingBusiness +
      counts.waitingReviewer >
    0;

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-2 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase text-cortex-blue dark:text-blue-300">
            Executive View
          </p>
          <h3 className="text-base font-semibold text-gray-900 dark:text-slate-100">
            Operational Health
          </h3>
        </div>
        <p className="text-sm text-gray-500 dark:text-slate-400">
          Risk, ownership gaps, and blocked work from the current operation.
        </p>
      </div>

      {activeLabel ? (
        <div className="mt-3 rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-900 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-100">
          Tickets view is filtered to: <span className="font-semibold">{activeLabel}</span>.
          Open the ticket queue to review details or take action.
        </div>
      ) : !hasAnySignal && tickets.length > 0 ? (
        <div className="mt-3 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900 dark:border-emerald-900/60 dark:bg-emerald-950/25 dark:text-emerald-100">
          No active risk signals in this board state. Counts update as SLA, ownership,
          and waiting-on signals change.
        </div>
      ) : tickets.length === 0 ? (
        <div className="mt-3 rounded-md border border-dashed border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-500 dark:border-slate-800 dark:bg-slate-950/30 dark:text-slate-400">
          No tickets are available for this board state yet.
        </div>
      ) : null}

      <div className="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-6">
        {SUMMARY_ITEMS.map((item) => {
          const count = item.getCount(counts);
          const isActive = activeFilterValue === item.filterValue;
          const hasCount = count > 0;

          return (
            <button
              key={item.filterValue}
              type="button"
              onClick={() => onDrillDown(item.filterValue)}
              disabled={!hasCount}
              className={getButtonClass(isActive, hasCount, item.tone)}
              aria-label={`${item.title}: ${count}`}
            >
              <span className="block text-xs font-medium text-gray-500 dark:text-slate-400">
                {item.title}
              </span>
              <span className="mt-2 block text-2xl font-semibold">{count}</span>
              <span className="mt-1 block text-xs text-gray-500 dark:text-slate-400">
                {hasCount ? item.reason : item.emptyText}
              </span>
            </button>
          );
        })}
      </div>

      <div className="mt-4 grid gap-3 lg:grid-cols-3">
        {buckets.map((bucket) => {
          const count = bucket.tickets.length;
          const isActive = activeFilterValue === bucket.filterValue;
          const hasCount = count > 0;

          return (
            <button
              key={bucket.filterValue}
              type="button"
              onClick={() => onDrillDown(bucket.filterValue)}
              disabled={!hasCount}
              className={getButtonClass(isActive, hasCount, bucket.tone)}
              aria-label={`${bucket.title}: ${count}`}
            >
              <span className="flex items-start justify-between gap-3">
                <span>
                  <span className="block text-sm font-semibold">
                    {bucket.title}
                  </span>
                  <span className="mt-1 block text-xs text-gray-500 dark:text-slate-400">
                    {bucket.reason}
                  </span>
                </span>
                <span className="rounded-full bg-gray-100 px-2 py-1 text-xs font-semibold text-gray-700 dark:bg-slate-800 dark:text-slate-200">
                  {count}
                </span>
              </span>

              <span className="mt-3 block space-y-2">
                {bucket.preview.length > 0 ? (
                  bucket.preview.map((ticket) => (
                    <span
                      key={ticket.id}
                      className="block rounded-md border border-gray-100 bg-gray-50 px-3 py-2 text-xs dark:border-slate-800 dark:bg-slate-950/50"
                    >
                      <span className="block font-medium text-gray-900 dark:text-slate-100">
                        {formatTicketIdentifier(ticket.id)}
                      </span>
                      <span className="mt-0.5 block truncate text-gray-500 dark:text-slate-400">
                        {formatDisplayValue(ticket.title)}
                      </span>
                      <span className="mt-1 block font-medium text-gray-600 dark:text-slate-300">
                        {getPreviewReason(ticket)}
                      </span>
                    </span>
                  ))
                ) : (
                  <span className="block rounded-md border border-dashed border-gray-200 px-3 py-2 text-xs text-gray-400 dark:border-slate-800 dark:text-slate-600">
                    {bucket.emptyText}
                  </span>
                )}
              </span>
            </button>
          );
        })}
      </div>
    </section>
  );
}
