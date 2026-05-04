import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useRef, useState } from "react";
import { ticketService } from "../services/api";
import type {
  CortexInsight,
  CortexLearningSignal,
} from "../types/cortexInsight";
import type { CortexRiskLevel, CortexSlaRisk } from "../types/cortexRisk";

const API_AUDIENCE = "https://cortex-api";
/** Sentence returned from risk APIs for memory-style patterns; detection uses equality. */
const MEMORY_PATTERN_SIGNAL_API = "Recent similar issues required follow-up";
const MEMORY_PATTERN_WATCH_ITEM = "Similar past work suggested follow-up";
const SIGNAL_SLA_PRESSURE = "SLA overdue or breach pressure";
const SIGNAL_APPROACHING_SLA = "Approaching SLA deadline";
const SIGNAL_PRIORITY = "Elevated priority";
const MEDIUM_INSIGHT_CONFIDENCE = 50;

interface CortexRiskPanelProps {
  ticketId: string;
  isOpen: boolean;
  insight?: CortexInsight | null;
  onRiskReady?: (risk: CortexSlaRisk | null) => void;
  onRecommendedActionClick?: () => void;
  highlightPanel?: boolean;
}

function levelClasses(level: CortexRiskLevel): string {
  switch (level) {
    case "High":
      return "bg-red-100 text-red-900 dark:bg-red-950/40 dark:text-red-100";
    case "Medium":
      return "bg-amber-100 text-amber-950 dark:bg-amber-950/40 dark:text-amber-50";
    default:
      return "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100";
  }
}

function riskStatusLabel(risk: CortexSlaRisk): "At Risk" | "Needs Attention" | "Stable" {
  const sla = (risk.slaStatus || "").toLowerCase();
  if (sla.includes("overdue") || sla.includes("late")) {
    return "At Risk";
  }
  if (risk.riskLevel === "High") {
    return "At Risk";
  }
  if (risk.riskLevel === "Medium") {
    return "Needs Attention";
  }
  return "Stable";
}

function riskStatusClass(status: "At Risk" | "Needs Attention" | "Stable"): string {
  switch (status) {
    case "At Risk":
      return "bg-red-100 text-red-900 dark:bg-red-950/40 dark:text-red-100";
    case "Needs Attention":
      return "bg-amber-100 text-amber-950 dark:bg-amber-950/40 dark:text-amber-50";
    default:
      return "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100";
  }
}

function containsAny(source: string | undefined | null, terms: string[]): boolean {
  const normalized = source?.toLowerCase() ?? "";
  return terms.some((term) => normalized.includes(term));
}

function isMediumOrHigh(confidence: string | undefined | null): boolean {
  const normalized = confidence?.trim().toLowerCase();
  return normalized === "medium" || normalized === "high";
}

function hasRiskLearningSignal(signal: CortexLearningSignal): boolean {
  if (!isMediumOrHigh(signal.confidence)) {
    return false;
  }

  const text = [
    signal.signalType,
    signal.title,
    signal.description,
    ...(signal.supportingFacts ?? []),
  ]
    .join(" ")
    .toLowerCase();

  if (
    containsAny(text, [
      "follow-up",
      "follow up",
      "clarification",
      "reassign",
      "reassignment",
      "reassigned",
      "reopened",
      "rework",
      "override",
      "overridden",
      "returned",
      "rejected",
      "needs more info",
      "more detail",
    ])
  ) {
    return true;
  }

  return (
    text.includes("sla") &&
    containsAny(text, [
      "breach",
      "breached",
      "elevated",
      "pressure",
      "late",
      "miss",
      "missed",
      "at risk",
    ])
  );
}

function hasMemoryPatternRisk(insight?: CortexInsight | null): boolean {
  const matches = insight?.matches ?? [];
  if (matches.length === 0) {
    return false;
  }

  const hasMediumConfidence =
    (insight?.confidenceScore ?? 0) >= MEDIUM_INSIGHT_CONFIDENCE ||
    matches.some((match) => match.confidenceScore >= MEDIUM_INSIGHT_CONFIDENCE);
  if (!hasMediumConfidence) {
    return false;
  }

  const hasLearningRisk = (insight?.learningSignals ?? []).some(hasRiskLearningSignal);
  const hasFrictionStatus = matches.some(
    (match) =>
      match.confidenceScore >= MEDIUM_INSIGHT_CONFIDENCE &&
      containsAny(match.status, [
        "rejected",
        "needs more info",
        "returned",
        "reopened",
        "resolved late",
        "breached",
      ]),
  );

  return hasLearningRisk || hasFrictionStatus;
}

function buildPredictiveSignals(
  risk: CortexSlaRisk,
  insight?: CortexInsight | null,
): string[] {
  const sources = [risk.slaStatus, ...risk.riskReasons, risk.recommendation]
    .filter((value) => value && value.trim().length > 0)
    .join(" ")
    .toLowerCase();
  const signals: string[] = [];

  if (sources.includes("priority") && (sources.includes("high") || sources.includes("critical"))) {
    signals.push(SIGNAL_PRIORITY);
  }

  if (
    sources.includes("sla") &&
    (sources.includes("overdue") || sources.includes("late"))
  ) {
    signals.push(SIGNAL_SLA_PRESSURE);
  } else if (
    sources.includes("sla") &&
    (sources.includes("near") ||
      sources.includes("deadline") ||
      sources.includes("approach") ||
      sources.includes("due"))
  ) {
    signals.push(SIGNAL_APPROACHING_SLA);
  }

  // Weak / optional gaps (distinct from blocking "missing detail" escalation).
  if (
    sources.includes("optional refinement") ||
    sources.includes("review can proceed")
  ) {
    signals.push("Minor detail gap");
  } else if (
    sources.includes("missing detail") ||
    sources.includes("missing information") ||
    sources.includes("missing required") ||
    (sources.includes("missing") && sources.includes("detail"))
  ) {
    signals.push("Missing required detail");
  }

  if (
    sources.includes("awaiting approval") ||
    sources.includes("pending approval") ||
    (sources.includes("approval") && sources.includes("await"))
  ) {
    signals.push("Awaiting approval");
  }

  if (sources.includes("longer than the usual intake window")) {
    signals.push("Extended approval wait");
  }

  if (
    sources.includes("workload") ||
    sources.includes("capacity") ||
    sources.includes("overloaded") ||
    sources.includes("load")
  ) {
    signals.push("Owner workload high");
  }

  if (
    risk.riskReasons.some((reason) => reason.trim() === MEMORY_PATTERN_SIGNAL_API) ||
    hasMemoryPatternRisk(insight)
  ) {
    signals.push(MEMORY_PATTERN_WATCH_ITEM);
  }

  if (signals.length === 0 && risk.riskLevel === "High") {
    signals.push(SIGNAL_APPROACHING_SLA);
  }

  return Array.from(new Set(signals)).slice(0, 4);
}

function leadCopy(level: CortexRiskLevel, signals: string[]): string {
  const onlyIntakeRoutine =
    signals.length > 0 &&
    signals.every((s) => ["Awaiting approval", "Minor detail gap"].includes(s));
  if (level === "Low" && onlyIntakeRoutine) {
    return "No SLA escalation needed. Intake timing looks routine—approve, return, or reject remains a separate business judgment.";
  }
  switch (level) {
    case "High":
      return "SLA operational risk is high—this ticket likely needs action soon.";
    case "Medium":
      return "Operational risk may be elevated from SLA and workflow signals below.";
    default:
      return "No SLA escalation needed. Cortex has not flagged active SLA deadline pressure from this evaluation.";
  }
}

function recommendedAttentionCopy(
  risk: CortexSlaRisk,
  signals: string[],
): string {
  const status = riskStatusLabel(risk);
  const onlyIntakeInformational =
    signals.length > 0 &&
    signals.every((s) =>
      ["Awaiting approval", "Minor detail gap"].includes(s),
    );

  if (status === "Stable" && (signals.length === 0 || onlyIntakeInformational)) {
    return "Proceed with normal review pacing. Approval, return, or rejection should still be based on business justification—not this SLA read.";
  }

  if (signals.includes("Extended approval wait")) {
    return "Follow up with approvers — intake has waited longer than usual.";
  }
  if (signals.includes("Missing required detail")) {
    return "Request missing details now.";
  }
  if (signals.includes("Awaiting approval") && status === "Needs Attention") {
    return "Prioritize approval or return for detail.";
  }
  if (signals.includes("Owner workload high")) {
    return "Assign to an available owner.";
  }
  if (signals.includes(SIGNAL_SLA_PRESSURE) || signals.includes(SIGNAL_APPROACHING_SLA)) {
    return "Review before end of day.";
  }
  if (signals.includes(MEMORY_PATTERN_WATCH_ITEM)) {
    return "Use follow-up checklist before assignment.";
  }
  switch (risk.riskLevel) {
    case "High":
      return "Review before end of day.";
    case "Medium":
      return "Review within the current shift.";
    default:
      return "No SLA-driven urgency surfaced here—prioritize approve, return, or reject based on substance, not SLA timing alone.";
  }
}

function recommendedNextStep(risk: CortexSlaRisk, signals: string[]): string {
  const fromApi = risk.recommendation?.trim();
  if (fromApi) {
    return fromApi;
  }
  return recommendedAttentionCopy(risk, signals);
}

export default function CortexRiskPanel({
  ticketId,
  isOpen,
  insight,
  onRiskReady,
  onRecommendedActionClick,
  highlightPanel = false,
}: CortexRiskPanelProps) {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const [risk, setRisk] = useState<CortexSlaRisk | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const riskFetchCompletedRef = useRef(false);

  useEffect(() => {
    if (!isOpen || !ticketId || !isAuthenticated) {
      return;
    }

    const controller = new AbortController();
    let cancelled = false;

    (async () => {
      riskFetchCompletedRef.current = false;
      setLoading(true);
      setError(null);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const result = await ticketService.getRisk(
          ticketId,
          token,
          controller.signal,
        );
        if (!cancelled) {
          setRisk(result);
          onRiskReady?.(result);
        }
      } catch (err) {
        if (!cancelled && (err as { name?: string }).name !== "AbortError") {
          setError("Unable to load risk guidance.");
        }
      } finally {
        riskFetchCompletedRef.current = true;
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [getAccessTokenSilently, isAuthenticated, isOpen, onRiskReady, ticketId]);

  if (!ticketId) {
    return null;
  }

  const predictiveSignals = risk ? buildPredictiveSignals(risk, insight) : [];
  const riskDriverLines = risk
    ? risk.riskReasons.map((r) => r.trim()).filter((r) => r.length > 0)
    : [];

  return (
    <div
      id="cortex-risk-panel"
      className={`px-4 py-4 transition-colors ${
        highlightPanel
          ? "rounded-md border border-red-300 bg-red-50/40 dark:border-red-700 dark:bg-red-950/20"
          : ""
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            SLA &amp; operational risk
          </p>
          <p className="mt-1 max-w-xl text-xs leading-snug text-slate-500 dark:text-slate-400">
            SLA operational signals—not business approval outcomes. Routing and reviewer actions stay on Decision.
          </p>
        </div>
        {risk ? (
          <span
            className={`shrink-0 rounded-md px-2.5 py-1 text-xs font-semibold ${levelClasses(risk.riskLevel)}`}
          >
            {risk.riskLevel} risk
          </span>
        ) : null}
      </div>

      {loading && !risk ? (
        <p className="mt-3 text-sm text-slate-500 dark:text-slate-400" role="status">
          Loading SLA and operational risk guidance…
        </p>
      ) : error ? (
        <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">{error}</p>
      ) : risk ? (
        <div className="mt-4 space-y-4 text-sm text-slate-800 dark:text-slate-100">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Current operational risk level
            </p>
            <div className="mt-2 flex flex-col gap-2 sm:flex-row sm:items-start sm:gap-3">
              <div className="flex flex-wrap items-center gap-2">
                <span
                  className={`rounded-md px-2.5 py-1 text-xs font-semibold ${riskStatusClass(
                    riskStatusLabel(risk),
                  )}`}
                >
                  {riskStatusLabel(risk)}
                </span>
              </div>
              <p className="min-w-0 flex-1 text-sm leading-snug text-slate-600 dark:text-slate-300">
                <span className="font-medium text-slate-800 dark:text-slate-100">
                  Risk outlook:{" "}
                </span>
                {leadCopy(risk.riskLevel, predictiveSignals)}
              </p>
            </div>
          </div>

          {risk.slaStatus?.trim() ? (
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                SLA outlook
              </p>
              <p className="mt-1.5 text-sm text-slate-700 dark:text-slate-200">
                {risk.slaStatus.trim()}
              </p>
            </div>
          ) : null}

          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Risk drivers
            </p>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
              Why this ticket may need attention (from Cortex&apos;s SLA and workflow
              evaluation).
            </p>
            {riskDriverLines.length > 0 ? (
              <ul className="mt-2 list-disc space-y-1.5 pl-5 text-sm text-slate-700 dark:text-slate-200">
                {riskDriverLines.map((line, index) => (
                  <li key={`${index}-${line.slice(0, 48)}`}>{line}</li>
                ))}
              </ul>
            ) : (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                No separate driver list was returned—use the outlook, SLA line, and
                watch items below.
              </p>
            )}
          </div>

          {predictiveSignals.length > 0 ? (
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Supporting watch items
              </p>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                Condensed reminders from the same evaluation (different from the driver list above).
              </p>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700 dark:text-slate-200">
                {predictiveSignals.map((signal, index) => (
                  <li key={`${index}-${signal}`}>{signal}</li>
                ))}
              </ul>
            </div>
          ) : null}

          <div className="rounded-md border border-slate-100 bg-slate-50/70 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Recommended next step
            </p>
            {risk.recommendation?.trim() ? null : (
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                Heuristic suggestion from SLA and intake rules when the service does
                not return a specific line.
              </p>
            )}
            {onRecommendedActionClick ? (
              <button
                type="button"
                onClick={onRecommendedActionClick}
                className="mt-1.5 text-left text-sm font-semibold text-slate-900 underline-offset-2 hover:underline dark:text-slate-50"
              >
                {recommendedNextStep(risk, predictiveSignals)}
              </button>
            ) : (
              <p className="mt-1.5 text-sm font-semibold text-slate-900 dark:text-slate-50">
                {recommendedNextStep(risk, predictiveSignals)}
              </p>
            )}
            {risk.recommendationReason?.trim() ? (
              <p className="mt-2 text-xs leading-snug text-slate-600 dark:text-slate-400">
                {risk.recommendationReason.trim()}
              </p>
            ) : null}
          </div>

          <details className="rounded-md border border-slate-100 bg-white/40 px-3 py-2 text-xs text-slate-600 dark:border-slate-800 dark:bg-slate-950/30 dark:text-slate-400">
            <summary className="cursor-pointer font-semibold text-slate-700 dark:text-slate-300">
              Supporting detail
            </summary>
            <dl className="mt-2 space-y-1.5">
              <div>
                <dt className="font-medium text-slate-600 dark:text-slate-400">
                  Evaluation time
                </dt>
                <dd>
                  {new Date(risk.evaluatedAtUtc).toLocaleString(undefined, {
                    dateStyle: "short",
                    timeStyle: "short",
                  })}
                </dd>
              </div>
            </dl>
            <p className="mt-2 text-[11px] leading-snug text-slate-500 dark:text-slate-500">
              Guidance blends SLA and workflow inputs with optional advisory patterns;
              it does not display a separate routing score.
            </p>
          </details>
        </div>
      ) : riskFetchCompletedRef.current && !loading && !risk ? (
        <p className="mt-3 text-sm leading-snug text-slate-600 dark:text-slate-300">
          No SLA risk signals are available yet. Cortex has not detected an active SLA
          risk for this ticket, or risk guidance has not been recorded.
        </p>
      ) : null}
    </div>
  );
}
