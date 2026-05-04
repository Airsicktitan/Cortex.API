import type { ReactNode } from "react";
import type { IntakeLearningOverview } from "../../types/intakeLearning";
import { formatDisplayDateTime } from "../../utils/presentation";

function formatPct(value: number): string {
  if (!Number.isFinite(value)) {
    return "0%";
  }
  return `${value.toFixed(1)}%`;
}

function formatAvg(value: number): string {
  if (!Number.isFinite(value)) {
    return "—";
  }
  return value.toFixed(2);
}

type ReportTableProps = {
  title: string;
  description?: string;
  columns: readonly { header: string; className?: string }[];
  rows: ReactNode[][];
  emptyHint?: string;
};

function ReportTable({
  title,
  description,
  columns,
  rows,
  emptyHint,
}: ReportTableProps) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">{title}</h3>
        {description ? (
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">{description}</p>
        ) : null}
      </div>
      {rows.length === 0 ? (
        <p className="px-6 py-8 text-center text-sm text-gray-500 dark:text-slate-400">
          {emptyHint ?? "No rows yet."}
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
              <tr>
                {columns.map((col) => (
                  <th key={col.header} className={`px-4 py-3 font-medium ${col.className ?? ""}`}>
                    {col.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((cells, ri) => (
                <tr
                  key={`r-${ri}`}
                  className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                >
                  {cells.map((cell, ci) => (
                    <td key={`${ri}-${ci}`} className="px-4 py-3 align-top tabular-nums">
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export function IntakeLearningReportSection({
  loading,
  error,
  overview,
  onRefresh,
}: {
  loading: boolean;
  error: string | null;
  overview: IntakeLearningOverview | null;
  onRefresh: () => void;
}) {
  if (loading) {
    return (
      <p className="text-sm text-gray-500 dark:text-slate-400">Loading intake learning insights…</p>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50/80 px-4 py-4 text-sm text-red-800 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">
        <p>{error}</p>
        <button
          type="button"
          onClick={onRefresh}
          className="mt-3 rounded-md bg-white px-3 py-2 text-sm font-medium text-red-900 shadow-sm ring-1 ring-red-200 transition-colors hover:bg-red-50 dark:bg-slate-900 dark:text-red-100 dark:ring-red-800 dark:hover:bg-slate-800"
        >
          Try again
        </button>
      </div>
    );
  }

  if (!overview) {
    return null;
  }

  const tablesEmpty =
    overview.boardReturns.length === 0 &&
    overview.priorityReturns.length === 0 &&
    overview.departmentReturns.length === 0;

  return (
    <div className="space-y-6">
      <div className="rounded-lg border border-sky-200/70 bg-sky-50/60 px-4 py-3 dark:border-sky-900/40 dark:bg-sky-950/30">
        <p className="text-sm leading-relaxed text-sky-950 dark:text-sky-50">
          These insights show <span className="font-medium">correlation</span> and{" "}
          <span className="font-medium">follow-up friction</span> patterns—they do not prove that a specific
          missing-detail hint caused a return. AI hints are advisory triage snapshots, not authoritative truth.
        </p>
        <p className="mt-2 text-[11px] leading-snug text-sky-900/85 dark:text-sky-200/90">
          These insights are based on available ticket outcomes and current triage snapshots. Return reasons may be
          cleared after resubmission, and missing-detail hints reflect the latest persisted triage snapshot.
        </p>
      </div>

      {tablesEmpty ? (
        <section className="rounded-lg border border-gray-200 bg-gray-50/80 px-6 py-12 text-center dark:border-slate-800 dark:bg-slate-950/40">
          <p className="text-sm font-medium text-gray-700 dark:text-slate-300">
            No intake learning data yet.
          </p>
          <p className="mx-auto mt-2 max-w-md text-sm text-gray-600 dark:text-slate-400">
            Cortex will show follow-up friction patterns after tickets have recorded outcomes.
          </p>
        </section>
      ) : (
        <>
          <ReportTable
            title="Return rate by board"
            description="Share of lifecycle-tracked tickets (with outcomes) whose outcome flags return for detail. Correlation only."
            columns={[
              { header: "Board" },
              { header: "Tickets", className: "text-right" },
              { header: "Returned", className: "text-right" },
              { header: "Return rate", className: "text-right" },
            ]}
            rows={overview.boardReturns.map((row) => [
              row.label,
              row.totalTickets,
              row.returnedTickets,
              formatPct(row.returnRatePercent),
            ])}
            emptyHint="No board-level cohort rows yet."
          />

          <ReportTable
            title="Return rate by priority"
            description="Current ticket priority value at reporting time—not necessarily priority at intake."
            columns={[
              { header: "Priority" },
              { header: "Tickets", className: "text-right" },
              { header: "Returned", className: "text-right" },
              { header: "Return rate", className: "text-right" },
            ]}
            rows={overview.priorityReturns.map((row) => [
              row.label,
              row.totalTickets,
              row.returnedTickets,
              formatPct(row.returnRatePercent),
            ])}
          />

          <ReportTable
            title="Return rate by requester department"
            description={`Requester department from current user profiles. Missing values appear as “Unknown” (${
              overview.unknownDepartmentTicketCount
            } ticket${overview.unknownDepartmentTicketCount === 1 ? "" : "s"} in cohort without a department).`}
            columns={[
              { header: "Department" },
              { header: "Tickets", className: "text-right" },
              { header: "Returned", className: "text-right" },
              { header: "Return rate", className: "text-right" },
            ]}
            rows={overview.departmentReturns.map((row) => [
              row.label,
              row.totalTickets,
              row.returnedTickets,
              formatPct(row.returnRatePercent),
            ])}
          />
        </>
      )}

      <section className="grid gap-4 md:grid-cols-2">
        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <h3 className="text-base font-semibold text-gray-900 dark:text-slate-100">
            Return reasons still available
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Reasons can be cleared after requester resubmission or approval, so this reflects currently available context.
          </p>
          <dl className="mt-4 space-y-2 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">Returned tickets</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {overview.returnReasonAvailability.returnedTickets}
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">Reason text still stored</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {overview.returnReasonAvailability.returnReasonStillAvailableCount}
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">Availability</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {formatPct(overview.returnReasonAvailability.returnReasonAvailabilityPercent)}
              </dd>
            </div>
          </dl>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <h3 className="text-base font-semibold text-gray-900 dark:text-slate-100">
            Missing-detail hint pressure
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Based on the latest persisted triage snapshot, not guaranteed return-time state.
          </p>
          <dl className="mt-4 space-y-2 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">Returned tickets</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {overview.missingHintSummary.returnedTickets}
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">With persisted hint bullets</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {overview.missingHintSummary.returnedTicketsWithMissingHintJson}
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-gray-500 dark:text-slate-400">Average bullet count</dt>
              <dd className="font-medium tabular-nums text-gray-900 dark:text-slate-100">
                {formatAvg(overview.missingHintSummary.averageMissingHintCount)}
              </dd>
            </div>
          </dl>
          <div className="mt-4 border-t border-gray-100 pt-3 dark:border-slate-800">
            <p className="text-[11px] font-medium uppercase tracking-wide text-gray-500 dark:text-slate-500">
              Buckets (returned tickets)
            </p>
            <ul className="mt-2 space-y-1.5 text-sm text-gray-700 dark:text-slate-300">
              <li className="flex justify-between gap-4">
                <span>0 hints</span>
                <span className="tabular-nums">{overview.missingHintSummary.zeroHintsCount}</span>
              </li>
              <li className="flex justify-between gap-4">
                <span>1–2 hints</span>
                <span className="tabular-nums">{overview.missingHintSummary.oneToTwoHintsCount}</span>
              </li>
              <li className="flex justify-between gap-4">
                <span>3–5 hints</span>
                <span className="tabular-nums">{overview.missingHintSummary.threeToFiveHintsCount}</span>
              </li>
              <li className="flex justify-between gap-4">
                <span>6+ hints</span>
                <span className="tabular-nums">{overview.missingHintSummary.sixPlusHintsCount}</span>
              </li>
            </ul>
          </div>
        </div>
      </section>

      <div className="rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-4 dark:border-slate-800 dark:bg-slate-950/40">
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-500">
          Data caveats ({overview.limitations.length})
        </p>
        <ul className="mt-2 list-disc space-y-1.5 pl-5 text-xs leading-relaxed text-gray-700 dark:text-slate-300">
          {overview.limitations.map((line) => (
            <li key={line}>{line}</li>
          ))}
        </ul>
      </div>

      <p className="text-[11px] text-gray-500 dark:text-slate-500">
        Generated {formatDisplayDateTime(overview.generatedAtUtc)} · Read-only aggregates
      </p>
    </div>
  );
}
