import type { ScreenshotInsightResult } from "../../types/screenshotInsight";
import { filterScreenshotInsightNoise } from "../../utils/screenshotInsightDisplay";

export function ScreenshotInsightEvidenceCard({
  result,
  compactForReviewerRail,
}: {
  result: ScreenshotInsightResult;
  compactForReviewerRail: boolean;
}) {
  const visibleLines = filterScreenshotInsightNoise(result.visibleDetails);
  const issueLines = filterScreenshotInsightNoise(result.possibleIssues);
  const followLines = filterScreenshotInsightNoise(result.recommendedFollowUp);
  const keyFindings = [...issueLines, ...visibleLines].slice(0, 2);

  const fullInsightContent = (
    <div className="space-y-5">
      <p className="text-[11px] leading-relaxed text-gray-500 dark:text-slate-500">
        Advisory read of visible screenshots. Review the attached files directly
        when decisions matter.
      </p>

      <div className="border-b border-gray-200 pb-4 dark:border-slate-700">
        <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Summary
        </p>
        <p className="text-[15px] font-semibold leading-snug text-gray-900 dark:text-slate-50">
          {result.summary?.trim() || "-"}
        </p>
      </div>

      {issueLines.length > 0 ? (
        <div className="rounded-lg border border-amber-300/80 bg-amber-50/90 px-3 py-3 dark:border-amber-700/50 dark:bg-amber-950/35">
          <p className="mb-2.5 text-xs font-bold uppercase tracking-wide text-amber-950 dark:text-amber-200">
            Possible issues
          </p>
          <ul className="list-none space-y-2.5 pl-0">
            {issueLines.map((line, idx) => (
              <li
                key={`pi-${idx}-${line.slice(0, 32)}`}
                className={
                  idx === 0
                    ? "border-l-[3px] border-amber-600 pl-3 text-sm font-semibold leading-relaxed text-gray-900 dark:border-amber-400 dark:text-slate-50"
                    : "border-l-[3px] border-amber-200/80 pl-3 text-sm leading-relaxed text-gray-800 dark:border-amber-800/60 dark:text-slate-200"
                }
              >
                {line}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {followLines.length > 0 ? (
        <div className="rounded-lg border border-emerald-300/70 bg-emerald-50/60 px-3 py-3 dark:border-emerald-800/50 dark:bg-emerald-950/25">
          <p className="mb-2 text-xs font-bold uppercase tracking-wide text-emerald-950 dark:text-emerald-200">
            Screenshot follow-up
          </p>
          <ul className="list-none space-y-2 pl-0">
            {followLines.map((line, idx) => (
              <li
                key={`rf-${idx}-${line.slice(0, 32)}`}
                className="flex gap-2 text-sm font-medium leading-relaxed text-emerald-950 dark:text-emerald-100"
              >
                <span
                  className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-emerald-600 dark:bg-emerald-400"
                  aria-hidden="true"
                />
                <span>{line}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {visibleLines.length > 0 ? (
        <div className="border-t border-gray-200 pt-4 dark:border-slate-700">
          <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
            What&apos;s visible
          </p>
          <p className="mb-2 text-[11px] text-gray-500 dark:text-slate-500">
            Observable UI detail, secondary to the reviewer analysis.
          </p>
          <ul className="list-outside list-disc space-y-2 pl-5 text-sm leading-[1.55] text-gray-600 dark:text-slate-400">
            {visibleLines.map((line, idx) => (
              <li key={`vis-${idx}-${line.slice(0, 32)}`}>{line}</li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );

  return (
    <div
      className={`rounded-md border border-gray-200 bg-gray-50 text-sm text-gray-800 dark:border-slate-700 dark:bg-slate-900/50 dark:text-slate-200 ${
        compactForReviewerRail ? "p-3" : "p-4"
      }`}
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold tracking-wide text-gray-700 dark:text-slate-300">
            {compactForReviewerRail
              ? "Screenshot Evidence"
              : "Screenshot Insight"}
          </p>
          {compactForReviewerRail ? (
            <p className="mt-1 text-[11px] leading-snug text-gray-500 dark:text-slate-500">
              Supporting evidence only. Use reviewer readiness for decision
              guidance.
            </p>
          ) : null}
        </div>
        {compactForReviewerRail ? (
          <span className="shrink-0 rounded-full bg-cortex-blue-soft px-2 py-0.5 text-[11px] font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
            Visual evidence checked
          </span>
        ) : null}
      </div>

      {result.unavailable ? (
        <p className="text-sm text-amber-900 dark:text-amber-100" role="status">
          {result.unavailableReason?.trim() ||
            "Screenshot evidence is not ready yet."}
        </p>
      ) : compactForReviewerRail ? (
        <div className="space-y-3">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              Summary
            </p>
            <p className="mt-1 text-sm font-semibold leading-snug text-gray-900 dark:text-slate-50">
              {result.summary?.trim() || "-"}
            </p>
          </div>

          {keyFindings.length > 0 ? (
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                Key screenshot findings
              </p>
              <ul className="mt-1 list-outside list-disc space-y-1.5 pl-5 text-sm leading-relaxed text-gray-700 dark:text-slate-300">
                {keyFindings.map((line, idx) => (
                  <li key={`evidence-${idx}-${line.slice(0, 32)}`}>{line}</li>
                ))}
              </ul>
            </div>
          ) : null}

          <details className="rounded-md border border-gray-200 bg-white/70 px-3 py-2 dark:border-slate-700 dark:bg-slate-950/30">
            <summary className="cursor-pointer text-xs font-semibold text-cortex-blue-dark hover:text-cortex-blue dark:text-cortex-cyan">
              Inspect full screenshot insight
            </summary>
            <div
              className="scroll-surface scroll-chain-auto mt-3 max-h-[min(42vh,20rem)] overflow-y-auto pr-0.5"
            >
              {fullInsightContent}
            </div>
          </details>
        </div>
      ) : (
        <div
          // `scroll-surface` applies `overscroll-behavior: contain`, which
          // traps wheel events at this nested scroller's bottom and freezes
          // the parent main-column scroll (same pattern that caused the
          // Cortex Decision freeze). Inline override restores default chain
          // behavior without losing the hidden-scrollbar styling.
          className="scroll-surface scroll-chain-auto relative max-h-[min(45vh,22rem)] overflow-y-auto pr-0.5"
        >
          {fullInsightContent}
        </div>
      )}
    </div>
  );
}
