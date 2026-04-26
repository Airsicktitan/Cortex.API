import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ticketService } from "../services/api";
import type { CortexInsight } from "../types/cortexInsight";
import { formatDisplayDateTime } from "../utils/presentation";

const API_AUDIENCE = "https://cortex-api";

interface CortexInsightPanelProps {
  ticketId: string;
  isOpen: boolean;
  onOpenSourceTicket?: (ticketId: string) => void | Promise<void>;
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

export default function CortexInsightPanel({
  ticketId,
  isOpen,
  onOpenSourceTicket,
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
    setLoading(false);
    setError(null);
  }, [ticketId, isOpen]);

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
      }
    } catch (err) {
      if (!controller.signal.aborted) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load Cortex Insight",
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
      return "Loading";
    }
    if (error) {
      return "Unavailable";
    }
    if (!insight) {
      return "Ready";
    }
    if (matches.length === 0) {
      return "No matches";
    }
    return `${matches.length} similar`;
  }, [error, insight, loading, matches.length]);

  return (
    <section className="rounded-md border border-slate-200 bg-slate-50/70 dark:border-slate-700 dark:bg-slate-900/45">
      <button
        type="button"
        onClick={handleToggle}
        aria-expanded={expanded}
        className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left"
      >
        <span>
          <span className="block text-sm font-semibold text-slate-900 dark:text-slate-50">
            Cortex Insight
          </span>
          <span className="mt-0.5 block text-xs text-slate-500 dark:text-slate-400">
            {statusText}
          </span>
        </span>
        <span
          className="shrink-0 text-xs font-semibold text-slate-500 dark:text-slate-400"
          aria-hidden="true"
        >
          {expanded ? "Hide" : "Show"}
        </span>
      </button>

      {expanded ? (
        <div className="border-t border-slate-200 px-4 py-4 dark:border-slate-700">
          {loading ? (
            <p className="text-sm text-slate-600 dark:text-slate-300" role="status">
              Loading Cortex Insight...
            </p>
          ) : error ? (
            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          ) : insight && matches.length === 0 ? (
            <p className="text-sm text-slate-600 dark:text-slate-300">
              No similar tickets found.
            </p>
          ) : insight ? (
            <div className="space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-semibold text-slate-900 dark:text-slate-50">
                  {matches.length} similar issue{matches.length === 1 ? "" : "s"} found
                </p>
                <span className="rounded-md bg-cortex-blue-soft px-2.5 py-1 text-xs font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
                  {Math.max(0, Math.min(100, insight.confidenceScore))}% confidence
                </span>
              </div>

              {insight.matchReasons.length > 0 ? (
                <div className="rounded-md border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/40">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Match Reasons
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
                      Strongest Source
                    </p>
                    <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                      {firstSimilar.status || "Unknown"}
                    </span>
                    <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                      {firstSimilar.confidenceScore}% match
                    </span>
                  </div>
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
                        Open source ticket
                      </button>
                    ) : (
                      <a
                        href={firstSimilar.sourceUrl}
                        className="font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                      >
                        Open source ticket
                      </a>
                    )}
                  </div>
                  {firstSimilar.sourceQuote ? (
                    <blockquote className="mt-2 border-l-2 border-cortex-blue/35 pl-3 text-sm leading-relaxed text-slate-700 dark:border-cortex-cyan/35 dark:text-slate-200">
                      {firstSimilar.sourceQuote}
                    </blockquote>
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
                    label="Suggested next step"
                    value={insight.suggestedNextStep}
                  />
                </div>
              ) : null}

              {matches.length > 1 ? (
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Other Matches
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
                            {ticket.status || "Unknown"} - {ticket.confidenceScore}% match
                          </span>
                          {onOpenSourceTicket ? (
                            <button
                              type="button"
                              onClick={() => void onOpenSourceTicket(ticket.sourceTicketId)}
                              className="text-xs font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                            >
                              Open
                            </button>
                          ) : (
                            <a
                              href={ticket.sourceUrl}
                              className="text-xs font-semibold text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
                            >
                              Open
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
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
