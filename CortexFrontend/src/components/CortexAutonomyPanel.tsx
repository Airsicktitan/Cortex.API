import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { ticketService } from "../services/api";
import type { CortexAutonomyResult } from "../types/cortexAutonomy";
import { formatDisplayDateTime } from "../utils/presentation";

const API_AUDIENCE = "https://cortex-api";

interface CortexAutonomyPanelProps {
  ticketId: string;
  isOpen: boolean;
}

type ModeAppearance = {
  badgeClasses: string;
  headline: string;
  description: string;
};

function modeAppearance(
  result: CortexAutonomyResult | null,
): ModeAppearance | null {
  if (!result) {
    return null;
  }

  const ownerLabel =
    result.recommendedOwnerName?.trim() ||
    result.recommendedOwnerId?.trim() ||
    "the recommended owner";

  if (result.wasAutoApplied) {
    return {
      badgeClasses:
        "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200",
      headline: "Cortex auto-applied this assignment.",
      description: `Assignment safely routed to ${ownerLabel} after every check passed.`,
    };
  }

  if (result.isEligible) {
    if (result.mode === "Disabled") {
      return {
        badgeClasses:
          "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200",
        headline: "Auto-apply eligible (autonomy currently disabled).",
        description: `Cortex would safely auto-assign this ticket to ${ownerLabel} once autonomy is enabled.`,
      };
    }
    return {
      badgeClasses:
        "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200",
      headline: "Shadow mode: Cortex would auto-assign this.",
      description: `Cortex would safely auto-assign this ticket to ${ownerLabel}.`,
    };
  }

  return {
    badgeClasses:
      "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
    headline: "Cortex did not auto-apply.",
    description: "Cortex kept this as a recommendation for human review.",
  };
}

function ConfidenceBar({ value }: { value: number }) {
  const clamped = Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0));
  const percentLabel = `${Math.round(clamped * 100)}%`;
  return (
    <div className="flex items-center gap-3" aria-label={`Signal strength ${percentLabel}`}>
      <div className="h-2 w-32 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
        <div
          className="h-full rounded-full bg-cortex-blue dark:bg-emerald-400"
          style={{ width: percentLabel }}
        />
      </div>
      <span className="text-sm font-semibold text-slate-700 dark:text-slate-200">
        {percentLabel}
      </span>
    </div>
  );
}

function CheckList({
  title,
  items,
  tone,
}: {
  title: string;
  items: string[];
  tone: "positive" | "blocked";
}) {
  if (items.length === 0) {
    return null;
  }

  const bulletClasses =
    tone === "positive"
      ? "text-emerald-600 dark:text-emerald-300"
      : "text-amber-600 dark:text-amber-300";

  return (
    <div>
      <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
        {title}
      </p>
      <ul className="mt-2 space-y-1.5">
        {items.map((item) => (
          <li
            key={item}
            className="flex items-start gap-2 text-sm leading-relaxed text-slate-700 dark:text-slate-200"
          >
            <span className={`mt-1 inline-block h-1.5 w-1.5 shrink-0 rounded-full ${bulletClasses}`}>
              <span className="sr-only">{tone === "positive" ? "Pass" : "Blocked"}</span>
            </span>
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default function CortexAutonomyPanel({
  ticketId,
  isOpen,
}: CortexAutonomyPanelProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [result, setResult] = useState<CortexAutonomyResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const loadAutonomy = useCallback(async () => {
    if (!isOpen || !ticketId) {
      return;
    }

    const controller = new AbortController();
    abortRef.current?.abort();
    abortRef.current = controller;
    setLoading(true);
    setError(null);

    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const next = await ticketService.getAutonomy(
        ticketId,
        token,
        controller.signal,
      );
      if (!controller.signal.aborted) {
        setResult(next);
      }
    } catch (err) {
      if (!controller.signal.aborted) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load Cortex autonomy state",
        );
      }
    } finally {
      if (abortRef.current === controller) {
        abortRef.current = null;
        setLoading(false);
      }
    }
  }, [getAccessTokenSilently, isOpen, ticketId]);

  useEffect(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setResult(null);
    setError(null);
    setLoading(false);
    if (isOpen && ticketId) {
      void loadAutonomy();
    }
    return () => {
      abortRef.current?.abort();
      abortRef.current = null;
    };
  }, [isOpen, loadAutonomy, ticketId]);

  const handleEvaluate = useCallback(async () => {
    if (!ticketId || loading) {
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const next = await ticketService.evaluateAutonomy(ticketId, token);
      setResult(next);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to evaluate Cortex autonomy",
      );
    } finally {
      setLoading(false);
    }
  }, [getAccessTokenSilently, loading, ticketId]);

  if (!isOpen || !ticketId) {
    return null;
  }

  const appearance = modeAppearance(result);

  return (
    <section
      className="mb-6 rounded-md border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900"
      aria-label="Cortex Safe Autonomy"
    >
      <header className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            Cortex Safe Autonomy
          </h3>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
            Cortex safely evaluated this assignment. Mutation occurs only when
            an operator explicitly enables auto-apply.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void handleEvaluate()}
          disabled={loading}
          className="inline-flex items-center justify-center rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          {loading
            ? "Evaluating…"
            : result
              ? "Re-evaluate"
              : "Evaluate now"}
        </button>
      </header>

      {error ? (
        <p className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:bg-amber-950/40 dark:text-amber-200">
          {error}
        </p>
      ) : null}

      {!result && !loading && !error ? (
        <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
          No autonomy evaluation has been recorded for this ticket yet.
        </p>
      ) : null}

      {result ? (
        <div className="mt-4 space-y-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <span
                className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${appearance?.badgeClasses ?? ""}`}
              >
                {appearance?.headline ?? `Mode: ${result.mode}`}
              </span>
              <p className="text-sm leading-relaxed text-slate-700 dark:text-slate-200">
                {result.summary || appearance?.description}
              </p>
            </div>
            <div className="flex flex-col items-start gap-1 text-xs text-slate-500 dark:text-slate-400 sm:items-end">
              <span>Mode: {result.mode}</span>
              {result.evaluatedAtUtc ? (
                <span>
                  Evaluated {formatDisplayDateTime(result.evaluatedAtUtc)}
                </span>
              ) : null}
              {result.appliedAtUtc ? (
                <span>
                  Applied {formatDisplayDateTime(result.appliedAtUtc)}
                </span>
              ) : null}
            </div>
          </div>

          <div className="grid gap-4 rounded-md border border-slate-100 bg-slate-50 p-3 sm:grid-cols-2 dark:border-slate-800 dark:bg-slate-950/40">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Signal Strength
              </p>
              <div className="mt-2">
                <ConfidenceBar value={result.confidence} />
              </div>
              {typeof result.learningAdjustment === "number" &&
              result.learningAdjustment !== 0 ? (
                <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                  Learning adjustment: {result.learningAdjustment > 0 ? "+" : ""}
                  {(result.learningAdjustment * 100).toFixed(1)}%
                </p>
              ) : null}
            </div>
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Recommended owner
              </p>
              <p className="mt-1 text-sm text-slate-800 dark:text-slate-100">
                {result.recommendedOwnerName?.trim() ||
                  result.recommendedOwnerId?.trim() ||
                  "—"}
              </p>
              {result.previousOwnerId ? (
                <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                  Current owner: {result.previousOwnerId}
                </p>
              ) : null}
            </div>
          </div>

          <CheckList
            title="Passed checks"
            items={result.passedChecks}
            tone="positive"
          />
          <CheckList
            title="Blocked reasons"
            items={result.blockedReasons}
            tone="blocked"
          />
        </div>
      ) : null}
    </section>
  );
}
