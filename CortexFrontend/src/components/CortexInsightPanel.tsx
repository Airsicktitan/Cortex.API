import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ticketService } from "../services/api";
import type {
  CortexInsight,
  CortexLearningSignal,
} from "../types/cortexInsight";
import { formatDisplayDateTime } from "../utils/presentation";

const API_AUDIENCE = "https://cortex-api";

interface CortexInsightPanelProps {
  ticketId: string;
  isOpen: boolean;
  onOpenSourceTicket?: (ticketId: string) => void | Promise<void>;
  onInsightReady?: (insight: CortexInsight | null) => void;
}

function InsightField({
  label,
  value,
}: {
  label: string;
  value?: string | null;
}) {
  return (
    <div className="rounded-md border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/40">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
        {label}
      </p>
      <p className="mt-1 text-sm leading-relaxed text-slate-800 dark:text-slate-100">
        {value?.trim() || "-"}
      </p>
    </div>
  );
}

function confidenceBadgeClasses(confidence: string): string {
  switch (confidence?.trim().toLowerCase()) {
    case "high":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200";
    case "medium":
      return "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200";
    case "low":
    default:
      return "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300";
  }
}

/**
 * Picks a headline metric from learning signal facts (e.g. "88% override rate",
 * "82% resolved within SLA"). Returns null if nothing clear enough to show.
 */
function extractKeyMetricFromSupportingFacts(
  supportingFacts: string[],
): string | null {
  for (const fact of supportingFacts) {
    const m = fact.match(
      /(\d{1,3}(?:\.\d+)?%[^.;]*)/i,
    );
    if (!m) continue;
    let candidate = m[0].replace(/\s+/g, " ").replace(/[,:;]$/g, "").trim();
    if (candidate.includes(",")) {
      const beforeComma = candidate.split(",")[0].trim();
      if (beforeComma.length >= 4 && /\d{1,3}(?:\.\d+)?%/.test(beforeComma)) {
        candidate = beforeComma;
      }
    }
    if (candidate.length < 4) continue;
    const lower = candidate.toLowerCase();
    const hasMetricContext =
      lower.includes("rate") ||
      lower.includes("success") ||
      lower.includes("sla") ||
      lower.includes("override") ||
      lower.includes("resolved") ||
      lower.includes("breach") ||
      lower.includes("within") ||
      /%\s*of\s/.test(lower) ||
      /%\s+of\s/.test(lower);
    if (hasMetricContext) {
      return candidate;
    }
  }
  for (const fact of supportingFacts) {
    const m2 = fact.match(
      /(\d{1,3}(?:\.\d+)?%\s+[\w'/-]+(?:\s+[\w'/-]+){1,12})/i,
    );
    if (m2) {
      let c = m2[0].replace(/\s+/g, " ").replace(/[,:;]$/g, "").trim();
      if (c.includes(",")) {
        const before = c.split(",")[0].trim();
        if (before.length >= 4 && /\d{1,3}(?:\.\d+)?%/.test(before)) {
          c = before;
        }
      }
      return c;
    }
  }
  return null;
}

function LearningSignalCard({ signal }: { signal: CortexLearningSignal }) {
  const confidenceLabel = signal.confidence?.trim() || "Low";
  const supportingFacts = signal.supportingFacts ?? [];
  const keyMetric = extractKeyMetricFromSupportingFacts(supportingFacts);
  const useTwoLineHeader =
    Boolean(keyMetric) &&
    keyMetric != null &&
    signal.title.length + keyMetric.length > 60;
  const showImpactGlyph =
    keyMetric && confidenceLabel.toLowerCase() !== "high";
  return (
    <div className="rounded-md border border-slate-100 bg-white px-3 py-3 dark:border-slate-800 dark:bg-slate-950/40">
      <div className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-2">
        <span
          className={`rounded-md px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${confidenceBadgeClasses(
            confidenceLabel,
          )}`}
        >
          {confidenceLabel}
        </span>
        {signal.signalType ? (
          <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
            {signal.signalType}
          </span>
        ) : null}
        </div>
        {keyMetric && !useTwoLineHeader ? (
          <p className="text-sm font-semibold leading-snug text-slate-900 dark:text-slate-50">
            {showImpactGlyph ? (
              <span
                className="mr-1 inline text-amber-600 dark:text-amber-400"
                aria-hidden
              >
                ⚠️
              </span>
            ) : null}
            {signal.title}
            <span className="font-bold text-slate-950 dark:text-slate-100">
              {" "}
              ({keyMetric})
            </span>
          </p>
        ) : keyMetric && useTwoLineHeader ? (
          <div>
            <p className="text-sm font-semibold leading-snug text-slate-900 dark:text-slate-50">
              {showImpactGlyph ? (
                <span
                  className="mr-1 inline text-amber-600 dark:text-amber-400"
                  aria-hidden
                >
                  ⚠️
                </span>
              ) : null}
              {signal.title}
            </p>
            <p className="mt-0.5 text-sm font-bold leading-snug text-slate-950 dark:text-slate-100">
              <span className="mr-0.5 font-normal text-slate-500 dark:text-slate-400" aria-hidden>
                →
              </span>
              {keyMetric}
            </p>
          </div>
        ) : (
        <p className="text-sm font-semibold leading-snug text-slate-900 dark:text-slate-50">
          {signal.title}
        </p>
        )}
      </div>
      {signal.description ? (
        <p className="mt-1.5 text-sm leading-relaxed text-slate-700 dark:text-slate-200">
          {signal.description}
        </p>
      ) : null}
      {supportingFacts.length > 0 ? (
        <details className="mt-2 rounded-md border border-slate-100 bg-slate-50/70 px-3 py-2 dark:border-slate-800 dark:bg-slate-950/30">
          <summary className="cursor-pointer text-xs font-semibold text-cortex-blue-dark hover:text-cortex-blue dark:text-cortex-cyan">
            Supporting detail
          </summary>
          <ul className="mt-2 list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
            {supportingFacts.map((fact, index) => (
              <li key={`${index}-${fact.slice(0, 24)}`}>{fact}</li>
            ))}
          </ul>
        </details>
      ) : null}
    </div>
  );
}

export default function CortexInsightPanel({
  ticketId,
  isOpen,
  onOpenSourceTicket,
  onInsightReady,
}: CortexInsightPanelProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [expanded, setExpanded] = useState(false);
  const [insight, setInsight] = useState<CortexInsight | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setExpanded(false);
    setInsight(null);
    onInsightReady?.(null);
    setLoading(false);
    setError(null);
  }, [ticketId, isOpen, onInsightReady]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      abortRef.current = null;
    };
  }, []);

  const loadInsight = useCallback(async () => {
    if (!isOpen || insight || loading || error || !ticketId) {
      return;
    }

    const controller = new AbortController();
    abortRef.current?.abort();
    abortRef.current = controller;
    setLoading(true);
    setError(null);

    try {
      const token = await getAccessTokenSilently({
        authorizationParams: {
          audience: API_AUDIENCE,
        },
      });
      const result = await ticketService.getInsight(
        ticketId,
        token,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setInsight(result);
        onInsightReady?.(result);
      }
    } catch (err) {
      if (!controller.signal.aborted) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load historical context",
        );
      }
    } finally {
      if (abortRef.current === controller) {
        abortRef.current = null;
        setLoading(false);
      }
    }
  }, [
    getAccessTokenSilently,
    error,
    insight,
    isOpen,
    loading,
    onInsightReady,
    ticketId,
  ]);

  useEffect(() => {
    if (expanded) {
      void loadInsight();
    }
  }, [expanded, loadInsight]);

  const handleToggle = () => {
    const nextExpanded = !expanded;
    setExpanded(nextExpanded);
    if (!nextExpanded) {
      abortRef.current?.abort();
      abortRef.current = null;
      setLoading(false);
    }
  };

  const matches = insight?.matches ?? [];
  const firstSimilar = matches[0] ?? null;
  const learningSignals = insight?.learningSignals ?? [];
  const hasGeneratedFields = insight
    ? [
        insight.summary,
        insight.resolution,
        insight.rootCause,
        insight.suggestedNextStep,
      ].some((value) => Boolean(value?.trim()))
    : false;
  const statusText = useMemo(() => {
    if (loading) {
      return "Loading similar tickets…";
    }
    if (error) {
      return "Unable to load";
    }
    if (!insight) {
      return "Show below to fetch";
    }
    if (matches.length === 0) {
      return "No close matches returned";
    }
    return `${matches.length} similar past ticket${matches.length === 1 ? "" : "s"}`;
  }, [error, insight, loading, matches.length]);

  return (
    <section className="px-4 py-4">
      <button
        type="button"
        onClick={handleToggle}
        aria-expanded={expanded}
        className="flex w-full items-center justify-between gap-3 text-left"
      >
        <span>
          <span className="block text-sm font-semibold text-slate-900 dark:text-slate-50">
            Similar past work
          </span>
          <span className="mt-0.5 block text-xs text-slate-500 dark:text-slate-400">
            {statusText}
          </span>
        </span>
        <span
          className="shrink-0 text-xs font-semibold text-slate-500 dark:text-slate-400"
          aria-hidden="true"
        >
          {expanded ? "Hide detail" : "Show"}
        </span>
      </button>

      {expanded ? (
        <div className="mt-3 border-t border-slate-200 pt-4 dark:border-slate-700">
          {loading ? (
            <p className="text-sm text-slate-600 dark:text-slate-300" role="status">
              Loading similar tickets and historical patterns…
            </p>
          ) : error ? (
            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          ) : insight && matches.length === 0 ? (
            <div className="space-y-4">
              <p className="rounded-md border border-slate-100 bg-slate-50/80 px-3 py-2.5 text-sm leading-snug text-slate-700 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-200">
                <span className="font-semibold text-slate-800 dark:text-slate-100">
                  No historical context found yet.{" "}
                </span>
                Cortex has not found similar past tickets for this item yet. Past
                resolution patterns below can still refine how you judge risk and
                follow-up—they do not set routing here.
              </p>
              {learningSignals.length > 0 ? (
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Past resolution signals
                  </p>
                  <div className="mt-2 space-y-2">
                    {learningSignals.map((signal, index) => (
                      <LearningSignalCard
                        key={`${signal.signalType}-${index}-${signal.title.slice(0, 24)}`}
                        signal={signal}
                      />
                    ))}
                  </div>
                </div>
              ) : null}
            </div>
          ) : insight ? (
            <div className="space-y-4">
              <p className="rounded-md border border-slate-200/80 bg-slate-50/90 px-3 py-2.5 text-xs leading-relaxed text-slate-600 dark:border-slate-700 dark:bg-slate-900/55 dark:text-slate-300">
                Patterns from prior tickets inform judgment only. Ownership, priority,
                and routing remain on the Decision tab—nothing here assigns work.
              </p>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-semibold text-slate-900 dark:text-slate-50">
                  {matches.length} similar past ticket
                  {matches.length === 1 ? "" : "s"} surfaced
                </p>
                <span
                  className="rounded-md bg-cortex-blue-soft px-2.5 py-1 text-xs font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100"
                  title="Advisory similarity index across past tickets—not a routing verdict"
                >
                  Overall similarity •{" "}
                  {Math.max(0, Math.min(100, insight.confidenceScore))}%
                </span>
              </div>

              {insight.matchReasons.length > 0 ? (
                <div className="rounded-md border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/40">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Why tickets look related
                  </p>
                  <ul className="mt-1.5 list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
                    {insight.matchReasons.map((reason) => (
                      <li key={reason}>{reason}</li>
                    ))}
                  </ul>
                </div>
              ) : null}

              {firstSimilar ? (
                <div className="rounded-md border border-cortex-blue/20 bg-white px-3 py-3 dark:border-cortex-blue/35 dark:bg-slate-950/35">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                      Closest similar ticket
                    </p>
                    <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                      Status: {firstSimilar.status || "—"}
                    </span>
                    <span
                      className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200"
                      title="How closely this past ticket resembles the present case"
                    >
                      Match confidence • {firstSimilar.confidenceScore}%
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                    Why this may matter: corroborating language, approvals, or follow-up patterns from prior work—not a verdict on routing.
                  </p>
                  <p className="mt-1 text-sm font-semibold leading-snug text-slate-900 dark:text-slate-50">
                    {firstSimilar.title}
                  </p>
                  <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500 dark:text-slate-400">
                    <span>{firstSimilar.sourceTicketId}</span>
                    <span>{formatDisplayDateTime(firstSimilar.createdDate)}</span>
                    {onOpenSourceTicket ? (
                      <button
                        type="button"
                        onClick={() => void onOpenSourceTicket(firstSimilar.sourceTicketId)}
                        className="font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                      >
                        View similar ticket
                      </button>
                    ) : (
                      <a
                        href={firstSimilar.sourceUrl}
                        className="font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                      >
                        View similar ticket
                      </a>
                    )}
                  </div>
                  {firstSimilar.sourceQuote ? (
                    <>
                      <p className="mt-2 text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                        Relevant quote
                      </p>
                      <blockquote className="mt-1 border-l-2 border-cortex-blue/35 pl-3 text-sm leading-relaxed text-slate-700 dark:border-cortex-cyan/35 dark:text-slate-200">
                        {firstSimilar.sourceQuote}
                      </blockquote>
                    </>
                  ) : null}
                </div>
              ) : null}

              {insight.unavailable ? (
                <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100">
                  {insight.unavailableReason ||
                    "Cortex found similar tickets, but the summary is unavailable."}
                </p>
              ) : null}

              {hasGeneratedFields ? (
                <div className="grid gap-3 sm:grid-cols-2">
                  <InsightField label="Summary" value={insight.summary} />
                  <InsightField label="Resolution" value={insight.resolution} />
                  <InsightField label="Root cause" value={insight.rootCause} />
                  <InsightField
                    label="Suggested next step (hint only)"
                    value={insight.suggestedNextStep}
                  />
                </div>
              ) : null}

              {matches.length > 1 ? (
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Additional similar tickets
                  </p>
                  <ul className="mt-2 space-y-1.5 text-sm text-slate-700 dark:text-slate-200">
                    {matches.slice(1).map((ticket) => (
                      <li
                        key={ticket.id}
                        className="rounded-md border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/35"
                      >
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          <span className="font-medium text-slate-900 dark:text-slate-50">
                            {ticket.title}
                          </span>
                          <span className="text-slate-500 dark:text-slate-400">
                            {ticket.status || "—"} · Confidence {ticket.confidenceScore}%
                          </span>
                          {onOpenSourceTicket ? (
                            <button
                              type="button"
                              onClick={() => void onOpenSourceTicket(ticket.sourceTicketId)}
                              className="text-xs font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                            >
                              View similar ticket
                            </button>
                          ) : (
                            <a
                              href={ticket.sourceUrl}
                              className="text-xs font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                            >
                              View similar ticket
                            </a>
                          )}
                        </div>
                        {ticket.sourceQuote ? (
                          <blockquote className="mt-2 border-l-2 border-slate-200 pl-3 text-sm leading-relaxed text-slate-600 dark:border-slate-700 dark:text-slate-300">
                            {ticket.sourceQuote}
                          </blockquote>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}

              {learningSignals.length > 0 ? (
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Past resolution signals
                  </p>
                  <div className="mt-2 space-y-2">
                    {learningSignals.map((signal, index) => (
                      <LearningSignalCard
                        key={`${signal.signalType}-${index}-${signal.title.slice(0, 24)}`}
                        signal={signal}
                      />
                    ))}
                  </div>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
