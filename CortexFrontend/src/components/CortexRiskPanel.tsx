import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useState } from "react";
import { ticketService } from "../services/api";
import type { CortexRiskLevel, CortexSlaRisk } from "../types/cortexRisk";

const API_AUDIENCE = "https://cortex-api";

interface CortexRiskPanelProps {
  ticketId: string;
  isOpen: boolean;
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

function leadCopy(level: CortexRiskLevel): string {
  switch (level) {
    case "High":
      return "This ticket is likely to miss its SLA without intervention.";
    case "Medium":
      return "Based on current signals, this ticket may require attention.";
    default:
      return "Cortex does not currently detect risk on this ticket.";
  }
}

function immediateActionCopy(risk: CortexSlaRisk): string {
  const recommendation = risk.recommendation?.trim();
  if (recommendation) {
    return recommendation;
  }
  switch (risk.riskLevel) {
    case "High":
      return "Escalate due to critical risk.";
    case "Medium":
      return "Reassign to reduce SLA pressure.";
    default:
      return "Continue with current ownership and monitor SLA trend.";
  }
}

export default function CortexRiskPanel({
  ticketId,
  isOpen,
  onRiskReady,
  onRecommendedActionClick,
  highlightPanel = false,
}: CortexRiskPanelProps) {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const [risk, setRisk] = useState<CortexSlaRisk | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !ticketId || !isAuthenticated) {
      return;
    }

    const controller = new AbortController();
    let cancelled = false;

    (async () => {
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
          setError("Cortex risk signals are temporarily unavailable.");
        }
      } finally {
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

  return (
    <div
      id="cortex-risk-panel"
      className={`mb-4 rounded-lg border px-4 py-3 shadow-sm transition-colors ${
        highlightPanel
          ? "border-red-300 bg-red-50/40 dark:border-red-700 dark:bg-red-950/20"
          : "border-slate-200/90 bg-white dark:border-slate-700 dark:bg-slate-950/40"
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Cortex Risk
          </p>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
            Predictive signals based on SLA, intake, and workload data
          </p>
        </div>
        {risk ? (
          <span
            className={`rounded-md px-2.5 py-1 text-xs font-semibold ${levelClasses(
              risk.riskLevel,
            )}`}
          >
            {risk.riskLevel} risk
          </span>
        ) : null}
      </div>

      {loading && !risk ? (
        <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
          Evaluating risk signals…
        </p>
      ) : error ? (
        <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
          {error}
        </p>
      ) : risk ? (
        <div className="mt-3 space-y-3 text-sm text-slate-800 dark:text-slate-100">
          <p className="font-medium">{leadCopy(risk.riskLevel)}</p>

          {risk.riskReasons.length > 0 ? (
            <ul className="list-disc space-y-1 pl-5 text-sm text-slate-700 dark:text-slate-200">
              {risk.riskReasons.slice(0, 4).map((reason, index) => (
                <li key={`${index}-${reason.slice(0, 24)}`}>{reason}</li>
              ))}
            </ul>
          ) : null}

          <div className="rounded-md border border-slate-100 bg-slate-50/70 px-3 py-2 dark:border-slate-800 dark:bg-slate-900/50">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Recommended action
            </p>
            <button
              type="button"
              onClick={onRecommendedActionClick}
              className="mt-0.5 text-left text-sm font-semibold text-slate-900 underline-offset-2 hover:underline dark:text-slate-50"
            >
              {immediateActionCopy(risk)}
            </button>
            {risk.recommendationReason ? (
              <p className="mt-0.5 text-xs text-slate-600 dark:text-slate-300">
                {risk.recommendationReason}
              </p>
            ) : null}
          </div>

          <p className="text-[11px] text-slate-500 dark:text-slate-400">
            SLA status: {risk.slaStatus || "—"} · Confidence{" "}
            {Math.round((risk.confidence ?? 0) * 100)}% · Advisory only — no
            actions are taken automatically.
          </p>
        </div>
      ) : null}
    </div>
  );
}
