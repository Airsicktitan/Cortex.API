import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useMemo, useRef, useState } from "react";
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
  formatTicketIdentifier,
  humanizeEnumLabel,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";
import { isOpenTicket } from "../utils/ticketLifecycle";
import {
  UNASSIGNED_FILTER,
  computeColumnDistincts,
  getCustomReportColumnFilterKind,
  hasAnyCustomReportFilter,
  rowMatchesCustomReportFilters,
} from "../utils/customReportFilters";
import type { WorkflowMetricsSnapshot } from "../types/workflowMetrics";
import type {
  RepeatIssueAiReviewResponse,
  RepeatIssueGroupDetailResponse,
  RepeatIssueGroupSummary,
  RepeatIssueOverviewResponse,
} from "../types/repeatIssues";
import { metricsService, repeatIssuesService } from "../services/api";
import { CortexTooltip } from "./ui/Tooltip";
import { ScrollableViewport } from "./ui/ScrollableViewport";

const API_AUDIENCE = "https://cortex-api";

type ReportSection =
  | "sla"
  | "telemetry"
  | "recurring-issues"
  | "online-users"
  | "custom";

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

function formatWorkflowPercent(part: number, total: number): string {
  if (!Number.isFinite(part) || !Number.isFinite(total) || total <= 0) {
    return "—";
  }
  return `${Math.round((part / total) * 100)}%`;
}

/** When follow-up averages are comparable and Ready &lt; Needs detail, surface a subtle insight. */
function getWorkflowFollowUpInsight(m: WorkflowMetricsSnapshot): string | null {
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

function getFollowUpFrictionInsight(
  readyAvg: number,
  gapsAvg: number,
  needsAvg: number,
) {
  const highest = Math.max(
    Number.isFinite(readyAvg) ? readyAvg : 0,
    Number.isFinite(gapsAvg) ? gapsAvg : 0,
    Number.isFinite(needsAvg) ? needsAvg : 0,
  );
  if (highest >= 5) {
    return "High Follow-Up Friction: unclear intake is likely creating repeated clarification work.";
  }
  if (highest >= 2) {
    return "Moderate Follow-Up Friction: reviewers still need extra clarification on some requests.";
  }
  return "Low Follow-Up Friction: ticket detail is generally clear enough to keep decisions moving.";
}

function TelemetrySummaryChip({
  label,
  value,
  hint,
  valueTone = "neutral",
}: {
  label: string;
  value: string;
  hint?: string;
  valueTone?: "neutral" | "positive" | "warning" | "critical";
}) {
  const valueClass =
    valueTone === "positive"
      ? "text-emerald-700 dark:text-emerald-300"
      : valueTone === "warning"
        ? "text-amber-800 dark:text-amber-300"
        : valueTone === "critical"
          ? "text-red-700 dark:text-red-300"
          : "text-gray-900 dark:text-slate-50";
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-800 dark:bg-slate-900/70">
      <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
        {label}
      </p>
      <p className={`mt-1 text-xl font-semibold tabular-nums tracking-tight ${valueClass}`}>
        {value}
      </p>
      {hint ? (
        <p className="mt-0.5 text-[11px] leading-snug text-gray-500 dark:text-slate-500">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

type TelemetryDonutSlice = {
  label: string;
  count: number;
  total: number;
  colorClass: string;
};

/**
 * Compact donut chart for reviewer readiness distribution.
 * The current telemetry payload is aggregate (not time-series), so share-based
 * composition best communicates submission quality at a glance.
 */
function TelemetryDonutChart({ slices }: { slices: TelemetryDonutSlice[] }) {
  const total = Math.max(
    1,
    slices.reduce((sum, slice) => sum + slice.count, 0),
  );
  const radius = 62;
  const strokeWidth = 24;
  const center = 90;
  const circumference = 2 * Math.PI * radius;
  const chartSlices = slices.map((slice, index) => {
    const fraction = slice.count / total;
    const offsetFraction = slices
      .slice(0, index)
      .reduce((sum, priorSlice) => sum + priorSlice.count / total, 0);

    return {
      slice,
      dash: fraction * circumference,
      offset: -offsetFraction * circumference,
    };
  });

  return (
    <div className="grid gap-4 lg:grid-cols-[13rem_minmax(0,1fr)]">
      <div
        className="mx-auto flex h-[11.25rem] w-[11.25rem] items-center justify-center"
        role="img"
        aria-label="Reviewer readiness distribution donut chart"
      >
        <svg viewBox="0 0 180 180" className="h-full w-full">
          <circle
            cx={center}
            cy={center}
            r={radius}
            fill="none"
            strokeWidth={strokeWidth}
            className="stroke-gray-100 dark:stroke-slate-800"
          />
          {chartSlices.map(({ slice, dash, offset }) => (
            <circle
              key={slice.label}
              cx={center}
              cy={center}
              r={radius}
              fill="none"
              strokeWidth={strokeWidth}
              strokeLinecap="butt"
              strokeDasharray={`${dash} ${circumference - dash}`}
              strokeDashoffset={offset}
              transform={`rotate(-90 ${center} ${center})`}
              className={slice.colorClass}
            />
          ))}
          <text
            x={center}
            y={center - 4}
            textAnchor="middle"
            className="fill-gray-900 text-xl font-semibold tabular-nums dark:fill-slate-100"
          >
            {formatWorkflowCount(total)}
          </text>
          <text
            x={center}
            y={center + 13}
            textAnchor="middle"
            className="fill-gray-500 text-[11px] dark:fill-slate-400"
          >
            signals
          </text>
        </svg>
      </div>

      <div className="space-y-2.5 self-center">
        {slices.map((slice) => {
          const pct =
            slice.total > 0 ? Math.round((slice.count / slice.total) * 100) : 0;
          return (
            <div
              key={slice.label}
              className="flex items-center justify-between gap-3 rounded-md border border-gray-100 bg-gray-50/70 px-3 py-2 dark:border-slate-800 dark:bg-slate-900/40"
            >
              <div className="flex min-w-0 items-center gap-2">
                <span
                  className={`inline-block h-2.5 w-2.5 rounded-full ${slice.colorClass}`}
                />
                <span className="truncate text-xs font-medium text-gray-700 dark:text-slate-300">
                  {slice.label}
                </span>
              </div>
              <span className="shrink-0 text-xs tabular-nums text-gray-600 dark:text-slate-400">
                <span className="font-semibold text-gray-900 dark:text-slate-100">
                  {formatWorkflowCount(slice.count)}
                </span>{" "}
                ({pct}%)
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function getReviewerReadinessInsight(
  readyCount: number,
  gapsCount: number,
  needsCount: number,
  total: number,
): string | null {
  if (total <= 0) {
    return null;
  }

  if (needsCount === total) {
    return "100% of reviewer signals currently require more detail before review.";
  }

  const needsShare = needsCount / total;
  if (needsShare >= 0.6) {
    return "Most reviewer signals currently need more detail, indicating an intake quality gap.";
  }

  const readyShare = readyCount / total;
  if (readyShare >= 0.6) {
    return "Most reviewer signals are already ready for review, indicating strong intake quality.";
  }

  if (gapsCount / total >= 0.5) {
    return "A large share of submissions have small gaps, suggesting targeted form improvements could quickly raise readiness.";
  }

  return null;
}

function TelemetryFollowUpRow({
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
    <div className="flex items-center gap-3">
      <span className="min-w-0 flex-1 text-xs leading-snug text-gray-600 dark:text-slate-400">
        {label}
      </span>
      <div
        className="h-1.5 w-[5rem] shrink-0 overflow-hidden rounded-full bg-gray-100 dark:bg-slate-800"
        aria-hidden
      >
        <div
          className="h-full rounded-full bg-slate-400/80 dark:bg-slate-400/60"
          style={{ width: `${fillPct}%` }}
        />
      </div>
      <span className="w-[2.25rem] shrink-0 text-right text-sm font-semibold tabular-nums tracking-tight text-gray-900 dark:text-slate-100">
        {value}
      </span>
    </div>
  );
}

function TelemetryEmptyState() {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-gray-200 bg-gray-50/70 px-6 py-8 text-center dark:border-slate-700 dark:bg-slate-900/40">
      <p className="text-sm font-medium text-gray-700 dark:text-slate-200">
        No telemetry yet
      </p>
      <p className="mt-1 max-w-sm text-xs leading-relaxed text-gray-500 dark:text-slate-400">
        Review readiness, follow-up friction, and Cortex Assist impact will
        appear here as users work through tickets.
      </p>
    </div>
  );
}

function TelemetryOverviewContent({ data }: { data: WorkflowMetricsSnapshot }) {
  const readyCount = Math.max(0, Math.round(data.reviewerSignalCounts.ready));
  const gapsCount = Math.max(0, Math.round(data.reviewerSignalCounts.gaps));
  const needsCount = Math.max(
    0,
    Math.round(data.reviewerSignalCounts.needs_detail),
  );
  const reviewerTotal = readyCount + gapsCount + needsCount;

  const intakeUsed = Math.max(0, Math.round(data.intakeAssistUsageCount));
  const screenshotUsed = Math.max(
    0,
    Math.round(data.screenshotInsightUsageCount),
  );

  const totalEvents = reviewerTotal + intakeUsed + screenshotUsed;

  if (totalEvents === 0) {
    return <TelemetryEmptyState />;
  }

  const readyRateLabel = `${formatWorkflowPercent(readyCount, reviewerTotal)} ready for review`;

  const readyAvg = data.avgCommentCountBySignal.ready;
  const gapsAvg = data.avgCommentCountBySignal.gaps;
  const needsAvg = data.avgCommentCountBySignal.needs_detail;
  const followUpMax = Math.max(
    0,
    Number.isFinite(readyAvg) ? readyAvg : 0,
    Number.isFinite(gapsAvg) ? gapsAvg : 0,
    Number.isFinite(needsAvg) ? needsAvg : 0,
  );
  const followUpAllZero = followUpMax <= 0;
  const missingDetailLabel = `${formatWorkflowAvg(data.avgMissingDetailCount)} gaps`;
  const assistSavedCount = Math.max(0, Math.round(data.intakeAssistSavedCount));
  const assistImpactLabel =
    intakeUsed > 0
      ? `Cortex Assist impact: ${formatWorkflowPercent(assistSavedCount, intakeUsed)} time saved`
      : "Cortex Assist impact not measured yet";
  const needsDetailFollowUpLabel =
    Number.isFinite(needsAvg)
      ? `Average follow-up: ${formatWorkflowAvg(needsAvg)} comments`
      : "Average follow-up: 0.0 comments";

  const readinessSlices: TelemetryDonutSlice[] = [
    {
      label: "Ready for review",
      count: readyCount,
      total: reviewerTotal,
      colorClass:
        "stroke-emerald-500 bg-emerald-500 dark:stroke-emerald-400 dark:bg-emerald-400",
    },
    {
      label: "Small gaps remain",
      count: gapsCount,
      total: reviewerTotal,
      colorClass:
        "stroke-amber-500 bg-amber-500 dark:stroke-amber-400 dark:bg-amber-400",
    },
    {
      label: "Needs detail first",
      count: needsCount,
      total: reviewerTotal,
      colorClass:
        "stroke-rose-500 bg-rose-500 dark:stroke-rose-400 dark:bg-rose-400",
    },
  ];

  const insight =
    getWorkflowFollowUpInsight(data) ??
    "Reviewer readiness reflects how ticket detail is trending across submissions.";
  const readinessInsight = getReviewerReadinessInsight(
    readyCount,
    gapsCount,
    needsCount,
    reviewerTotal,
  );

  return (
    <div className="space-y-5">
      <section
        className="grid grid-cols-2 gap-3 md:grid-cols-4"
        aria-label="Workflow insight metrics"
      >
        <TelemetrySummaryChip
          label="Intake Quality"
          value={missingDetailLabel}
          hint="Average missing details per ticket; lower means reviewers can decide sooner."
          valueTone={data.avgMissingDetailCount >= 3 ? "warning" : "neutral"}
        />
        <TelemetrySummaryChip
          label="Review Readiness"
          value={readyRateLabel}
          hint={
            reviewerTotal > 0
              ? `${formatWorkflowCount(readyCount)} of ${formatWorkflowCount(reviewerTotal)} tickets were ready without additional follow-up.`
              : "Awaiting reviewer activity"
          }
          valueTone={readyCount / Math.max(reviewerTotal, 1) >= 0.6 ? "positive" : "warning"}
        />
        <TelemetrySummaryChip
          label="Follow-Up Friction"
          value={needsDetailFollowUpLabel}
          hint={
            "Needs-detail tickets require this many comments on average, showing clarification effort."
          }
          valueTone={needsAvg >= 5 ? "critical" : needsAvg >= 2 ? "warning" : "positive"}
        />
        <TelemetrySummaryChip
          label="Cortex Assist Impact"
          value={assistImpactLabel}
          hint={
            intakeUsed > 0
              ? `${formatWorkflowCount(intakeUsed)} assisted intake sessions; ${formatWorkflowCount(screenshotUsed)} visual evidence checks.`
              : "No assisted intake sessions have been recorded yet."
          }
          valueTone={intakeUsed > 0 ? "positive" : "neutral"}
        />
      </section>

      <section
        className="rounded-lg border border-gray-200 bg-white px-4 py-4 dark:border-slate-800 dark:bg-slate-900/60"
        aria-labelledby="telemetry-chart-heading"
      >
        <div className="mb-3 flex items-baseline justify-between gap-3">
          <div>
            <h4
              id="telemetry-chart-heading"
              className="text-sm font-semibold text-gray-900 dark:text-slate-100"
            >
              Review Readiness
            </h4>
            <p className="mt-0.5 text-[11px] leading-snug text-gray-500 dark:text-slate-400">
              Shows whether new submissions can move forward without clarification.
            </p>
          </div>
          <p className="shrink-0 text-[11px] tabular-nums text-gray-500 dark:text-slate-500">
            {formatWorkflowCount(reviewerTotal)} reviewed tickets
          </p>
        </div>

        {reviewerTotal === 0 ? (
          <p className="py-2 text-xs text-gray-500 dark:text-slate-500">
            No reviewer activity yet.
          </p>
        ) : (
          <div className="space-y-2">
            <TelemetryDonutChart slices={readinessSlices} />
            {readinessInsight ? (
              <p className="text-xs text-gray-600 dark:text-slate-400">
                {readinessInsight}
              </p>
            ) : null}
          </div>
        )}
      </section>

      {!followUpAllZero && (
        <section
          className="rounded-lg border border-gray-100 bg-gray-50/60 px-4 py-3 dark:border-slate-800 dark:bg-slate-900/30"
          aria-label="Follow-up friction by readiness signal"
        >
          <div className="mb-2 flex items-baseline justify-between gap-3">
            <p className="text-[11px] font-semibold uppercase tracking-wider text-gray-600 dark:text-slate-300">
              Follow-Up Friction
            </p>
            <p className="text-[11px] text-gray-500 dark:text-slate-500">
              Average comments per ticket
            </p>
          </div>
          <div className="space-y-1.5">
            <TelemetryFollowUpRow
              label="Ready for review"
              value={formatWorkflowAvg(readyAvg)}
              valueNum={readyAvg}
              maxInSection={followUpMax}
            />
            <TelemetryFollowUpRow
              label="Small gaps remain"
              value={formatWorkflowAvg(gapsAvg)}
              valueNum={gapsAvg}
              maxInSection={followUpMax}
            />
            <TelemetryFollowUpRow
              label="Needs detail first"
              value={formatWorkflowAvg(needsAvg)}
              valueNum={needsAvg}
              maxInSection={followUpMax}
            />
          </div>
        </section>
      )}

      <p className="text-[11px] leading-relaxed text-gray-500 dark:text-slate-500">
        {getFollowUpFrictionInsight(readyAvg, gapsAvg, needsAvg)} {insight}
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
  onCreateRootCauseTask: (draft: RootCauseTaskDraftInput) => void;
}

type RootCauseTaskDraftInput = {
  title: string;
  description: string;
  boardId?: number;
  priority?: string;
};

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

function buildWorkloadBalanceInsight(openTickets: Ticket[]) {
  const ownerCounts = new Map<string, number>();
  for (const ticket of openTickets) {
    const owner = getOwnerLabel(ticket);
    if (owner === "—") {
      continue;
    }
    ownerCounts.set(owner, (ownerCounts.get(owner) ?? 0) + 1);
  }

  if (ownerCounts.size === 0) {
    return {
      value: "—",
      hint: "No assigned open tickets to compare yet.",
      tone: "neutral" as const,
    };
  }

  const [owner, count] = Array.from(ownerCounts.entries()).sort(
    (left, right) => right[1] - left[1],
  )[0];
  const tone = count >= 8 ? "warning" : "neutral";
  return {
    value: `${count}`,
    hint: `${owner} has the highest visible open workload.`,
    tone,
  };
}

function buildSlaExecutiveSummary(openTickets: Ticket[]) {
  const openCount = openTickets.length;
  const atRiskOpen = openTickets.filter(
    (t) => t.slaStatus === "At Risk",
  ).length;
  const breachedOpen = openTickets.filter(
    (t) => t.slaStatus === "Breached",
  ).length;

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
      supporting:
        "Prioritize overdue items, then work through at-risk tickets before they breach.",
    };
  }

  if (breachedOpen > 0) {
    return {
      tone: "critical" as const,
      headline: `${breachedOpen} ticket${breachedOpen === 1 ? "" : "s"} ${breachedOpen === 1 ? "has" : "have"} breached SLA and need attention.`,
      supporting:
        "These items are past their SLA target—reassign, escalate, or resolve as soon as practical.",
    };
  }

  return {
    tone: "warning" as const,
    headline: `${atRiskOpen} ticket${atRiskOpen === 1 ? "" : "s"} ${atRiskOpen === 1 ? "is" : "are"} at risk of breaching SLA.`,
    supporting:
      "They are still inside the warning window—act before the deadline passes.",
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
    if (
      leftTicket.slaStatus === "Breached" &&
      rightTicket.slaStatus !== "Breached"
    ) {
      return -1;
    }

    if (
      leftTicket.slaStatus !== "Breached" &&
      rightTicket.slaStatus === "Breached"
    ) {
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
                  <td className="px-4 py-3 align-top">
                    {getOwnerLabel(ticket)}
                  </td>
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
          Open tickets in the warning window or already past their SLA—open a
          row to act in the ticket modal.
        </p>
      </div>

      {tickets.length === 0 ? (
        <div className="px-6 py-5">
          <div className="border-l-2 border-emerald-500/40 py-0.5 pl-4 dark:border-emerald-500/35">
            <p className="text-sm font-medium text-emerald-900 dark:text-emerald-100">
              Nothing urgent on SLA right now
            </p>
            <p className="mt-1 text-xs leading-relaxed text-emerald-900/75 dark:text-emerald-200/80">
              No open tickets are at risk or overdue in this view. Lists update
              when you refresh.
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
            Presence is based on recent activity heartbeats within the
            configured inactivity timeout.
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
                    <td className="px-4 py-3 align-top">
                      {humanizeEnumLabel(user.role)}
                    </td>
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

function formatHoursLabel(hours: number | null | undefined): string {
  if (hours === null || hours === undefined || !Number.isFinite(hours)) {
    return "—";
  }
  if (hours < 1) {
    const minutes = Math.max(0, Math.round(hours * 60));
    return `${minutes}m`;
  }
  if (hours < 72) {
    return `${hours.toFixed(1)}h`;
  }
  return `${(hours / 24).toFixed(1)}d`;
}

function formatIsoDate(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return "—";
  }
  return parsed.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function formatCountLabel(
  value: number,
  singular: string,
  plural: string,
): string {
  return `${value} ${value === 1 ? singular : plural}`;
}

function buildRecurringIssueInsight(summary: RepeatIssueGroupSummary): {
  headline: string;
  impact: string;
} {
  const headline = `This issue has been handled ${formatCountLabel(summary.repeatCount, "time", "times")} and still has ${formatCountLabel(summary.openCount, "open ticket", "open tickets")}.`;

  if (summary.openCount > 0) {
    return {
      headline,
      impact:
        "It is consuming repeated effort instead of being resolved at the source.",
    };
  }

  if (summary.trendLabel === "rising") {
    return {
      headline,
      impact:
        "The trend is rising, which suggests the underlying cause is still active.",
    };
  }

  return {
    headline,
    impact:
      "The pattern keeps reappearing, so a root-cause fix can prevent repeat work.",
  };
}

function buildRootCauseTaskDraft(
  detail: RepeatIssueGroupDetailResponse,
  review: RepeatIssueAiReviewResponse | null,
): RootCauseTaskDraftInput {
  const summary = detail.summary;
  const safeTitle =
    summary.representativeTitle?.trim() || "Recurring issue pattern";
  const trendToneLabel =
    summary.trendLabel === "rising"
      ? "increasing"
      : summary.trendLabel === "falling"
        ? "decreasing"
        : "stable";
  const trendDescriptor =
    summary.trendDelta === 0
      ? `${trendToneLabel} (no change vs prior period)`
      : `${trendToneLabel} (${summary.trendDelta > 0 ? "+" : ""}${summary.trendDelta} vs prior period)`;
  const repeatedEffort = formatHoursLabel(summary.totalResolutionHours);

  const signatureSummary =
    summary.signatureTokens.length > 0
      ? summary.signatureTokens.join(", ")
      : "No clear recurring pattern keywords identified";

  const descriptionLines = [
    `⚠︎ This issue has been handled ${summary.repeatCount} times and still has ${summary.openCount} open tickets, indicating the problem is being repeatedly addressed instead of resolved at the source.`,
    "",
    `Total repeated effort: ${repeatedEffort}`,
    `Trend: ${trendDescriptor}`,
    "",
    "Context:",
    `- Recurring issue: ${safeTitle}`,
    `- Board: ${summary.boardName}`,
    `- Pattern: ${signatureSummary}`,
    "",
    "Define and execute a root-cause fix to eliminate this issue at the source.",
  ];

  if (review && !review.unavailable) {
    const summaryText = review.summary?.trim();
    if (summaryText) {
      descriptionLines.push("", `Cortex Assist summary: ${summaryText}`);
    }

    if (review.suggestedNextSteps.length > 0) {
      const compactSteps = review.suggestedNextSteps
        .slice(0, 2)
        .map((step) => `${step.category}: ${step.rationale}`)
        .join("; ");
      if (compactSteps) {
        descriptionLines.push(`Suggested next steps: ${compactSteps}`);
      }
    }
  }

  const normalizedPriority = detail.dominantPriority?.trim();
  const safePriority =
    normalizedPriority &&
    ["Critical", "High", "Medium", "Low"].includes(normalizedPriority)
      ? normalizedPriority
      : undefined;

  return {
    title: `Root cause review: ${safeTitle}`,
    description: descriptionLines.join("\n"),
    boardId: summary.boardId,
    priority: safePriority,
  };
}

function RepeatIssueTrendBadge({
  label,
  delta,
}: {
  label: string;
  delta: number;
}) {
  const toneClass =
    label === "rising"
      ? "bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300"
      : label === "falling"
        ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300"
        : "bg-gray-100 text-gray-700 dark:bg-slate-800 dark:text-slate-300";
  const arrow = label === "rising" ? "↑" : label === "falling" ? "↓" : "→";
  const signedDelta = delta > 0 ? `+${delta}` : String(delta);
  const deltaText = delta === 0 ? "" : ` ${signedDelta}`;
  return (
    <span
      aria-label={`Trend ${label} (${signedDelta} vs prior 30 days)`}
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${toneClass}`}
    >
      <span aria-hidden="true">{arrow}</span>
      <span className="capitalize">{label}</span>
      {deltaText ? (
        <span className="text-[10px] opacity-80">{deltaText}</span>
      ) : null}
    </span>
  );
}

function RepeatIssueSummaryChip({
  label,
  value,
  hint,
  tooltip,
}: {
  label: string;
  value: string;
  hint?: string;
  tooltip: string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-800 dark:bg-slate-900/70">
      <div className="flex items-baseline gap-1">
        <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
          {label}
        </p>
        <CortexTooltip content={tooltip}>
          <button
            type="button"
            aria-label={`About ${label}`}
            className="relative top-[1px] inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-current text-[9px] font-semibold leading-none text-gray-400 hover:text-gray-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue dark:text-slate-500 dark:hover:text-slate-300"
          >
            <span aria-hidden="true">i</span>
          </button>
        </CortexTooltip>
      </div>
      <p className="mt-1 text-xl font-semibold tabular-nums tracking-tight text-gray-900 dark:text-slate-50">
        {value}
      </p>
      {hint ? (
        <p className="mt-0.5 text-[11px] leading-snug text-gray-500 dark:text-slate-500">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

function RepeatIssueRankTable({
  groups,
  selectedKey,
  onSelect,
}: {
  groups: RepeatIssueGroupSummary[];
  selectedKey: string | null;
  onSelect: (groupKey: string) => void;
}) {
  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-800">
      <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-slate-800">
        <thead className="bg-gray-50 text-left text-[11px] font-semibold uppercase tracking-wider text-gray-500 dark:bg-slate-900/70 dark:text-slate-400">
          <tr>
            <th scope="col" className="px-4 py-2.5">
              Recurring issue
            </th>
            <th scope="col" className="px-4 py-2.5 text-right">
              Repeats
            </th>
            <th scope="col" className="px-4 py-2.5 text-right">
              Open
            </th>
            <th scope="col" className="px-4 py-2.5 text-right">
              Last seen
            </th>
            <th scope="col" className="px-4 py-2.5 text-right">
              <CortexTooltip content="Sum of lifecycle durations (created → closed) across tickets in this group. This is a duration proxy, not human work time.">
                <span className="inline-flex items-baseline gap-1 underline decoration-dotted decoration-gray-400 underline-offset-2">
                  Repeated effort
                </span>
              </CortexTooltip>
            </th>
            <th scope="col" className="px-4 py-2.5">
              Trend
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white dark:divide-slate-800 dark:bg-slate-900">
          {groups.map((group) => {
            const isSelected = group.groupKey === selectedKey;
            return (
              <tr
                key={group.groupKey}
                onClick={() => onSelect(group.groupKey)}
                className={`cursor-pointer transition-colors ${
                  isSelected
                    ? "bg-cortex-blue/5 dark:bg-cortex-blue/10"
                    : "hover:bg-gray-50 dark:hover:bg-slate-800/40"
                }`}
              >
                <td className="px-4 py-3 align-top">
                  <button
                    type="button"
                    onClick={(event) => {
                      event.stopPropagation();
                      onSelect(group.groupKey);
                    }}
                    className="text-left"
                  >
                    <p className="font-medium text-gray-900 dark:text-slate-100 line-clamp-1">
                      {group.representativeTitle || "(untitled)"}
                    </p>
                    <p className="mt-0.5 text-[11px] text-gray-500 dark:text-slate-500">
                      <span className="font-medium">{group.boardName}</span>
                      {group.signatureTokens.length > 0 ? (
                        <span>
                          {" · "}
                          {group.signatureTokens.join(", ")}
                        </span>
                      ) : null}
                    </p>
                  </button>
                </td>
                <td className="px-4 py-3 text-right align-top tabular-nums font-semibold text-gray-900 dark:text-slate-100">
                  {group.repeatCount}
                </td>
                <td className="px-4 py-3 text-right align-top tabular-nums text-gray-700 dark:text-slate-300">
                  {group.openCount}
                </td>
                <td className="px-4 py-3 text-right align-top tabular-nums text-gray-600 dark:text-slate-400">
                  {formatIsoDate(group.lastSeenUtc)}
                </td>
                <td className="px-4 py-3 text-right align-top tabular-nums text-gray-700 dark:text-slate-300">
                  {formatHoursLabel(group.totalResolutionHours)}
                </td>
                <td className="px-4 py-3 align-top">
                  <RepeatIssueTrendBadge
                    label={group.trendLabel}
                    delta={group.trendDelta}
                  />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function RepeatIssueAiReviewPanel({
  review,
  loading,
  error,
  onGenerate,
  disabled,
}: {
  review: RepeatIssueAiReviewResponse | null;
  loading: boolean;
  error: string | null;
  onGenerate: () => void;
  disabled: boolean;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50/60 p-4 dark:border-slate-800 dark:bg-slate-900/40">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h5 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
            Cortex Assist Review
          </h5>
          <p className="mt-0.5 text-[11px] leading-relaxed text-gray-500 dark:text-slate-500">
            Advisory summary of the recurring pattern. No action is taken.
          </p>
        </div>
        <button
          type="button"
          onClick={onGenerate}
          disabled={disabled || loading}
          className="ai-button ai-button--ready inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold text-cortex-blue-dark hover:bg-cortex-blue-soft focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 dark:text-emerald-300 dark:hover:bg-emerald-950/40"
        >
          {loading
            ? "Generating…"
            : review
              ? "Regenerate"
              : "Generate review"}
        </button>
      </div>

      {error ? (
        <p className="mt-3 text-xs text-red-700 dark:text-red-300">{error}</p>
      ) : null}

      {!loading && !error && !review ? (
        <p className="mt-3 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
          Generate a Cortex Assist review to summarize this recurring pattern,
          describe its operational impact, and propose next-step categories.
        </p>
      ) : null}

      {review?.unavailable ? (
        <p className="mt-3 text-xs leading-relaxed text-amber-800 dark:text-amber-300">
          {review.unavailableReason ??
            "Cortex Assist review is not ready right now."}
        </p>
      ) : null}

      {review && !review.unavailable ? (
        <div className="mt-3 space-y-3 text-sm">
          {review.summary ? (
            <p className="text-gray-900 dark:text-slate-100">
              {review.summary}
            </p>
          ) : null}

          <dl className="grid grid-cols-1 gap-2 md:grid-cols-2">
            {review.impact ? (
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                  Impact
                </dt>
                <dd className="mt-0.5 text-xs leading-relaxed text-gray-700 dark:text-slate-300">
                  {review.impact}
                </dd>
              </div>
            ) : null}
            {review.trendCommentary ? (
              <div>
                <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                  Trend
                </dt>
                <dd className="mt-0.5 text-xs leading-relaxed text-gray-700 dark:text-slate-300">
                  {review.trendCommentary}
                </dd>
              </div>
            ) : null}
          </dl>

          {review.commonCharacteristics.length > 0 ? (
            <div>
              <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                Shared characteristics
              </p>
              <ul className="mt-1 list-inside list-disc space-y-0.5 text-xs text-gray-700 dark:text-slate-300">
                {review.commonCharacteristics.map((item, index) => (
                  <li key={`char-${index}`}>{item}</li>
                ))}
              </ul>
            </div>
          ) : null}

          {review.suggestedNextSteps.length > 0 ? (
            <div>
              <p className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
                Suggested next steps
              </p>
              <ul className="mt-1 space-y-1.5">
                {review.suggestedNextSteps.map((step, index) => (
                  <li
                    key={`step-${index}`}
                    className="rounded-md border border-gray-200 bg-white px-2.5 py-1.5 text-xs dark:border-slate-800 dark:bg-slate-900"
                  >
                    <span className="mr-2 inline-flex items-center rounded-full bg-cortex-blue/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-cortex-blue dark:bg-cortex-blue/20">
                      {step.category}
                    </span>
                    <span className="text-gray-700 dark:text-slate-300">
                      {step.rationale}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function RepeatIssueGroupDetailPanel({
  detail,
  loading,
  error,
  review,
  reviewLoading,
  reviewError,
  onGenerateReview,
  onCreateRootCauseTask,
}: {
  detail: RepeatIssueGroupDetailResponse | null;
  loading: boolean;
  error: string | null;
  review: RepeatIssueAiReviewResponse | null;
  reviewLoading: boolean;
  reviewError: string | null;
  onGenerateReview: () => void;
  onCreateRootCauseTask: (draft: RootCauseTaskDraftInput) => void;
}) {
  if (loading) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-4 text-sm text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
        Loading recurring issue detail…
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-4 text-sm text-gray-600 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-300">
        {error}
      </div>
    );
  }

  if (!detail) {
    return null;
  }

  const summary = detail.summary;
  const insight = buildRecurringIssueInsight(summary);
  const rootCauseTaskDraft = buildRootCauseTaskDraft(detail, review);

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h4 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
            {summary.representativeTitle || "(untitled)"}
          </h4>
          <p className="mt-0.5 text-[11px] text-gray-500 dark:text-slate-500">
            {summary.boardName}
            {summary.signatureTokens.length > 0
              ? ` · signature: ${summary.signatureTokens.join(", ")}`
              : ""}
          </p>
        </div>
        <RepeatIssueTrendBadge
          label={summary.trendLabel}
          delta={summary.trendDelta}
        />
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-4">
        <div>
          <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
            First seen
          </dt>
          <dd className="mt-0.5 text-xs tabular-nums text-gray-700 dark:text-slate-300">
            {formatIsoDate(summary.firstSeenUtc)}
          </dd>
        </div>
        <div>
          <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
            Avg resolution time
          </dt>
          <dd className="mt-0.5 text-xs tabular-nums text-gray-700 dark:text-slate-300">
            {formatHoursLabel(summary.avgResolutionHours)}
          </dd>
        </div>
        <div>
          <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
            Touch count
          </dt>
          <dd className="mt-0.5 text-xs tabular-nums text-gray-700 dark:text-slate-300">
            {summary.operationalTouchCount} comments
          </dd>
        </div>
        <div>
          <dt className="text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-slate-400">
            Owners
          </dt>
          <dd className="mt-0.5 text-xs text-gray-700 dark:text-slate-300 line-clamp-2">
            {detail.owners.length === 0 ? "—" : detail.owners.join(", ")}
          </dd>
        </div>
      </dl>

      {detail.dominantPriority || detail.dominantStatus ? (
        <p className="mt-3 text-[11px] text-gray-500 dark:text-slate-500">
          {detail.dominantPriority
            ? `Dominant priority: ${detail.dominantPriority}`
            : null}
          {detail.dominantPriority && detail.dominantStatus ? " · " : null}
          {detail.dominantStatus
            ? `dominant status: ${detail.dominantStatus}`
            : null}
        </p>
      ) : null}

      <div className="mt-4 rounded-md border border-amber-200/70 bg-amber-50/60 px-3.5 py-3 dark:border-amber-500/20 dark:bg-amber-950/20">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-sm font-semibold leading-relaxed text-amber-950 dark:text-amber-200">
              {insight.headline}
            </p>
            <p className="mt-0.5 text-xs leading-relaxed text-amber-900/90 dark:text-amber-300/90">
              {insight.impact}
            </p>
          </div>
          <button
            type="button"
            onClick={() => onCreateRootCauseTask(rootCauseTaskDraft)}
            className="shrink-0 rounded-md bg-cortex-blue px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-cortex-blue-dark focus:outline-none focus-visible:ring-2 focus-visible:ring-cortex-blue focus-visible:ring-offset-2 dark:focus-visible:ring-offset-slate-900"
          >
            Create root cause task
          </button>
        </div>
      </div>

      <div className="mt-4 overflow-x-auto rounded-md border border-gray-200 dark:border-slate-800">
        <table className="min-w-full divide-y divide-gray-200 text-xs dark:divide-slate-800">
          <thead className="bg-gray-50 text-left font-semibold uppercase tracking-wider text-gray-500 dark:bg-slate-900/70 dark:text-slate-400">
            <tr>
              <th scope="col" className="px-3 py-2">
                Ticket
              </th>
              <th scope="col" className="px-3 py-2">
                Priority
              </th>
              <th scope="col" className="px-3 py-2">
                Status
              </th>
              <th scope="col" className="px-3 py-2">
                Created
              </th>
              <th scope="col" className="px-3 py-2 text-right">
                Resolution
              </th>
              <th scope="col" className="px-3 py-2 text-right">
                Comments
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 bg-white dark:divide-slate-800 dark:bg-slate-900">
            {detail.tickets.map((ticket) => (
              <tr key={ticket.ticketId}>
                <td className="px-3 py-2 align-top">
                  <p className="font-medium text-gray-900 dark:text-slate-100 line-clamp-1">
                    {ticket.title}
                  </p>
                  <p className="text-[10px] text-gray-500 dark:text-slate-500">
                    {formatTicketIdentifier(ticket.ticketId)}
                    {ticket.isArchived ? " · archived" : ""}
                  </p>
                </td>
                <td className="px-3 py-2 align-top text-gray-700 dark:text-slate-300">
                  {ticket.priority}
                </td>
                <td className="px-3 py-2 align-top text-gray-700 dark:text-slate-300">
                  {ticket.status}
                </td>
                <td className="px-3 py-2 align-top tabular-nums text-gray-600 dark:text-slate-400">
                  {formatIsoDate(ticket.createdDate)}
                </td>
                <td className="px-3 py-2 align-top text-right tabular-nums text-gray-700 dark:text-slate-300">
                  {formatHoursLabel(ticket.resolutionHours)}
                </td>
                <td className="px-3 py-2 align-top text-right tabular-nums text-gray-700 dark:text-slate-300">
                  {ticket.commentCount}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-4">
        <RepeatIssueAiReviewPanel
          review={review}
          loading={reviewLoading}
          error={reviewError}
          onGenerate={onGenerateReview}
          disabled={false}
        />
      </div>
    </div>
  );
}

function RepeatIssueIntelligenceSection({
  isActive,
  onCreateRootCauseTask,
}: {
  isActive: boolean;
  onCreateRootCauseTask: (draft: RootCauseTaskDraftInput) => void;
}) {
  const { getAccessTokenSilently } = useAuth0();
  const [overview, setOverview] = useState<RepeatIssueOverviewResponse | null>(
    null,
  );
  const [overviewLoading, setOverviewLoading] = useState(true);
  const [overviewError, setOverviewError] = useState<string | null>(null);

  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [detail, setDetail] = useState<RepeatIssueGroupDetailResponse | null>(
    null,
  );
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [review, setReview] = useState<RepeatIssueAiReviewResponse | null>(
    null,
  );
  const [reviewLoading, setReviewLoading] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const data = await repeatIssuesService.getOverview(token, 8);
        if (!cancelled) {
          setOverview(data);
          setOverviewError(null);
        }
      } catch {
        if (!cancelled) {
          setOverview(null);
          setOverviewError(
            "Unable to load recurring issue intelligence right now.",
          );
        }
      } finally {
        if (!cancelled) {
          setOverviewLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [getAccessTokenSilently]);

  useEffect(() => {
    if (!selectedKey) {
      setDetail(null);
      setDetailError(null);
      setReview(null);
      setReviewError(null);
      return undefined;
    }

    let cancelled = false;
    setDetailLoading(true);
    setDetailError(null);
    setReview(null);
    setReviewError(null);

    (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const data = await repeatIssuesService.getGroupDetail(
          selectedKey,
          token,
        );
        if (!cancelled) {
          setDetail(data);
        }
      } catch {
        if (!cancelled) {
          setDetail(null);
          setDetailError("Unable to load recurring issue detail.");
        }
      } finally {
        if (!cancelled) {
          setDetailLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [selectedKey, getAccessTokenSilently]);

  const handleGenerateReview = async () => {
    if (!selectedKey) {
      return;
    }

    setReviewLoading(true);
    setReviewError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const data = await repeatIssuesService.generateAiReview(
        selectedKey,
        token,
      );
      setReview(data);
    } catch {
      setReview(null);
      setReviewError(
        "Unable to generate Cortex Assist review right now. Try again shortly.",
      );
    } finally {
      setReviewLoading(false);
    }
  };

  const groups = overview?.groups ?? [];
  const hasGroups = groups.length > 0;

  if (!isActive) {
    return null;
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b border-gray-100 px-6 py-3.5 dark:border-slate-800">
        <div className="min-w-0">
          <h3 className="text-base font-semibold tracking-tight text-gray-900 dark:text-slate-100">
            Recurring Issue Intelligence
          </h3>
          <p className="mt-0.5 text-xs leading-relaxed text-gray-500 dark:text-slate-400">
            Spot repeating operational pain across tickets and surface advisory
            Cortex Assist reviews.
          </p>
        </div>
        <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-gray-600 dark:bg-slate-800 dark:text-slate-400">
          Advisory · v1
        </span>
      </div>

      <div className="space-y-5 px-6 py-4">
        {overviewLoading ? (
          <p className="text-sm text-gray-500 dark:text-slate-400">Loading…</p>
        ) : overviewError ? (
          <p className="text-sm text-gray-600 dark:text-slate-400">
            {overviewError}
          </p>
        ) : !overview || !hasGroups ? (
          <div className="rounded-md border border-dashed border-gray-200 bg-gray-50/60 px-4 py-5 text-center dark:border-slate-800 dark:bg-slate-900/40">
            <p className="text-sm font-medium text-gray-700 dark:text-slate-200">
              No recurring issue patterns detected yet.
            </p>
            <p className="mt-1 text-xs leading-relaxed text-gray-500 dark:text-slate-500">
              Groups appear once{" "}
              <span className="font-semibold">
                {overview?.minimumGroupSize ?? 3}
              </span>{" "}
              or more tickets share the same board and keyword signature.
            </p>
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
              <RepeatIssueSummaryChip
                label="Recurring groups"
                value={String(overview.totalRecurringGroups)}
                hint={`Min. ${overview.minimumGroupSize} tickets to qualify`}
                tooltip="Distinct issue patterns where at least N tickets share the same board and keyword signature."
              />
              <RepeatIssueSummaryChip
                label="Repeat tickets"
                value={String(overview.ticketsInRecurringGroups)}
                hint="Across all recurring groups"
                tooltip="Total tickets falling into any recurring group. This counts repeat volume, not unique issues."
              />
              <RepeatIssueSummaryChip
                label="Still open"
                value={String(overview.openTicketsInRecurringGroups)}
                hint="Open tickets tied to recurring issues"
                tooltip="Tickets in a recurring group that are not in a terminal status (Resolved, Closed, Done, etc.)."
              />
              <RepeatIssueSummaryChip
                label="Repeated effort"
                value={formatHoursLabel(
                  overview.totalResolutionHoursInRecurringGroups,
                )}
                hint="Total lifecycle time"
                tooltip="Sum of lifecycle durations (created → closed) across closed tickets in recurring groups. Proxy for repeated operational effort — not human work hours."
              />
            </div>

            <RepeatIssueRankTable
              groups={groups}
              selectedKey={selectedKey}
              onSelect={(key) =>
                setSelectedKey((current) => (current === key ? null : key))
              }
            />

            {selectedKey ? (
              <RepeatIssueGroupDetailPanel
                detail={detail}
                loading={detailLoading}
                error={detailError}
                review={review}
                reviewLoading={reviewLoading}
                reviewError={reviewError}
                onGenerateReview={handleGenerateReview}
                onCreateRootCauseTask={onCreateRootCauseTask}
              />
            ) : (
              <p className="text-[11px] text-gray-500 dark:text-slate-500">
                Select a row to see contributing tickets and request a Cortex
                Assist review.
              </p>
            )}
          </>
        )}
      </div>
    </section>
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
  onCreateRootCauseTask,
}: ReportsPageProps) {
  const [isExportMenuOpen, setIsExportMenuOpen] = useState(false);
  const exportMenuRef = useRef<HTMLDivElement | null>(null);
  const reportsScrollRef = useRef<HTMLDivElement | null>(null);
  const { getAccessTokenSilently } = useAuth0();
  const [workflowMetrics, setWorkflowMetrics] =
    useState<WorkflowMetricsSnapshot | null>(null);
  const [workflowMetricsError, setWorkflowMetricsError] = useState<
    string | null
  >(null);
  const [workflowMetricsLoading, setWorkflowMetricsLoading] = useState(true);

  const [customReportRowFilter, setCustomReportRowFilter] = useState("");
  const [customReportColumnFilters, setCustomReportColumnFilters] = useState<
    Record<string, string>
  >({});

  useEffect(() => {
    setCustomReportRowFilter("");
    setCustomReportColumnFilters({});
  }, [selectedCustomReportId, customReportResult?.generatedDateUtc]);

  const customReportColumnDistincts = useMemo(() => {
    if (!customReportResult) {
      return {};
    }
    return computeColumnDistincts(
      customReportResult.columns,
      customReportResult.rows,
    );
  }, [customReportResult]);

  const hasActiveCustomReportFilters = useMemo(
    () =>
      hasAnyCustomReportFilter(
        customReportRowFilter,
        customReportColumnFilters,
      ),
    [customReportRowFilter, customReportColumnFilters],
  );

  const filteredCustomReportRows = useMemo(() => {
    if (!customReportResult) {
      return [];
    }
    return customReportResult.rows.filter((row) =>
      rowMatchesCustomReportFilters(
        row,
        customReportResult.columns,
        customReportColumnFilters,
        customReportRowFilter,
      ),
    );
  }, [
    customReportResult,
    customReportColumnFilters,
    customReportRowFilter,
  ]);

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
    return (
      <ScrollableViewport
        viewportRef={reportsScrollRef}
        outerClassName="h-full"
        viewportClassName="scroll-chain-auto h-full overflow-y-auto"
        affordanceAriaLabel="Scroll reports workspace to bottom"
      >
        <ReportsSkeleton />
      </ScrollableViewport>
    );
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
      (ticket) =>
        ticket.slaStatus === "At Risk" || ticket.slaStatus === "Breached",
    ),
  );
  const resolvedLateTickets = sortByUrgency(
    tickets.filter((ticket) => ticket.slaStatus === "Resolved Late"),
  );

  const openTickets = tickets.filter(isOpenTicket);
  const executiveSummary = buildSlaExecutiveSummary(openTickets);
  const assignedOpenCount = openTickets.filter(
    (ticket) => getOwnerLabel(ticket) !== "—",
  ).length;
  const ownershipClarityLabel = formatWorkflowPercent(
    assignedOpenCount,
    openTickets.length,
  );
  const workloadBalanceInsight = buildWorkloadBalanceInsight(openTickets);

  return (
    <ScrollableViewport
      viewportRef={reportsScrollRef}
      outerClassName="h-full"
      viewportClassName="scroll-chain-auto h-full overflow-y-auto"
      affordanceAriaLabel="Scroll reports workspace to bottom"
    >
        <div className="space-y-6 pb-6">
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

            <button
              onClick={() => onChangeSection("telemetry")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                activeSection === "telemetry"
                  ? "bg-cortex-blue text-white"
                  : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              }`}
            >
              Workflow Insights
            </button>

            <button
              onClick={() => onChangeSection("recurring-issues")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                activeSection === "recurring-issues"
                  ? "bg-cortex-blue text-white"
                  : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              }`}
            >
              Recurring Issues
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
                      activeSection === "custom" &&
                      selectedCustomReportId === report.id
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

      <RepeatIssueIntelligenceSection
        isActive={activeSection === "recurring-issues"}
        onCreateRootCauseTask={onCreateRootCauseTask}
      />

      {activeSection === "sla" ? (
        <>
          {showSlaLegend && <SlaLegend />}

          {error ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">{error}</p>
            </div>
          ) : totalTickets === 0 ? (
            <section className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
              No workflow insights yet. Create or approve tickets to show intake,
              ownership, and SLA health.
            </section>
          ) : (
            <div className="space-y-6">
              <section
                className={executiveSummaryAccentClass(executiveSummary.tone)}
                aria-labelledby="sla-executive-summary-heading"
              >
                <h3 id="sla-executive-summary-heading" className="sr-only">
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
                    Ownership Clarity
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
                    {ownershipClarityLabel}
                  </p>
                  <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                    Open tickets with a named owner; clear ownership reduces follow-up.
                  </p>
                </div>

                <div className="rounded-lg border border-green-200 bg-green-50 p-5 dark:border-green-900/40 dark:bg-green-950/20">
                  <p className="text-sm font-medium text-green-700 dark:text-green-300">
                    SLA Health
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-green-900 dark:text-green-100">
                    {inSlaCount}
                  </p>
                  <p className="mt-2 text-sm text-green-700/80 dark:text-green-300/80">
                    Tickets on track or resolved within SLA.
                  </p>
                </div>

                <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-5 dark:border-yellow-900/40 dark:bg-yellow-950/20">
                  <p className="text-sm font-medium text-yellow-800 dark:text-yellow-300">
                    SLA Risk
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-yellow-900 dark:text-yellow-100">
                    {atRiskCount + outsideSlaCount}
                  </p>
                  <p className="mt-2 text-sm text-yellow-800/80 dark:text-yellow-300/80">
                    {atRiskCount} at risk and {outsideSlaCount} outside SLA; act before delayed work compounds.
                  </p>
                </div>

                <div
                  className={
                    workloadBalanceInsight.tone === "warning"
                      ? "rounded-lg border border-amber-200 bg-amber-50 p-5 dark:border-amber-900/40 dark:bg-amber-950/20"
                      : "rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"
                  }
                >
                  <p
                    className={
                      workloadBalanceInsight.tone === "warning"
                        ? "text-sm font-medium text-amber-800 dark:text-amber-300"
                        : "text-sm font-medium text-gray-500 dark:text-slate-400"
                    }
                  >
                    Workload Balance
                  </p>
                  <p
                    className={
                      workloadBalanceInsight.tone === "warning"
                        ? "mt-3 text-3xl font-semibold text-amber-900 dark:text-amber-100"
                        : "mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100"
                    }
                  >
                    {workloadBalanceInsight.value}
                  </p>
                  <p
                    className={
                      workloadBalanceInsight.tone === "warning"
                        ? "mt-2 text-sm text-amber-800/80 dark:text-amber-300/80"
                        : "mt-2 text-sm text-gray-500 dark:text-slate-400"
                    }
                  >
                    {workloadBalanceInsight.hint}
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
                            {formatPercentage(
                              statusCounts[status],
                              totalTickets,
                            )}
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
      ) : activeSection === "telemetry" ? (
        <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
          <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b border-gray-100 px-6 py-3.5 dark:border-slate-800">
            <div className="min-w-0">
              <h3 className="text-base font-semibold tracking-tight text-gray-900 dark:text-slate-100">
                Workflow Metrics
              </h3>
              <p className="mt-0.5 text-xs leading-relaxed text-gray-500 dark:text-slate-400">
                See whether Cortex is reducing follow-up, improving readiness,
                and lowering operational risk.
              </p>
            </div>
            <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wider text-gray-600 dark:bg-slate-800 dark:text-slate-400">
              All-time · v1
            </span>
          </div>
          <div className="px-6 py-4">
            {workflowMetricsLoading ? (
              <p className="text-sm text-gray-500 dark:text-slate-400">
                Loading…
              </p>
            ) : workflowMetricsError ? (
              <p className="text-sm text-gray-600 dark:text-slate-400">
                {workflowMetricsError}
              </p>
            ) : workflowMetrics ? (
              <TelemetryOverviewContent data={workflowMetrics} />
            ) : (
              <TelemetryEmptyState />
            )}
          </div>
        </section>
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
                customReports.find(
                  (report) => report.id === selectedCustomReportId,
                )?.name ??
                "Custom Report"}
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Custom SQL report registered in Configuration.
            </p>
          </div>

          {customReportError ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">
                {customReportError}
              </p>
            </div>
          ) : !customReportResult ? (
            <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
              Select a custom report to run it.
            </div>
          ) : (
            <div className="space-y-4 px-6 py-6">
              <div className="flex flex-col gap-3 lg:flex-row lg:flex-wrap lg:items-center lg:justify-between lg:gap-x-4">
                <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1 text-sm text-gray-500 dark:text-slate-400">
                  <span className="shrink-0">
                    Generated{" "}
                    {formatDisplayDateTime(customReportResult.generatedDateUtc)}
                  </span>
                  <span className="font-medium text-gray-700 dark:text-slate-300">
                    {!hasActiveCustomReportFilters
                      ? customReportResult.rows.length === 1
                        ? "1 row"
                        : `${customReportResult.rows.length} rows`
                      : `Showing ${filteredCustomReportRows.length} of ${customReportResult.rows.length} rows`}
                  </span>
                </div>
                <div className="flex w-full flex-col gap-2 sm:flex-row sm:items-center sm:justify-end lg:w-auto">
                  {hasActiveCustomReportFilters ? (
                    <button
                      type="button"
                      onClick={() => {
                        setCustomReportRowFilter("");
                        setCustomReportColumnFilters({});
                      }}
                      className="shrink-0 rounded-md border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                    >
                      Clear filters
                    </button>
                  ) : null}
                  <div className="flex min-w-0 flex-1 flex-col gap-1 sm:max-w-md">
                    <label
                      className="sr-only"
                      htmlFor="custom-report-row-filter"
                    >
                      Search across all columns
                    </label>
                    <input
                      id="custom-report-row-filter"
                      type="search"
                      value={customReportRowFilter}
                      onChange={(e) => setCustomReportRowFilter(e.target.value)}
                      placeholder="Search all columns..."
                      autoComplete="off"
                      className="w-full min-w-0 rounded-md border border-gray-200 bg-white px-3 py-2 text-sm text-gray-900 placeholder:text-gray-400 focus:border-cortex-blue focus:outline-none focus:ring-1 focus:ring-cortex-blue dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500 dark:focus:border-cortex-cyan dark:focus:ring-cortex-cyan"
                    />
                  </div>
                </div>
              </div>
              {customReportResult.isTruncated && (
                <p className="text-sm text-amber-700 dark:text-amber-300">
                  Showing the first 500 rows for performance.
                </p>
              )}

              {customReportResult.rows.length === 0 ? (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-400">
                  This report returned no rows.
                </div>
              ) : filteredCustomReportRows.length === 0 ? (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-6 py-12 text-center text-gray-600 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-300">
                  No rows match the current filters.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full text-sm">
                    <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                      <tr>
                        {customReportResult.columns.map((column) => (
                          <th
                            key={column}
                            className="min-w-[9rem] px-3 py-3 font-medium"
                          >
                            {column}
                          </th>
                        ))}
                      </tr>
                      <tr className="border-t border-gray-200 dark:border-slate-700">
                        {customReportResult.columns.map((column) => {
                          const kind =
                            getCustomReportColumnFilterKind(column);
                          const value =
                            customReportColumnFilters[column] ?? "";
                          const distinct = customReportColumnDistincts[column] ?? {
                            values: [],
                            hasBlank: false,
                          };

                          if (kind === "select" || kind === "owner") {
                            return (
                              <th
                                key={`filter-${column}`}
                                className="min-w-[9rem] px-2 py-2 align-top font-normal"
                              >
                                <label className="sr-only" htmlFor={`crf-${column}`}>
                                  Filter by {column}
                                </label>
                                <select
                                  id={`crf-${column}`}
                                  value={value}
                                  onChange={(e) =>
                                    setCustomReportColumnFilters((prev) => ({
                                      ...prev,
                                      [column]: e.target.value,
                                    }))
                                  }
                                  className="w-full max-w-[14rem] rounded border border-gray-200 bg-white px-2 py-1.5 text-xs text-gray-900 focus:border-cortex-blue focus:outline-none focus:ring-1 focus:ring-cortex-blue dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100 dark:focus:border-cortex-cyan dark:focus:ring-cortex-cyan"
                                >
                                  <option value="">All</option>
                                  {distinct.hasBlank ? (
                                    <option value={UNASSIGNED_FILTER}>
                                      (Unassigned)
                                    </option>
                                  ) : null}
                                  {distinct.values.map((opt) => (
                                    <option key={opt} value={opt}>
                                      {opt}
                                    </option>
                                  ))}
                                </select>
                              </th>
                            );
                          }

                          const placeholder =
                            column.length > 22
                              ? `Filter ${column.slice(0, 18)}...`
                              : `Filter ${column}...`;

                          return (
                            <th
                              key={`filter-${column}`}
                              className="min-w-[9rem] px-2 py-2 align-top font-normal"
                            >
                              <label className="sr-only" htmlFor={`crf-${column}`}>
                                {kind === "date" ? "Text filter for" : "Contains filter for"}{" "}
                                {column}
                              </label>
                              <input
                                id={`crf-${column}`}
                                type="search"
                                value={value}
                                onChange={(e) =>
                                  setCustomReportColumnFilters((prev) => ({
                                    ...prev,
                                    [column]: e.target.value,
                                  }))
                                }
                                placeholder={placeholder}
                                title={`Filter ${column} (${kind === "date" ? "text" : "contains"})`}
                                autoComplete="off"
                                className="w-full max-w-[14rem] rounded border border-gray-200 bg-white px-2 py-1.5 text-xs text-gray-900 placeholder:text-gray-400 focus:border-cortex-blue focus:outline-none focus:ring-1 focus:ring-cortex-blue dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500 dark:focus:border-cortex-cyan dark:focus:ring-cortex-cyan"
                              />
                            </th>
                          );
                        })}
                      </tr>
                    </thead>
                    <tbody>
                      {filteredCustomReportRows.map((row, rowIndex) => (
                        <tr
                          key={rowIndex}
                          className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                        >
                          {customReportResult.columns.map((column) => (
                            <td
                              key={`${rowIndex}-${column}`}
                              className="px-3 py-3 align-top"
                            >
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
    </ScrollableViewport>
  );
}
