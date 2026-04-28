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

function buildPredictiveSignals(risk: CortexSlaRisk): string[] {
  const sources = [risk.slaStatus, ...risk.riskReasons, risk.recommendation]
    .filter((value) => value && value.trim().length > 0)
    .join(" ")
    .toLowerCase();
  const signals: string[] = [];

  if (sources.includes("priority") && (sources.includes("high") || sources.includes("critical"))) {
    signals.push("High priority");
  }

  if (
    sources.includes("sla") &&
    (sources.includes("overdue") || sources.includes("late"))
  ) {
    signals.push("SLA breach risk high");
  } else if (
    sources.includes("sla") &&
    (sources.includes("near") ||
      sources.includes("deadline") ||
      sources.includes("approach") ||
      sources.includes("due"))
  ) {
    signals.push("Near SLA deadline");
  }

  if (
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

  if (
    sources.includes("workload") ||
    sources.includes("capacity") ||
    sources.includes("overloaded") ||
    sources.includes("load")
  ) {
    signals.push("Owner workload high");
  }

  if (
    sources.includes("similar") ||
    sources.includes("follow-up") ||
    sources.includes("follow up") ||
    sources.includes("historical")
  ) {
    signals.push("Recent similar issues required follow-up");
  }

  if (signals.length === 0 && risk.riskLevel === "High") {
    signals.push("Near SLA deadline");
  }
  if (signals.length === 0 && risk.riskLevel === "Medium") {
    signals.push("Needs closer monitoring");
  }

  return Array.from(new Set(signals)).slice(0, 4);
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

function recommendedAttentionCopy(
  risk: CortexSlaRisk,
  signals: string[],
): string {
  if (signals.includes("Missing required detail")) {
    return "Request missing details now.";
  }
  if (signals.includes("Awaiting approval")) {
    return "Approve or return for detail.";
  }
  if (signals.includes("Owner workload high")) {
    return "Assign to an available owner.";
  }
  if (signals.includes("SLA breach risk high") || signals.includes("Near SLA deadline")) {
    return "Review before end of day.";
  }
  if (signals.includes("Recent similar issues required follow-up")) {
    return "Use follow-up checklist before assignment.";
  }
  switch (risk.riskLevel) {
    case "High":
      return "Review before end of day.";
    case "Medium":
      return "Review within the current shift.";
    default:
      return "Continue with normal monitoring.";
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

  const predictiveSignals = risk ? buildPredictiveSignals(risk) : [];

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
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Cortex Risk
          </p>
        </div>
        {risk ? (
          <span className={`rounded-md px-2.5 py-1 text-xs font-semibold ${levelClasses(risk.riskLevel)}`}>
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
        <div className="mt-3 space-y-4 text-sm text-slate-800 dark:text-slate-100">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Risk Status
            </p>
            <div className="mt-1 flex items-center gap-2">
              <span
                className={`rounded-md px-2.5 py-1 text-xs font-semibold ${riskStatusClass(
                  riskStatusLabel(risk),
                )}`}
              >
                {riskStatusLabel(risk)}
              </span>
              <span className="text-sm text-slate-600 dark:text-slate-300">
                {leadCopy(risk.riskLevel)}
              </span>
            </div>
          </div>

          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Signals
            </p>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700 dark:text-slate-200">
              {predictiveSignals.map((signal, index) => (
                <li key={`${index}-${signal}`}>{signal}</li>
              ))}
            </ul>
          </div>

          <div className="rounded-md border border-slate-100 bg-slate-50/70 px-3 py-2 dark:border-slate-800 dark:bg-slate-900/50">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Recommended attention
            </p>
            {onRecommendedActionClick ? (
              <button
                type="button"
                onClick={onRecommendedActionClick}
                className="mt-0.5 text-left text-sm font-semibold text-slate-900 underline-offset-2 hover:underline dark:text-slate-50"
              >
                {recommendedAttentionCopy(risk, predictiveSignals)}
              </button>
            ) : (
              <p className="mt-0.5 text-sm font-semibold text-slate-900 dark:text-slate-50">
                {recommendedAttentionCopy(risk, predictiveSignals)}
              </p>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
