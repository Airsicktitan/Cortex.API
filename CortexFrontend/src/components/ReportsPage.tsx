import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useRef, useState, type ReactNode } from "react";
import type { Ticket } from "../types/ticket";
import type {
  CustomReportDefinition,
  CustomReportResult,
} from "../types/customReport";
import type { OnlineUser } from "../types/user";
import { ReportsSkeleton } from "./LoadingSkeletons";
import SlaLegend from "./SlaLegend";
import {
  formatSlaSummary,
  getSlaBadgeClass,
  getSlaDisplayLabel,
  mapBackendSlaStatusToDisplayLabel,
} from "../utils/ticketSla";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  humanizeEnumLabel,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";
import { isOpenTicket } from "../utils/ticketLifecycle";
import type { WorkflowMetricsSnapshot } from "../types/workflowMetrics";
import { metricsService } from "../services/api";

const API_AUDIENCE = "https://cortex-api";

type ReportSection = "sla" | "online-users" | "custom";

function formatWorkflowAvg(value: number): string {
  if (!Number.isFinite(value)) {
    return "0.0";
  }
  return value.toFixed(1);
}

function formatWorkflowCount(value: number): string {
  if (!Number.isFinite(value)) {
    return "0";
  }
  return String(Math.round(value));
}

/** When follow-up averages are comparable and Ready &lt; Needs detail, surface a subtle insight. */
function getWorkflowFollowUpInsight(
  m: WorkflowMetricsSnapshot,
): string | null {
  const readyAvg = m.avgCommentCountBySignal.ready;
  const needsAvg = m.avgCommentCountBySignal.needs_detail;
  if (!Number.isFinite(readyAvg) || !Number.isFinite(needsAvg)) {
    return null;
  }
  if (needsAvg <= 0 || readyAvg >= needsAvg) {
    return null;
  }
  return "So far, tickets labeled Ready for review show lower average follow-up comments than those labeled Needs detail first.";
}

function WorkflowSectionBlock({
  title,
  children,
  intro,
}: {
  title: string;
  children: ReactNode;
  /** Muted helper shown under the section title (e.g. Follow-Up Proxy). */
  intro?: ReactNode;
}) {
  return (
    <div className="rounded-lg bg-slate-50/90 px-3 py-2.5 ring-1 ring-gray-100/80 dark:bg-slate-800/35 dark:ring-slate-700/50">
      <p className="mb-1.5 text-[11px] font-bold uppercase tracking-wider text-gray-600 dark:text-slate-300">
        {title}
      </p>
      {intro}
      <div className="space-y-1.5">{children}</div>
    </div>
  );
}

function WorkflowMetricRow({
  label,
  value,
  valueIsAverage = false,
  labelEmphasis = "default",
}: {
  label: string;
  value: string;
  valueIsAverage?: boolean;
  /** Stronger label copy for reviewer readiness (still neutral). */
  labelEmphasis?: "default" | "readiness";
}) {
  const labelClass =
    labelEmphasis === "readiness"
      ? "text-sm font-medium text-gray-700 dark:text-slate-300"
      : "text-xs text-gray-500 dark:text-slate-400";

  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-gray-100/80 pb-1.5 last:border-b-0 last:pb-0 dark:border-slate-700/40">
      <span className={`min-w-0 leading-snug ${labelClass}`}>{label}</span>
      <span
        className={`shrink-0 text-right text-base font-semibold tabular-nums tracking-tight text-gray-900 dark:text-slate-50 ${
          valueIsAverage ? "min-w-[3.25rem]" : "min-w-[2.5rem]"
        }`}
      >
        {value}
      </span>
    </div>
  );
}

function ReviewerReadinessDistributionBar({
  ready,
  gaps,
  needsDetail,
}: {
  ready: number;
  gaps: number;
  needsDetail: number;
}) {
  const r = Math.max(0, Math.round(ready));
  const g = Math.max(0, Math.round(gaps));
  const n = Math.max(0, Math.round(needsDetail));
  const total = r + g + n;
  const pct = (x: number) => (total > 0 ? (x / total) * 100 : 0);

  return (
    <div
      className="mb-2.5"
      role="img"
      aria-label="Reviewer readiness distribution: Ready, Small gaps, and Needs detail shares of total signals."
    >
      <div className="flex h-2 w-full overflow-hidden rounded-full bg-gray-200/90 dark:bg-slate-700/90">
        {total > 0 ? (
          <>
            <div
              style={{ width: `${pct(r)}%` }}
              className="min-w-0 shrink-0 bg-emerald-500/75 dark:bg-emerald-500/55"
            />
            <div
              style={{ width: `${pct(g)}%` }}
              className="min-w-0 shrink-0 bg-amber-400/85 dark:bg-amber-500/50"
            />
            <div
              style={{ width: `${pct(n)}%` }}
              className="min-w-0 shrink-0 bg-rose-500/70 dark:bg-rose-500/48"
            />
          </>
        ) : (
          <div className="h-full w-full bg-gray-300/60 dark:bg-slate-600/60" />
        )}
      </div>
    </div>
  );
}

function WorkflowFollowUpRow({
  label,
  value,
  valueNum,
  maxInSection,
}: {
  label: string;
  value: string;
  valueNum: number;
  maxInSection: number;
}) {
  const max = maxInSection > 0 ? maxInSection : 1;
  const safe = Number.isFinite(valueNum) ? Math.max(0, valueNum) : 0;
  const fillPct = Math.min(100, (safe / max) * 100);

  return (
    <div className="flex items-center gap-2 border-b border-gray-100/80 pb-1.5 last:border-b-0 last:pb-0 dark:border-slate-700/40">
      <span className="min-w-0 flex-1 text-xs leading-snug text-gray-500 dark:text-slate-400">
        {label}
      </span>
      <div
        className="h-1.5 w-[5.5rem] shrink-0 overflow-hidden rounded-full bg-gray-200/90 dark:bg-slate-700/80"
        aria-hidden
      >
        <div
          className="h-full rounded-full bg-slate-500/75 dark:bg-slate-400/55"
          style={{ width: `${fillPct}%` }}
        />
      </div>
      <span className="w-[2.75rem] shrink-0 text-right text-base font-semibold tabular-nums tracking-tight text-gray-900 dark:text-slate-50">
        {value}
      </span>
    </div>
  );
}

function WorkflowMetricsSnapshotContent({
  data,
}: {
  data: WorkflowMetricsSnapshot;
}) {
  const insight = getWorkflowFollowUpInsight(data);
  const readyAvg = data.avgCommentCountBySignal.ready;
  const gapsAvg = data.avgCommentCountBySignal.gaps;
  const needsAvg = data.avgCommentCountBySignal.needs_detail;
  const followUpMax = Math.max(
    0,
    Number.isFinite(readyAvg) ? readyAvg : 0,
    Number.isFinite(gapsAvg) ? gapsAvg : 0,
    Number.isFinite(needsAvg) ? needsAvg : 0,
  );

  const readinessTotal =
    Math.max(0, Math.round(data.reviewerSignalCounts.ready)) +
    Math.max(0, Math.round(data.reviewerSignalCounts.gaps)) +
    Math.max(0, Math.round(data.reviewerSignalCounts.needs_detail));

  const followUpAllZero =
    (!Number.isFinite(readyAvg) || readyAvg <= 0) &&
    (!Number.isFinite(gapsAvg) || gapsAvg <= 0) &&
    (!Number.isFinite(needsAvg) || needsAvg <= 0);

  const requesterAllZero =
    data.intakeAssistUsageCount === 0 &&
    data.intakeAssistSavedCount === 0 &&
    (!Number.isFinite(data.avgMissingDetailCount) ||
      data.avgMissingDetailCount <= 0);

  const insightLine = insight ?? "Not enough data to identify patterns yet.";

  return (
    <div className="space-y-5 text-sm text-gray-800 dark:text-slate-200">
      <WorkflowSectionBlock title="Requester Assist">
        {requesterAllZero ? (
          <p className="text-xs leading-relaxed text-gray-500 dark:text-slate-500">
            No assist activity recorded yet.
          </p>
        ) : (
          <>
            <WorkflowMetricRow
              label="Intake Assist Used"
              value={formatWorkflowCount(data.intakeAssistUsageCount)}
            />
            <WorkflowMetricRow
              label="Tickets Saved After Assist"
              value={formatWorkflowCount(data.intakeAssistSavedCount)}
            />
            <WorkflowMetricRow
              label="Average Missing Details"
              value={formatWorkflowAvg(data.avgMissingDetailCount)}
              valueIsAverage
            />
          </>
        )}
      </WorkflowSectionBlock>

      <WorkflowSectionBlock title="Reviewer Readiness">
        {readinessTotal === 0 ? (
          <p className="text-xs leading-relaxed text-gray-500 dark:text-slate-500">
            No reviewer activity yet.
          </p>
        ) : (
          <>
            <ReviewerReadinessDistributionBar
              ready={data.reviewerSignalCounts.ready}
              gaps={data.reviewerSignalCounts.gaps}
              needsDetail={data.reviewerSignalCounts.needs_detail}
            />
            <div className="flex flex-wrap gap-x-5 gap-y-1 border-b border-gray-100/80 pb-1.5 text-xs dark:border-slate-700/40">
              <span className="text-gray-600 dark:text-slate-400">
                Ready:{" "}
                <span className="font-semibold tabular-nums text-gray-900 dark:text-slate-100">
                  {formatWorkflowCount(data.reviewerSignalCounts.ready)}
                </span>
              </span>
              <span className="text-gray-600 dark:text-slate-400">
                Gaps:{" "}
                <span className="font-semibold tabular-nums text-gray-900 dark:text-slate-100">
                  {formatWorkflowCount(data.reviewerSignalCounts.gaps)}
                </span>
              </span>
              <span className="text-gray-600 dark:text-slate-400">
                Needs detail:{" "}
                <span className="font-semibold tabular-nums text-gray-900 dark:text-slate-100">
                  {formatWorkflowCount(data.reviewerSignalCounts.needs_detail)}
                </span>
              </span>
            </div>
          </>
        )}
      </WorkflowSectionBlock>

      <WorkflowSectionBlock title="Screenshot Insight">
        {data.screenshotInsightUsageCount === 0 ? (
          <p className="text-xs leading-relaxed text-gray-500 dark:text-slate-500">
            No screenshot insight usage yet.
          </p>
        ) : (
          <WorkflowMetricRow
            label="Screenshot Insight Used"
            value={formatWorkflowCount(data.screenshotInsightUsageCount)}
          />
        )}
      </WorkflowSectionBlock>

      <WorkflowSectionBlock
        title="Follow-Up Proxy"
        intro={
          <p className="mb-1.5 text-[11px] leading-relaxed text-gray-500 dark:text-slate-500">
            Average comment activity when this signal was shown.
          </p>
        }
      >
        {followUpAllZero ? (
          <p className="text-xs leading-relaxed text-gray-500 dark:text-slate-500">
            No follow-up data available yet.
          </p>
        ) : (
          <>
            <WorkflowFollowUpRow
              label="Ready for Review"
              value={formatWorkflowAvg(readyAvg)}
              valueNum={readyAvg}
              maxInSection={followUpMax}
            />
            <WorkflowFollowUpRow
              label="Small Gaps Remain"
              value={formatWorkflowAvg(gapsAvg)}
              valueNum={gapsAvg}
              maxInSection={followUpMax}
            />
            <WorkflowFollowUpRow
              label="Needs Detail First"
              value={formatWorkflowAvg(needsAvg)}
              valueNum={needsAvg}
              maxInSection={followUpMax}
            />
          </>
        )}
      </WorkflowSectionBlock>

      <p className="rounded-r-md border border-gray-100 border-l-[3px] border-l-slate-400 bg-white py-1.5 pl-3.5 pr-3 text-[11px] leading-relaxed text-gray-500 dark:border-slate-700/60 dark:border-l-slate-500 dark:bg-slate-800/40 dark:text-slate-500">
        {insightLine}
      </p>
    </div>
  );
}

interface ReportsPageProps {
  tickets: Ticket[];
  onlineUsers: OnlineUser[];
  customReports: CustomReportDefinition[];
  customReportResult: CustomReportResult | null;
  loading: boolean;
  onlineUsersLoading: boolean;
  customReportLoading: boolean;
  error: string | null;
  onlineUsersError: string | null;
  customReportError: string | null;
  showSlaLegend: boolean;
  canViewOnlineUsers: boolean;
  canViewCustomReports: boolean;
  activeSection: ReportSection;
  onChangeSection: (section: ReportSection) => void;
  selectedCustomReportId: number | null;
  onSelectCustomReport: (id: number) => void;
  onToggleSlaLegend: () => void;
  onRefresh: () => void;
  onRefreshOnlineUsers: () => void;
  onRefreshCustomReport: () => void;
  onExportCsv: () => void;
  onExportGoogleSheets: () => void;
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
  Breached: "Open tickets past the SLA deadline (shown as Overdue in the UI).",
  Met: "Resolved or closed before the SLA deadline (shown as Resolved On Time).",
  "Resolved Late": "Resolved or closed after the SLA deadline.",
};

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

function buildSlaExecutiveSummary(openTickets: Ticket[]) {
  const openCount = openTickets.length;
  const atRiskOpen = openTickets.filter((t) => t.slaStatus === "At Risk").length;
  const breachedOpen = openTickets.filter((t) => t.slaStatus === "Breached").length;

  if (openCount === 0) {
    return {
      tone: "neutral" as const,
      headline: "No open tickets in this report.",
      supporting:
        "Figures below still reflect your full visible ticket set, including resolved work.",
    };
  }

  if (breachedOpen === 0 && atRiskOpen === 0) {
    return {
      tone: "positive" as const,
      headline: "All active tickets are currently within SLA.",
      supporting: `${openCount} open ticket${openCount === 1 ? "" : "s"} — none are at risk or overdue.`,
    };
  }

  if (breachedOpen > 0 && atRiskOpen > 0) {
    return {
      tone: "critical" as const,
      headline: `${breachedOpen} ticket${breachedOpen === 1 ? "" : "s"} ${breachedOpen === 1 ? "has" : "have"} breached SLA; ${atRiskOpen} ${atRiskOpen === 1 ? "is" : "are"} at risk.`,
      supporting: "Prioritize overdue items, then work through at-risk tickets before they breach.",
    };
  }

  if (breachedOpen > 0) {
    return {
      tone: "critical" as const,
      headline: `${breachedOpen} ticket${breachedOpen === 1 ? "" : "s"} ${breachedOpen === 1 ? "has" : "have"} breached SLA and need attention.`,
      supporting: "These items are past their SLA target—reassign, escalate, or resolve as soon as practical.",
    };
  }

  return {
    tone: "warning" as const,
    headline: `${atRiskOpen} ticket${atRiskOpen === 1 ? "" : "s"} ${atRiskOpen === 1 ? "is" : "are"} at risk of breaching SLA.`,
    supporting: "They are still inside the warning window—act before the deadline passes.",
  };
}

function executiveSummaryAccentClass(
  tone: "positive" | "warning" | "critical" | "neutral",
) {
  const base =
    "rounded-xl border border-gray-200 bg-white px-6 py-5 shadow-sm dark:border-slate-700 dark:bg-slate-900";
  switch (tone) {
    case "positive":
      return `${base} border-l-[5px] border-l-emerald-500 dark:border-l-emerald-400`;
    case "warning":
      return `${base} border-l-[5px] border-l-amber-500 dark:border-l-amber-400`;
    case "critical":
      return `${base} border-l-[5px] border-l-red-500 dark:border-l-red-400`;
    default:
      return `${base} border-l-[5px] border-l-slate-400 dark:border-l-slate-500`;
  }
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
  emptySupporting,
  emptyVariant = "default",
  onOpenTicket,
}: {
  title: string;
  description: string;
  tickets: Ticket[];
  emptyMessage: string;
  /** Shown under the primary empty line when using compact-balanced variant. */
  emptySupporting?: string;
  emptyVariant?: "default" | "compact-balanced";
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
        emptyVariant === "compact-balanced" ? (
          <div className="px-6 py-5">
            <div className="border-l-2 border-slate-300 py-0.5 pl-4 dark:border-slate-600">
              <p className="text-sm font-medium text-gray-800 dark:text-slate-200">
                {emptyMessage}
              </p>
              {emptySupporting ? (
                <p className="mt-1 text-xs leading-relaxed text-gray-500 dark:text-slate-400">
                  {emptySupporting}
                </p>
              ) : null}
            </div>
          </div>
        ) : (
          <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
            {emptyMessage}
          </div>
        )
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
                    {formatDisplayDateTime(ticket.slaTargetDate)}
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

function AttentionNeededTable({
  tickets,
  onOpenTicket,
}: {
  tickets: Ticket[];
  onOpenTicket: (ticket: Ticket) => void;
}) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
          Attention needed
        </h3>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          Open tickets in the warning window or already past their SLA—open a row to act in the ticket
          modal.
        </p>
      </div>

      {tickets.length === 0 ? (
        <div className="px-6 py-5">
          <div className="border-l-2 border-emerald-500/40 py-0.5 pl-4 dark:border-emerald-500/35">
            <p className="text-sm font-medium text-emerald-900 dark:text-emerald-100">
              Nothing urgent on SLA right now
            </p>
            <p className="mt-1 text-xs leading-relaxed text-emerald-900/75 dark:text-emerald-200/80">
              No open tickets are at risk or overdue in this view. Lists update when you refresh.
            </p>
          </div>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
              <tr>
                <th className="px-4 py-3 font-medium">Work item</th>
                <th className="px-4 py-3 font-medium">Owner</th>
                <th className="px-4 py-3 font-medium">Priority</th>
                <th className="px-4 py-3 font-medium">SLA</th>
                <th className="px-4 py-3 font-medium">Timing</th>
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
                  <td className="max-w-[min(100vw,28rem)] px-4 py-3 align-top">
                    <p className="font-medium leading-snug text-gray-900 dark:text-slate-100">
                      {ticket.title}
                    </p>
                    <p className="mt-1 font-mono text-xs text-gray-500 dark:text-slate-400">
                      {ticket.id}
                    </p>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 align-top text-gray-800 dark:text-slate-200">
                    {getOwnerLabel(ticket)}
                  </td>
                  <td className="px-4 py-3 align-top">{ticket.priority}</td>
                  <td className="px-4 py-3 align-top">
                    <span
                      className={`inline-flex w-fit rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(getSlaDisplayLabel(ticket))}`}
                    >
                      {getSlaDisplayLabel(ticket)}
                    </span>
                  </td>
                  <td className="max-w-xs px-4 py-3 align-top text-gray-700 dark:text-slate-300">
                    <span className="text-sm">{formatSlaSummary(ticket)}</span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 align-top text-gray-600 dark:text-slate-400">
                    {formatDisplayDateTime(ticket.slaTargetDate)}
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

function OnlineUsersReport({
  users,
  error,
  onRefresh,
}: {
  users: OnlineUser[];
  error: string | null;
  onRefresh: () => void;
}) {
  const adminOrDeveloperCount = users.filter(
    (user) => user.role === "Admin" || user.role === "Developer",
  ).length;
  const departmentsRepresented = new Set(
    users
      .map((user) => formatDisplayValue(user.department))
      .filter((department): department is string => department !== "—"),
  ).size;

  return error ? (
    <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
      <p className="text-red-700 dark:text-red-300">{error}</p>
    </div>
  ) : (
    <>
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Online Now
          </p>
          <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
            {users.length}
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Users active within the configured session window.
          </p>
        </div>

        <div className="rounded-lg border border-cortex-blue/20 bg-cortex-blue-soft p-5 dark:border-cortex-blue/30 dark:bg-cortex-blue/10">
          <p className="text-sm font-medium text-cortex-ink dark:text-cortex-cyan">
            Admin / Developer
          </p>
          <p className="mt-3 text-3xl font-semibold text-cortex-ink-dark dark:text-slate-100">
            {adminOrDeveloperCount}
          </p>
          <p className="mt-2 text-sm text-cortex-ink/80 dark:text-slate-300">
            Operational users who can access advanced reports and controls.
          </p>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Departments
          </p>
          <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
            {departmentsRepresented}
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Distinct departments represented by active users.
          </p>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Refresh
          </p>
          <button
            onClick={onRefresh}
            className="mt-3 rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
          >
            Refresh Online Users
          </button>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Pull the latest active presence snapshot from the API.
          </p>
        </div>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
            Online Users
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Presence is based on recent activity heartbeats within the configured
            inactivity timeout.
          </p>
        </div>

        {users.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No users are currently online.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Display Name</th>
                  <th className="px-4 py-3 font-medium">Role</th>
                  <th className="px-4 py-3 font-medium">Department</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium">Last Seen</th>
                  <th className="px-4 py-3 font-medium">Last Login</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr
                    key={user.id}
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {user.displayName}
                        </p>
                        {user.nickName && (
                          <p className="text-xs text-gray-500 dark:text-slate-400">
                            {user.nickName}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">{humanizeEnumLabel(user.role)}</td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(user.department)}
                    </td>
                    <td className="px-4 py-3 align-top">{user.email}</td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastSeenDateUtc)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastLoginDate)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}

export default function ReportsPage({
  tickets,
  onlineUsers,
  customReports,
  customReportResult,
  loading,
  onlineUsersLoading,
  customReportLoading,
  error,
  onlineUsersError,
  customReportError,
  showSlaLegend,
  canViewOnlineUsers,
  canViewCustomReports,
  activeSection,
  onChangeSection,
  selectedCustomReportId,
  onSelectCustomReport,
  onToggleSlaLegend,
  onRefresh,
  onRefreshOnlineUsers,
  onRefreshCustomReport,
  onExportCsv,
  onExportGoogleSheets,
  onOpenTicket,
}: ReportsPageProps) {
  const [isExportMenuOpen, setIsExportMenuOpen] = useState(false);
  const exportMenuRef = useRef<HTMLDivElement | null>(null);
  const { getAccessTokenSilently } = useAuth0();
  const [workflowMetrics, setWorkflowMetrics] =
    useState<WorkflowMetricsSnapshot | null>(null);
  const [workflowMetricsError, setWorkflowMetricsError] = useState<
    string | null
  >(null);
  const [workflowMetricsLoading, setWorkflowMetricsLoading] = useState(true);

  useEffect(() => {
    if (!isExportMenuOpen) {
      return undefined;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!exportMenuRef.current?.contains(event.target as Node)) {
        setIsExportMenuOpen(false);
      }
    };

    window.addEventListener("mousedown", handlePointerDown);
    return () => window.removeEventListener("mousedown", handlePointerDown);
  }, [isExportMenuOpen]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const data = await metricsService.getWorkflowMetricsSnapshot(token);
        if (!cancelled) {
          setWorkflowMetrics(data);
          setWorkflowMetricsError(null);
        }
      } catch {
        if (!cancelled) {
          setWorkflowMetricsError("Unable to load workflow metrics.");
          setWorkflowMetrics(null);
        }
      } finally {
        if (!cancelled) {
          setWorkflowMetricsLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [getAccessTokenSilently]);

  if (
    (activeSection === "sla" && loading) ||
    (activeSection === "online-users" && onlineUsersLoading) ||
    (activeSection === "custom" && customReportLoading)
  ) {
    return <ReportsSkeleton />;
  }

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

  const openTickets = tickets.filter(isOpenTicket);
  const executiveSummary = buildSlaExecutiveSummary(openTickets);

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
                Reports
              </h2>
              <p className="text-sm text-gray-500 dark:text-slate-400">
                Review operational insights and role-specific reporting.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              {activeSection === "sla" && (
                <>
                  <div className="relative" ref={exportMenuRef}>
                    <button
                      onClick={() =>
                        setIsExportMenuOpen((currentValue) => !currentValue)
                      }
                      className="rounded-md bg-cortex-ink px-4 py-2 text-white transition-colors hover:bg-cortex-ink-dark"
                    >
                      Export Report
                    </button>

                    {isExportMenuOpen && (
                      <div className="absolute right-0 z-20 mt-2 w-56 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-700 dark:bg-slate-900">
                        <button
                          onClick={() => {
                            setIsExportMenuOpen(false);
                            onExportCsv();
                          }}
                          className="block w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:text-slate-100 dark:hover:bg-slate-800"
                        >
                          Export as CSV
                        </button>
                        <button
                          onClick={() => {
                            setIsExportMenuOpen(false);
                            onExportGoogleSheets();
                          }}
                          className="block w-full border-t border-gray-100 px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:border-slate-800 dark:text-slate-100 dark:hover:bg-slate-800"
                        >
                          Export for Google Sheets
                        </button>
                      </div>
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={onToggleSlaLegend}
                    className="rounded-md border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-600 shadow-sm transition-colors hover:bg-gray-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-300 dark:hover:bg-slate-800"
                  >
                    {showSlaLegend ? "Hide SLA definitions" : "SLA definitions"}
                  </button>
                  <button
                    onClick={onRefresh}
                    className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                  >
                    Refresh
                  </button>
                </>
              )}

              {activeSection === "online-users" && (
                <button
                  onClick={onRefreshOnlineUsers}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Refresh
                </button>
              )}

              {activeSection === "custom" && (
                <button
                  onClick={onRefreshCustomReport}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Refresh
                </button>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => onChangeSection("sla")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                activeSection === "sla"
                  ? "bg-cortex-blue text-white"
                  : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              }`}
            >
              SLA Report
            </button>

            {canViewOnlineUsers && (
              <button
                onClick={() => onChangeSection("online-users")}
                className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                  activeSection === "online-users"
                    ? "bg-cortex-blue text-white"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                Online Users
              </button>
            )}

            {canViewCustomReports &&
              customReports
                .filter((report) => report.isEnabled)
                .map((report) => (
                  <button
                    key={report.id}
                    onClick={() => {
                      onChangeSection("custom");
                      onSelectCustomReport(report.id);
                    }}
                    className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                      activeSection === "custom" && selectedCustomReportId === report.id
                        ? "bg-cortex-blue text-white"
                        : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                    }`}
                  >
                    {report.name}
                  </button>
                ))}
          </div>
        </div>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
          <h3 className="text-lg font-bold tracking-tight text-gray-900 dark:text-slate-100">
            Workflow Metrics (Preview)
          </h3>
          <p className="mt-1.5 text-sm leading-relaxed text-gray-500 dark:text-slate-400">
            Operational snapshot of intake quality, reviewer readiness, and
            follow-up signals.
          </p>
          <p className="mt-1 text-xs text-gray-400 dark:text-slate-500">
            Early metrics from real Cortex usage (all-time, v1).
          </p>
        </div>
        <div className="px-6 py-4">
          {workflowMetricsLoading ? (
            <p className="text-sm text-gray-500 dark:text-slate-400">Loading…</p>
          ) : workflowMetricsError ? (
            <p className="text-sm text-gray-600 dark:text-slate-400">
              {workflowMetricsError}
            </p>
          ) : workflowMetrics ? (
            <WorkflowMetricsSnapshotContent data={workflowMetrics} />
          ) : null}
        </div>
      </section>

      {activeSection === "sla" ? (
        <>
          {showSlaLegend && <SlaLegend />}

          {error ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">{error}</p>
            </div>
          ) : totalTickets === 0 ? (
            <section className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
              No ticket data is available for reporting yet.
            </section>
          ) : (
            <div className="space-y-6">
              <section
                className={executiveSummaryAccentClass(executiveSummary.tone)}
                aria-labelledby="sla-executive-summary-heading"
              >
                <h3
                  id="sla-executive-summary-heading"
                  className="sr-only"
                >
                  Executive summary
                </h3>
                <p className="text-xl font-semibold leading-snug tracking-tight text-gray-900 dark:text-slate-50 sm:text-2xl">
                  {executiveSummary.headline}
                </p>
                <p className="mt-2 max-w-4xl text-sm leading-relaxed text-gray-600 dark:text-slate-400">
                  {executiveSummary.supporting}
                </p>
              </section>

              <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                  <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
                    Total Tickets
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
                    {totalTickets}
                  </p>
                  <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                    Visible to your current role.
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
                    Overdue open tickets or resolved after the SLA deadline.
                  </p>
                </div>
              </section>

              {/* Future: drop a full-width SLA trend chart here without changing the outer layout. */}

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
                              className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(mapBackendSlaStatusToDisplayLabel(status))}`}
                            >
                              {mapBackendSlaStatusToDisplayLabel(status)}
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
                <AttentionNeededTable
                  tickets={actionableTickets}
                  onOpenTicket={onOpenTicket}
                />

                <RiskTable
                  title="Resolved Late"
                  description="Tickets that were completed after their SLA target."
                  tickets={resolvedLateTickets}
                  emptyMessage="No tickets have been resolved late."
                  emptySupporting="Closures after the SLA deadline will appear here."
                  emptyVariant="compact-balanced"
                  onOpenTicket={onOpenTicket}
                />
              </div>
            </div>
          )}
        </>
      ) : activeSection === "online-users" ? (
        <OnlineUsersReport
          users={onlineUsers}
          error={onlineUsersError}
          onRefresh={onRefreshOnlineUsers}
        />
      ) : null}

      {activeSection === "custom" && (
        <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
          <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              {customReportResult?.reportName ??
                customReports.find((report) => report.id === selectedCustomReportId)?.name ??
                "Custom Report"}
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Custom SQL report registered in Configuration.
            </p>
          </div>

          {customReportError ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">{customReportError}</p>
            </div>
          ) : !customReportResult ? (
            <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
              Select a custom report to run it.
            </div>
          ) : (
            <div className="space-y-4 px-6 py-6">
              <div className="flex flex-col gap-2 text-sm text-gray-500 dark:text-slate-400">
                <span>Generated {formatDisplayDateTime(customReportResult.generatedDateUtc)}</span>
                {customReportResult.isTruncated && (
                  <span className="text-amber-700 dark:text-amber-300">
                    Showing the first 500 rows for performance.
                  </span>
                )}
              </div>

              {customReportResult.rows.length === 0 ? (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-400">
                  This report returned no rows.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full text-sm">
                    <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                      <tr>
                        {customReportResult.columns.map((column) => (
                          <th key={column} className="px-4 py-3 font-medium">
                            {column}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {customReportResult.rows.map((row, rowIndex) => (
                        <tr
                          key={rowIndex}
                          className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                        >
                          {customReportResult.columns.map((column) => (
                            <td key={`${rowIndex}-${column}`} className="px-4 py-3 align-top">
                              {String(row[column] ?? "—")}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </section>
      )}
    </div>
  );
}
