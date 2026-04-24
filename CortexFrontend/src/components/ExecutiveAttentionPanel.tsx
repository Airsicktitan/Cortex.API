import { useMemo, useRef } from "react";
import type { Ticket } from "../types/ticket";
import {
  ATTENTION_FILTER_LABELS,
  type AttentionFilterValue,
  buildExecutiveSummaryCounts,
  hasOwnershipGap,
  isBlockedOrWaiting,
  isAttentionFilterValue,
  isTicketOverdue,
  isTicketSlaRisk,
} from "../utils/ticketAttention";
import { getActivitySignal, getWaitingOnLabel } from "../utils/ticketActivity";
import { formatDisplayValue, formatTicketIdentifier } from "../utils/presentation";
import { getSlaDisplayLabel } from "../utils/ticketSla";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";
import { ScrollableViewport } from "./ui/ScrollableViewport";

type SummaryItem = {
  filterValue: AttentionFilterValue;
  title: string;
  reason: string;
  emptyText: string;
  tone: "critical" | "warning" | "neutral";
  getCount: (counts: ReturnType<typeof buildExecutiveSummaryCounts>) => number;
};

type ActionPanelTone = "critical" | "warning" | "neutral";

type AttentionBucket = {
  filterValue: AttentionFilterValue;
  title: string;
  subtitle: string;
  footerLabel: string;
  emptyText: string;
  tone: ActionPanelTone;
  getTickets: (tickets: Ticket[]) => Ticket[];
};

const SUMMARY_ITEMS: SummaryItem[] = [
  {
    filterValue: "overdue",
    title: "Overdue",
    reason: "Open tickets past SLA deadline.",
    emptyText: "No open tickets are past SLA deadline.",
    tone: "critical",
    getCount: (counts) => counts.overdue,
  },
  {
    filterValue: "sla-risk",
    title: "SLA Risk",
    reason: "Tickets approaching SLA breach.",
    emptyText: "No tickets are inside the warning window.",
    tone: "warning",
    getCount: (counts) => counts.slaRisk,
  },
  {
    filterValue: "stale",
    title: "Stale",
    reason: "No updates in the configured stale window.",
    emptyText: "All visible tickets have recent activity.",
    tone: "warning",
    getCount: (counts) => counts.stale,
  },
  {
    filterValue: "unassigned",
    title: "Unassigned",
    reason: "Active tickets without an owner.",
    emptyText: "Every active ticket has an owner.",
    tone: "neutral",
    getCount: (counts) => counts.unassigned,
  },
  {
    filterValue: "waiting-business",
    title: "Waiting on Business",
    reason: "Waiting on business response.",
    emptyText: "No tickets are waiting on business response.",
    tone: "warning",
    getCount: (counts) => counts.waitingBusiness,
  },
  {
    filterValue: "waiting-reviewer",
    title: "Waiting on Reviewer",
    reason: "Waiting on reviewer decision.",
    emptyText: "No tickets are waiting on reviewer decision.",
    tone: "warning",
    getCount: (counts) => counts.waitingReviewer,
  },
];

const ATTENTION_BUCKETS: AttentionBucket[] = [
  {
    filterValue: "ownership-gaps",
    title: "Ownership Gaps",
    subtitle: "Tickets that need a clear owner.",
    footerLabel: "Open ownership gap queue →",
    emptyText: "Every visible ticket has a clear owner.",
    tone: "neutral",
    getTickets: (tickets) => tickets.filter(hasOwnershipGap),
  },
  {
    filterValue: "blocked-waiting",
    title: "Blocked / Waiting",
    subtitle: "Tickets waiting on another response or decision.",
    footerLabel: "Open blocked / waiting queue →",
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

function sortActionTickets(
  filterValue: AttentionFilterValue,
  tickets: Ticket[],
) {
  const copy = [...tickets];

  if (filterValue === "overdue" || filterValue === "sla-risk") {
    return copy.sort((left, right) => {
      const overdueDelta =
        Number(isTicketOverdue(right)) - Number(isTicketOverdue(left));
      if (overdueDelta !== 0) return overdueDelta;

      const slaDelta = left.slaRemainingMinutes - right.slaRemainingMinutes;
      if (slaDelta !== 0) return slaDelta;

      return getTicketTime(left, "created") - getTicketTime(right, "created");
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

function getBucketTicketDetail(ticket: Ticket) {
  const slaLabel = getSlaDisplayLabel(ticket);
  if (slaLabel === "Overdue") return "Overdue";
  if (slaLabel === "At Risk") return "At Risk";

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
  tone: ActionPanelTone,
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

function getActionPanelClass(tone: ActionPanelTone, isActive: boolean) {
  const base =
    "flex min-h-0 flex-col overflow-hidden rounded-lg border p-4 shadow-sm lg:h-full";
  const active =
    isActive
      ? "border-cortex-blue ring-2 ring-cortex-blue/15 dark:border-blue-400 dark:ring-cortex-blue/20"
      : "";

  if (tone === "critical") {
    return `${base} border-red-200 bg-red-50/40 dark:border-red-900/40 dark:bg-red-950/10 ${active}`;
  }

  if (tone === "warning") {
    return `${base} border-amber-200 bg-amber-50/40 dark:border-amber-900/40 dark:bg-amber-950/10 ${active}`;
  }

  return `${base} border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900 ${active}`;
}

function getActionPanelBadgeClass(
  tone: ActionPanelTone,
  isActive: boolean,
  hasCount: boolean,
) {
  if (isActive) {
    return "bg-blue-100 text-blue-800 dark:bg-blue-900/50 dark:text-blue-200";
  }

  if (!hasCount) {
    return "bg-gray-100 text-gray-500 dark:bg-slate-800 dark:text-slate-400";
  }

  if (tone === "critical") {
    return "bg-red-100 text-red-800 dark:bg-red-900/50 dark:text-red-200";
  }

  if (tone === "warning") {
    return "bg-amber-100 text-amber-800 dark:bg-amber-900/50 dark:text-amber-200";
  }

  return "bg-gray-100 text-gray-700 dark:bg-slate-800 dark:text-slate-200";
}

function getActionPanelFooterClass(tone: ActionPanelTone, isActive: boolean) {
  const base =
    "text-xs font-medium transition-colors hover:underline focus:outline-none focus:ring-2 focus:ring-cortex-blue/40";

  if (isActive) {
    return `${base} text-cortex-blue dark:text-blue-300`;
  }

  if (tone === "critical") {
    return `${base} text-red-700 dark:text-red-400`;
  }

  if (tone === "warning") {
    return `${base} text-amber-700 dark:text-amber-400`;
  }

  return `${base} text-cortex-blue dark:text-blue-300`;
}

function fmtDuration(totalMinutes: number): string {
  const abs = Math.abs(totalMinutes);
  if (abs === 0) return "0m";
  if (abs < 60) return `${abs}m`;
  if (abs < 24 * 60) {
    const h = Math.round((abs / 60) * 10) / 10;
    return Number.isInteger(h) ? `${h}h` : `${h.toFixed(1)}h`;
  }
  const d = Math.round((abs / (24 * 60)) * 10) / 10;
  return Number.isInteger(d) ? `${d}d` : `${d.toFixed(1)}d`;
}

function getRiskOwnerLabel(ticket: Ticket): string {
  const syniti = readOnlySynitiOwnerLabel(ticket).trim();
  if (syniti && syniti !== "—") return syniti;
  const business = readOnlyBusinessOwnerLabel(ticket).trim();
  return business && business !== "—" ? business : "Unassigned";
}

function ActionTicketRow({
  ticket,
  detail,
  onOpenTicket,
}: {
  ticket: Ticket;
  detail: string;
  onOpenTicket: (ticket: Ticket) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onOpenTicket(ticket)}
      className="w-full rounded-md border border-gray-100 bg-gray-50 px-3 py-2.5 text-left text-xs transition-colors hover:border-gray-200 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-cortex-blue/40 dark:border-slate-800 dark:bg-slate-950/50 dark:hover:border-slate-700 dark:hover:bg-slate-900/80"
    >
      <p className="font-medium text-gray-900 dark:text-slate-100">
        {formatTicketIdentifier(ticket.id)}
      </p>
      <p className="mt-0.5 truncate text-gray-500 dark:text-slate-400">
        {formatDisplayValue(ticket.title)}
      </p>
      <p className="mt-1 font-medium text-gray-600 dark:text-slate-300">{detail}</p>
    </button>
  );
}

function ActionPanel({
  title,
  subtitle,
  emptyText,
  footerLabel,
  tone,
  isActive,
  tickets,
  onFooterClick,
  onOpenTicket,
  getDetail,
}: {
  title: string;
  subtitle: string;
  emptyText: string;
  footerLabel: string;
  tone: ActionPanelTone;
  isActive: boolean;
  tickets: Ticket[];
  onFooterClick: () => void;
  onOpenTicket: (ticket: Ticket) => void;
  getDetail: (ticket: Ticket) => string;
}) {
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const hasTickets = tickets.length > 0;

  return (
    <section className={getActionPanelClass(tone, isActive)}>
      <div className="flex items-start justify-between gap-3 border-b border-gray-200 pb-3 dark:border-slate-800">
        <div>
          <h4 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
            {title}
          </h4>
          <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
            {subtitle}
          </p>
        </div>
        <span
          className={`rounded-full px-2 py-0.5 text-xs font-semibold ${getActionPanelBadgeClass(tone, isActive, hasTickets)}`}
        >
          {tickets.length}
        </span>
      </div>

      <ScrollableViewport
        viewportRef={scrollRef}
        outerClassName="mt-3 flex-1"
        viewportClassName="scroll-chain-auto h-full overflow-y-auto pr-1"
        affordanceAriaLabel={`Scroll ${title.toLowerCase()} panel to bottom`}
      >
          {hasTickets ? (
            <div className="space-y-2 pb-1">
              {tickets.map((ticket) => (
                <ActionTicketRow
                  key={ticket.id}
                  ticket={ticket}
                  detail={getDetail(ticket)}
                  onOpenTicket={onOpenTicket}
                />
              ))}
            </div>
          ) : (
            <div className="flex h-full items-center justify-center px-2">
              <p className="rounded-md border border-dashed border-gray-200 px-3 py-4 text-center text-xs text-gray-400 dark:border-slate-800 dark:text-slate-600">
                {emptyText}
              </p>
            </div>
          )}
      </ScrollableViewport>

      {hasTickets ? (
        <div className="mt-3 border-t border-gray-200 pt-3 dark:border-slate-800">
          <button
            type="button"
            onClick={onFooterClick}
            className={getActionPanelFooterClass(tone, isActive)}
          >
            {footerLabel}
          </button>
        </div>
      ) : null}
    </section>
  );
}

function CriticalIssuesPanel({
  tickets,
  isActive,
  onDrillDown,
  onOpenTicket,
}: {
  tickets: Ticket[];
  isActive: boolean;
  onDrillDown: (filterValue: AttentionFilterValue) => void;
  onOpenTicket: (ticket: Ticket) => void;
}) {
  const overdueTickets = sortActionTickets(
    "overdue",
    tickets.filter(isTicketOverdue),
  );

  return (
    <ActionPanel
      title="Critical Issues"
      subtitle="Open tickets past SLA deadline."
      emptyText="No overdue work."
      footerLabel="Open overdue queue →"
      tone="critical"
      isActive={isActive}
      tickets={overdueTickets}
      onFooterClick={() => onDrillDown("overdue")}
      onOpenTicket={onOpenTicket}
      getDetail={(ticket) =>
        `${fmtDuration(ticket.slaRemainingMinutes)} overdue · Owner: ${getRiskOwnerLabel(ticket)}`
      }
    />
  );
}

function AtRiskPanel({
  tickets,
  isActive,
  onDrillDown,
  onOpenTicket,
}: {
  tickets: Ticket[];
  isActive: boolean;
  onDrillDown: (filterValue: AttentionFilterValue) => void;
  onOpenTicket: (ticket: Ticket) => void;
}) {
  const atRiskTickets = sortActionTickets(
    "sla-risk",
    tickets.filter(isTicketSlaRisk),
  );

  return (
    <ActionPanel
      title="At Risk"
      subtitle="Tickets approaching SLA breach."
      emptyText="No work is currently inside the warning window."
      footerLabel="Open at-risk queue →"
      tone="warning"
      isActive={isActive}
      tickets={atRiskTickets}
      onFooterClick={() => onDrillDown("sla-risk")}
      onOpenTicket={onOpenTicket}
      getDetail={(ticket) =>
        `Due in ${fmtDuration(ticket.slaRemainingMinutes)} · Owner: ${getRiskOwnerLabel(ticket)}`
      }
    />
  );
}

interface ExecutiveAttentionPanelProps {
  tickets: Ticket[];
  activeFilterValue: string;
  onDrillDown: (filterValue: AttentionFilterValue) => void;
  onOpenTicket: (ticket: Ticket) => void;
}

export default function ExecutiveAttentionPanel({
  tickets,
  activeFilterValue,
  onDrillDown,
  onOpenTicket,
}: ExecutiveAttentionPanelProps) {
  const counts = useMemo(() => buildExecutiveSummaryCounts(tickets), [tickets]);
  const buckets = useMemo(
    () =>
      ATTENTION_BUCKETS.map((bucket) => {
        const bucketTickets = bucket.getTickets(tickets);
        return {
          ...bucket,
          tickets: sortActionTickets(bucket.filterValue, bucketTickets),
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
    <section className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900 lg:flex lg:h-full lg:min-h-0 lg:flex-col lg:overflow-hidden">
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

      <div className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
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

      <div className="mt-4 grid gap-3 lg:min-h-0 lg:flex-1 lg:auto-rows-fr lg:grid-cols-2">
        <CriticalIssuesPanel
          tickets={tickets}
          isActive={activeFilterValue === "overdue"}
          onDrillDown={onDrillDown}
          onOpenTicket={onOpenTicket}
        />
        <AtRiskPanel
          tickets={tickets}
          isActive={activeFilterValue === "sla-risk"}
          onDrillDown={onDrillDown}
          onOpenTicket={onOpenTicket}
        />
        {buckets.map((bucket) => (
          <ActionPanel
            key={bucket.filterValue}
            title={bucket.title}
            subtitle={bucket.subtitle}
            emptyText={bucket.emptyText}
            footerLabel={bucket.footerLabel}
            tone={bucket.tone}
            isActive={activeFilterValue === bucket.filterValue}
            tickets={bucket.tickets}
            onFooterClick={() => onDrillDown(bucket.filterValue)}
            onOpenTicket={onOpenTicket}
            getDetail={getBucketTicketDetail}
          />
        ))}
      </div>
    </section>
  );
}
