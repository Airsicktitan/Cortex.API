import type { ApprovalTriagePreview, Ticket } from "../../types/ticket";
import {
  shouldShowApprovalTriageModalPanel,
  triageHasContent,
} from "../../utils/approvalTriage";

function priorityBadgeClass(priorityRaw: string): string {
  const p = priorityRaw.trim().toLowerCase();
  const base =
    "inline-flex items-center rounded-md border px-2.5 py-0.5 text-sm font-semibold tabular-nums";
  if (p === "low") {
    return `${base} border-emerald-300/80 bg-emerald-50 text-emerald-900 dark:border-emerald-700/80 dark:bg-emerald-950/50 dark:text-emerald-100`;
  }
  if (p === "medium") {
    return `${base} border-amber-300/80 bg-amber-50 text-amber-950 dark:border-amber-700/80 dark:bg-amber-950/40 dark:text-amber-100`;
  }
  if (p === "high" || p === "critical") {
    return `${base} border-red-300/80 bg-red-50 text-red-900 dark:border-red-800/80 dark:bg-red-950/50 dark:text-red-100`;
  }
  return `${base} border-gray-300/80 bg-gray-100 text-gray-800 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200`;
}

type ApprovalTriagePanelProps = {
  triage: ApprovalTriagePreview | null | undefined;
  loading?: boolean;
  unavailable?: boolean;
  unavailableMessage?: string | null;
  /** Legacy: compact in-card queue styling (unused when `presentation` is set). */
  embedded?: boolean;
  /** Visual mode: standalone dashed panel, embedded text, or modal right rail. */
  presentation?: "standalone" | "embedded" | "modalColumn";
};

/**
 * AI-assisted triage content. Use `presentation="modalColumn"` for the reviewer modal rail.
 */
export function ApprovalTriagePanel({
  triage,
  loading = false,
  unavailable = false,
  unavailableMessage,
  embedded = false,
  presentation: presentationProp,
}: ApprovalTriagePanelProps) {
  const presentation =
    presentationProp ?? (embedded ? "embedded" : "standalone");
  const hasContent = triageHasContent(triage);

  const isEmbedded = presentation === "embedded";
  const isModalColumn = presentation === "modalColumn";
  const isStandalone = presentation === "standalone";

  const titleSize = isModalColumn
    ? "text-xs"
    : isEmbedded
      ? "text-[10px]"
      : "text-xs";
  const bodyClass = isModalColumn
    ? "text-sm leading-relaxed text-gray-800 dark:text-slate-200"
    : isEmbedded
      ? "text-xs leading-relaxed text-gray-800 dark:text-slate-200"
      : "text-sm leading-relaxed text-gray-800 dark:text-slate-200";
  const mutedClass = isModalColumn
    ? "text-xs leading-relaxed text-gray-500 dark:text-slate-400"
    : isEmbedded
      ? "text-[10px] leading-relaxed text-gray-500 dark:text-slate-400"
      : "text-xs leading-relaxed text-gray-500 dark:text-slate-400";
  const sectionLabelClass = isModalColumn
    ? "text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400"
    : isEmbedded
      ? "text-[10px] font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400"
      : "text-xs font-semibold tracking-wide text-gray-700 dark:text-slate-300";
  const dlGap = isModalColumn ? "space-y-4" : isEmbedded ? "space-y-3" : "space-y-5";

  const outerClass = isStandalone
    ? "rounded-lg border border-dashed border-gray-200 bg-gray-50/60 p-3 dark:border-slate-700 dark:bg-slate-900/30"
    : isEmbedded
      ? "text-left"
      : "min-w-0 text-left";

  const headerRow = isModalColumn ? (
    <div className="flex shrink-0 items-start justify-between gap-2">
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
        AI Triage
      </p>
      <span className="text-xs font-medium text-gray-500 dark:text-slate-400">
        Advisory
      </span>
    </div>
  ) : isEmbedded ? (
    <div className="flex shrink-0 items-baseline justify-between gap-2">
      <p
        className={`font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400 ${titleSize}`}
      >
        AI triage
      </p>
      <span className={`font-normal ${mutedClass}`}>Advisory</span>
    </div>
  ) : (
    <div className="flex shrink-0 items-start justify-between gap-2">
      <p
        className={`font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300 ${titleSize}`}
      >
        AI Triage
      </p>
      <span className="font-medium text-[10px] text-gray-500 dark:text-slate-400">
        Advisory
      </span>
    </div>
  );

  return (
    <div className={outerClass}>
      {headerRow}
      <p className={`${isModalColumn ? "mt-2" : isEmbedded ? "mt-1" : "mt-1.5"} shrink-0 ${mutedClass}`}>
        Suggestions are advisory. You decide how to review and route tickets.
      </p>
      {loading ? (
        <p className={`mt-4 ${bodyClass} text-gray-600 dark:text-slate-300`}>
          Analyzing request…
        </p>
      ) : unavailable ? (
        <p className={`mt-4 ${bodyClass} text-gray-600 dark:text-slate-400`}>
          {unavailableMessage?.trim() ||
            "AI triage is not available. You can still review using the ticket details."}
        </p>
      ) : hasContent ? (
        <dl className={`mt-4 ${dlGap} ${bodyClass}`}>
          {triage?.summary?.trim() ? (
            <div className="space-y-1.5">
              <dt className={sectionLabelClass}>Summary</dt>
              <dd className="whitespace-pre-wrap">{triage.summary.trim()}</dd>
            </div>
          ) : null}
          {triage?.suggestedPriority?.trim() ? (
            <div className="space-y-2">
              <dt className={sectionLabelClass}>Suggested priority</dt>
              <dd>
                <span
                  className={
                    isEmbedded || isModalColumn
                      ? `${priorityBadgeClass(triage.suggestedPriority)} text-xs`
                      : priorityBadgeClass(triage.suggestedPriority)
                  }
                >
                  {triage.suggestedPriority.trim()}
                </span>
              </dd>
            </div>
          ) : null}
          {triage?.priorityReason?.trim() ? (
            <div className="space-y-1.5">
              <dt className={sectionLabelClass}>Priority reasoning</dt>
              <dd className="whitespace-pre-wrap">{triage.priorityReason.trim()}</dd>
            </div>
          ) : null}
          {triage?.suggestedStatus?.trim() ? (
            <div className="space-y-1.5">
              <dt className={sectionLabelClass}>Suggested status</dt>
              <dd className="whitespace-pre-wrap">{triage.suggestedStatus.trim()}</dd>
            </div>
          ) : null}
          {triage?.potentialSlaRisk?.trim() || triage?.slaRiskReason?.trim() ? (
            <div className="space-y-2">
              <dt className={sectionLabelClass}>Potential SLA risk</dt>
              <dd className="space-y-1.5">
                {triage?.potentialSlaRisk?.trim() ? (
                  <span
                    className={
                      isEmbedded || isModalColumn
                        ? `${priorityBadgeClass(triage.potentialSlaRisk)} text-xs`
                        : priorityBadgeClass(triage.potentialSlaRisk)
                    }
                  >
                    {triage.potentialSlaRisk.trim()}
                  </span>
                ) : null}
                {triage?.slaRiskReason?.trim() ? (
                  <p
                    className={
                      isModalColumn
                        ? "text-sm leading-relaxed text-gray-800 dark:text-slate-200"
                        : bodyClass
                    }
                  >
                    {triage.slaRiskReason.trim()}
                  </p>
                ) : null}
              </dd>
            </div>
          ) : null}
          {triage?.missingDetailHints && triage.missingDetailHints.length > 0 ? (
            <div className="space-y-1.5">
              <dt className={sectionLabelClass}>Missing details</dt>
              <dd>
                <ul className="list-outside list-disc space-y-2 pl-5">
                  {triage.missingDetailHints.map((hint, index) => (
                    <li key={index} className="leading-relaxed">
                      {hint}
                    </li>
                  ))}
                </ul>
              </dd>
            </div>
          ) : null}
        </dl>
      ) : (
        <p className={`mt-4 ${bodyClass} text-gray-500 dark:text-slate-500`}>
          No analysis yet.
        </p>
      )}
    </div>
  );
}

/**
 * Right-hand AI triage rail for the reviewer ticket modal (~1/3 width). Matches intake review layout:
 * bordered column, Regenerate on top, scrollable advisory content.
 */
export function ApprovalTriageModalColumn({
  ticket,
  onRegenerateAnalysis,
  regenerateLoading = false,
}: {
  ticket: Ticket;
  onRegenerateAnalysis?: () => void | Promise<void>;
  regenerateLoading?: boolean;
}) {
  if (!shouldShowApprovalTriageModalPanel(ticket)) {
    return null;
  }

  const canRegenerate = Boolean(onRegenerateAnalysis);

  return (
    <aside
      className="flex min-h-0 min-w-0 flex-col border-t border-gray-200 pt-4 dark:border-slate-800 lg:min-h-0 lg:flex-1 lg:border-l lg:border-t-0 lg:pl-5 lg:pt-0"
      aria-label="AI triage"
    >
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-lg border border-gray-200 bg-gray-50/90 shadow-sm dark:border-slate-700 dark:bg-slate-900/45">
        {canRegenerate ? (
          <div className="flex shrink-0 justify-end border-b border-gray-100 px-3 py-2.5 dark:border-slate-800">
            <button
              type="button"
              onClick={() => void onRegenerateAnalysis?.()}
              disabled={regenerateLoading}
              className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-xs font-medium text-gray-800 shadow-sm transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700"
            >
              {regenerateLoading ? "Regenerating…" : "Regenerate Analysis"}
            </button>
          </div>
        ) : null}
        <div
          className={`min-h-0 flex-1 overflow-y-auto px-3 py-3 sm:px-4 sm:py-4 ${canRegenerate ? "" : "pt-4"}`}
        >
          <ApprovalTriagePanel
            triage={ticket.approvalTriagePreview}
            presentation="modalColumn"
          />
        </div>
      </div>
    </aside>
  );
}
