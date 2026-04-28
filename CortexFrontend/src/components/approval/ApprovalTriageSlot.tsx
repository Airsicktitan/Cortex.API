import { useRef } from "react";
import {
  type ScreenshotInsightPersisted,
  screenshotInsightPersistedHasContent,
} from "../../types/screenshotInsight";
import type { ApprovalTriagePreview, Ticket } from "../../types/ticket";
import {
  triageHasContent,
} from "../../utils/approvalTriage";
import { filterScreenshotInsightNoise } from "../../utils/screenshotInsightDisplay";
import { getTriageClarityIndicator } from "../../utils/triageClarity";
import { ScrollableViewport } from "../ui/ScrollableViewport";

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
  /** For clarity pill heuristics — pass ticket fields when available. */
  ticketTitle?: string;
  ticketDescription?: string;
};

type TriageApplyAction = "priority" | "status" | "both";

type ApprovalTriageApplyControls = {
  hasSuggestedPriority: boolean;
  hasSuggestedStatus: boolean;
  canApplyPriority: boolean;
  canApplyStatus: boolean;
  canApplyBoth: boolean;
  pendingAction: TriageApplyAction | null;
  errorMessage?: string | null;
  onApplyPriority: () => void | Promise<void>;
  onApplyStatus: () => void | Promise<void>;
  onApplyBoth: () => void | Promise<void>;
};

function getApplyHelperText(
  controls: ApprovalTriageApplyControls,
): string | null {
  if (!controls.hasSuggestedPriority && !controls.hasSuggestedStatus) {
    return null;
  }

  if (controls.canApplyPriority && controls.canApplyStatus) {
    return "Apply a saved suggestion to update canonical workflow fields.";
  }

  if (!controls.hasSuggestedPriority && controls.hasSuggestedStatus) {
    return controls.canApplyStatus
      ? "Only the saved status suggestion can be applied."
      : "The saved status suggestion is already reflected on this ticket.";
  }

  if (controls.hasSuggestedPriority && !controls.hasSuggestedStatus) {
    return controls.canApplyPriority
      ? "Only the saved priority suggestion can be applied."
      : "The saved priority suggestion is already reflected on this ticket.";
  }

  if (!controls.canApplyPriority && !controls.canApplyStatus) {
    return "The saved suggestions are already reflected on this ticket.";
  }

  if (!controls.canApplyPriority) {
    return "Priority already matches the saved suggestion.";
  }

  if (!controls.canApplyStatus) {
    return "Status already matches the saved suggestion.";
  }

  return null;
}

/**
 * Intake insight (advisory) content. Use `presentation="modalColumn"` for the reviewer modal rail.
 */
export function ApprovalTriagePanel({
  triage,
  loading = false,
  unavailable = false,
  unavailableMessage,
  embedded = false,
  presentation: presentationProp,
  ticketTitle = "",
  ticketDescription = "",
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
        Intake insight
      </p>
      <span className="text-xs font-medium text-gray-500 dark:text-slate-400">
        Reviewer-facing
      </span>
    </div>
  ) : isEmbedded ? (
    <div className="flex shrink-0 items-baseline justify-between gap-2">
      <p
        className={`font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400 ${titleSize}`}
      >
        Intake insight
      </p>
      <span className={`font-normal ${mutedClass}`}>Reviewer-facing</span>
    </div>
  ) : (
    <div className="flex shrink-0 items-start justify-between gap-2">
      <p
        className={`font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300 ${titleSize}`}
      >
        Intake insight
      </p>
      <span className="font-medium text-[10px] text-gray-500 dark:text-slate-400">
        Reviewer-facing
      </span>
    </div>
  );

  const clarity = getTriageClarityIndicator(triage, {
    title: ticketTitle,
    description: ticketDescription,
    triageSummary: triage?.summary,
  });
  const showClarity =
    !loading && !unavailable && hasContent && clarity !== null;

  return (
    <div className={outerClass}>
      {headerRow}
      <p className={`${isModalColumn ? "mt-2" : isEmbedded ? "mt-1" : "mt-1.5"} shrink-0 ${mutedClass}`}>
        Captures what matters so reviewers can act with fewer meetings. Suggestions stay
        advisory until applied.
      </p>
      {showClarity && clarity ? (
        <div className={`${isModalColumn ? "mt-3" : isEmbedded ? "mt-2" : "mt-2.5"} shrink-0`}>
          <span
            className={`inline-flex max-w-full items-center rounded-full border px-2.5 py-1 text-xs font-semibold leading-snug ${clarity.toneClass}`}
          >
            {clarity.label}
          </span>
        </div>
      ) : null}
      {loading ? (
        <p className={`mt-4 ${bodyClass} text-gray-600 dark:text-slate-300`}>
          Analyzing request…
        </p>
      ) : unavailable ? (
        <p className={`mt-4 ${bodyClass} text-gray-600 dark:text-slate-400`}>
          {unavailableMessage?.trim() ||
            "Intake insight is not available. Use ticket details to review."}
        </p>
      ) : hasContent ? (
        <>
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
              <dt className={sectionLabelClass}>Execution risk</dt>
              <dd className="space-y-1.5">
                <p className={mutedClass}>
                  Unclear work increases delays and follow-up.
                </p>
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
              <dt className={sectionLabelClass}>
                What would have required a follow-up
              </dt>
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
        {isModalColumn ? (
          <p
            className={`${mutedClass} mt-4 border-t border-gray-200/90 pt-4 dark:border-slate-700/90`}
          >
            All decisions are tracked in history—no need to chase status updates.
          </p>
        ) : null}
        </>
      ) : (
        <p className={`mt-4 ${bodyClass} text-gray-500 dark:text-slate-500`}>
          No analysis yet.
        </p>
      )}
    </div>
  );
}

/** Persisted screenshot (attachment) AI insight in the reviewer modal rail. */
function ScreenshotInsightTriagePanel({
  insight,
}: {
  insight: ScreenshotInsightPersisted;
}) {
  const bodyClass = "text-sm leading-relaxed text-gray-800 dark:text-slate-200";
  const sectionLabelClass =
    "text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400";
  const mutedClass = "text-xs leading-relaxed text-gray-500 dark:text-slate-400";
  const visibleLines = filterScreenshotInsightNoise(insight.visibleDetails ?? []);
  const issueLines = filterScreenshotInsightNoise(insight.possibleIssues ?? []);
  const followLines = filterScreenshotInsightNoise(
    insight.recommendedFollowUp ?? [],
  );

  const analyzedLabel =
    insight.analyzedImageCount != null && insight.analyzedImageCount > 0
      ? `${insight.analyzedImageCount} image${
          insight.analyzedImageCount === 1 ? "" : "s"
        } analyzed`
      : null;

  let dateLabel: string | null = null;
  if (insight.analyzedAtUtc) {
    const d = new Date(insight.analyzedAtUtc);
    if (!Number.isNaN(d.getTime())) {
      dateLabel = d.toLocaleString(undefined, {
        dateStyle: "medium",
        timeStyle: "short",
      });
    }
  }

  const metaLine = [analyzedLabel, dateLabel].filter(Boolean).join(" · ");

  return (
    <section aria-label="Attachment insight" className="min-w-0 text-left">
      <div className="flex shrink-0 items-start justify-between gap-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Attachment insight
        </p>
        <span className="text-xs font-medium text-gray-500 dark:text-slate-400">
          Attachment insight
        </span>
      </div>
      <p className={`mt-2 ${mutedClass}`}>
        From images attached to this ticket. Advisory only—review the files
        directly when decisions matter.
      </p>
      {metaLine ? <p className={`mt-2 ${mutedClass}`}>{metaLine}</p> : null}
      {insight.analyzedFileNames && insight.analyzedFileNames.length > 0 ? (
        <p className={`mt-1 ${mutedClass} break-words`}>
          {insight.analyzedFileNames.join(", ")}
        </p>
      ) : null}
      <dl className={`mt-4 space-y-4 ${bodyClass}`}>
        {insight.summary?.trim() ? (
          <div className="space-y-1.5">
            <dt className={sectionLabelClass}>Summary</dt>
            <dd className="whitespace-pre-wrap">{insight.summary.trim()}</dd>
          </div>
        ) : null}
        {visibleLines.length > 0 ? (
          <div className="space-y-1.5">
            <dt className={sectionLabelClass}>Visible details</dt>
            <dd>
              <ul className="list-outside list-disc space-y-2 pl-5">
                {visibleLines.map((line, index) => (
                  <li key={index} className="leading-relaxed">
                    {line}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        ) : null}
        {issueLines.length > 0 ? (
          <div className="space-y-1.5">
            <dt className={sectionLabelClass}>Possible issues</dt>
            <dd>
              <ul className="list-outside list-disc space-y-2 pl-5">
                {issueLines.map((line, index) => (
                  <li key={index} className="leading-relaxed">
                    {line}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        ) : null}
        {followLines.length > 0 ? (
          <div className="space-y-1.5">
            <dt className={sectionLabelClass}>Recommended follow-up</dt>
            <dd>
              <ul className="list-outside list-disc space-y-2 pl-5">
                {followLines.map((line, index) => (
                  <li key={index} className="leading-relaxed">
                    {line}
                  </li>
                ))}
              </ul>
            </dd>
          </div>
        ) : null}
      </dl>
    </section>
  );
}

/**
 * Right-hand intake insight rail for the reviewer ticket modal (~1/3 width). Matches intake review layout:
 * bordered column, Regenerate on top, scrollable advisory content.
 */
export function ApprovalTriageModalColumn({
  ticket,
  onRegenerateAnalysis,
  canRegenerateAnalysis,
  regenerateDisabledHint,
  regenerateLoading = false,
  applyControls,
}: {
  ticket: Ticket;
  onRegenerateAnalysis?: () => void | Promise<void>;
  canRegenerateAnalysis?: boolean;
  regenerateDisabledHint?: string | null;
  regenerateLoading?: boolean;
  applyControls?: ApprovalTriageApplyControls;
}) {
  const triageScrollRef = useRef<HTMLDivElement | null>(null);
  const canRegenerate =
    Boolean(onRegenerateAnalysis) &&
    (canRegenerateAnalysis ?? Boolean(onRegenerateAnalysis));
  const applyHelperText = applyControls ? getApplyHelperText(applyControls) : null;
  const triageActionPending = applyControls?.pendingAction != null;

  return (
    <aside
      className="flex min-h-0 min-w-0 flex-col"
      aria-label="Intake insight"
    >
      <div className="relative flex min-h-0 flex-1 flex-col overflow-hidden">
        <div className="shrink-0 border-b border-gray-200 px-4 py-3 dark:border-slate-800">
          <div className="flex justify-end">
            <button
              type="button"
              onClick={() => void onRegenerateAnalysis?.()}
              disabled={!canRegenerate || regenerateLoading || triageActionPending}
              aria-busy={regenerateLoading}
              title={!canRegenerate ? (regenerateDisabledHint ?? undefined) : undefined}
              className="ai-button ai-button--ready rounded-md px-3 py-1.5 text-xs font-semibold text-cortex-blue-dark hover:bg-cortex-blue-soft disabled:cursor-not-allowed disabled:opacity-60 dark:text-emerald-300 dark:hover:bg-emerald-950/40"
            >
              <span>
                {regenerateLoading ? "Regenerating…" : "Regenerate Analysis"}
              </span>
            </button>
          </div>
          {!canRegenerate && regenerateDisabledHint ? (
            <p className="mt-1.5 text-right text-[11px] text-gray-500 dark:text-slate-400">
              {regenerateDisabledHint}
            </p>
          ) : null}
        </div>
        <ScrollableViewport
          viewportRef={triageScrollRef}
          outerClassName="flex-1"
          viewportClassName="h-full overflow-y-auto px-4 py-4"
          affordanceAriaLabel="Scroll intake insight to bottom"
        >
            <ApprovalTriagePanel
              triage={ticket.approvalTriagePreview}
              presentation="modalColumn"
              ticketTitle={ticket.title}
              ticketDescription={ticket.description}
            />
            {screenshotInsightPersistedHasContent(ticket.screenshotInsight) ? (
              <div className="mt-6 border-t border-gray-200/90 pt-6 dark:border-slate-700/90">
                <ScreenshotInsightTriagePanel
                  insight={ticket.screenshotInsight!}
                />
              </div>
            ) : null}
        </ScrollableViewport>
        {applyControls ? (
          <div className="shrink-0 border-t border-gray-200 px-4 py-3 dark:border-slate-800">
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
              Apply Suggestions
            </p>
            <p className="mt-1 text-xs leading-snug text-gray-500 dark:text-slate-500">
              Apply to avoid a follow-up discussion.
            </p>
            {applyHelperText ? (
              <p className="mt-1 text-xs leading-relaxed text-gray-500 dark:text-slate-400">
                {applyHelperText}
              </p>
            ) : null}
            {applyControls.errorMessage?.trim() ? (
              <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs leading-relaxed text-red-800 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-200">
                {applyControls.errorMessage.trim()}
              </div>
            ) : null}
            <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
              <button
                type="button"
                onClick={() => void applyControls.onApplyPriority()}
                disabled={!applyControls.canApplyPriority || triageActionPending}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-xs font-semibold text-gray-800 shadow-sm transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-55 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700"
              >
                {applyControls.pendingAction === "priority"
                  ? "Applying…"
                  : "Apply Priority"}
              </button>
              <button
                type="button"
                onClick={() => void applyControls.onApplyStatus()}
                disabled={!applyControls.canApplyStatus || triageActionPending}
                className="rounded-md border border-gray-300 bg-white px-3 py-2 text-xs font-semibold text-gray-800 shadow-sm transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-55 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700"
              >
                {applyControls.pendingAction === "status"
                  ? "Applying…"
                  : "Apply Status"}
              </button>
              <button
                type="button"
                onClick={() => void applyControls.onApplyBoth()}
                disabled={!applyControls.canApplyBoth || triageActionPending}
                className="rounded-md bg-cortex-blue px-3 py-2 text-xs font-semibold text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-55"
              >
                {applyControls.pendingAction === "both"
                  ? "Applying…"
                  : "Apply Both"}
              </button>
            </div>
          </div>
        ) : null}
      </div>
    </aside>
  );
}
