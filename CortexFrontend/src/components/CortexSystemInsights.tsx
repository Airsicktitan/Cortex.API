import { useCallback, useEffect, useState } from "react";
import { getUserFacingErrorMessage, systemService } from "../services/api";
import type { CortexSystemRecommendation } from "../types/cortexSystemRecommendation";

interface CortexSystemInsightsProps {
  getApiToken: () => Promise<string>;
}

function confidenceBadgeClass(confidence: string): string {
  switch (confidence?.trim().toLowerCase()) {
    case "high":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-200";
    case "medium":
      return "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-200";
    default:
      return "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300";
  }
}

function severityBadgeClass(severity: string): string {
  switch (severity?.trim().toLowerCase()) {
    case "high":
      return "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-200";
    case "medium":
      return "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-200";
    default:
      return "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300";
  }
}

function statusBadgeClass(status: string): string {
  switch (status?.trim().toLowerCase()) {
    case "accepted":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-200";
    case "dismissed":
      return "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300";
    case "deferred":
      return "bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-200";
    default:
      return "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/30 dark:text-slate-100";
  }
}

export default function CortexSystemInsights({
  getApiToken,
}: CortexSystemInsightsProps) {
  const [recommendations, setRecommendations] = useState<CortexSystemRecommendation[]>(
    [],
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionInFlightId, setActionInFlightId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = await getApiToken();
      const result = await systemService.getRecommendations(token);
      setRecommendations(result);
    } catch (caughtError) {
      setError(
        getUserFacingErrorMessage(
          caughtError,
          "Unable to load Cortex system insights",
        ),
      );
      setRecommendations([]);
    } finally {
      setLoading(false);
    }
  }, [getApiToken]);

  useEffect(() => {
    void load();
  }, [load]);

  const setRecommendationStatus = useCallback(
    async (
      recommendation: CortexSystemRecommendation,
      action: "accept" | "dismiss" | "defer",
    ) => {
      setActionInFlightId(recommendation.id);
      setError(null);
      try {
        const token = await getApiToken();
        if (action === "accept") {
          await systemService.acceptRecommendation(recommendation.id, token);
        } else if (action === "dismiss") {
          await systemService.dismissRecommendation(
            recommendation.id,
            "Not relevant for current project",
            token,
          );
        } else {
          await systemService.deferRecommendation(recommendation.id, token);
        }
        await load();
      } catch (caughtError) {
        setError(
          getUserFacingErrorMessage(
            caughtError,
            "Unable to update recommendation status",
          ),
        );
      } finally {
        setActionInFlightId(null);
      }
    },
    [getApiToken, load],
  );

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
            ⚠️ Cortex System Insights
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            System-level recommendations based on historical routing outcomes.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void load()}
          disabled={loading}
          className="rounded-md bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700 transition-colors hover:bg-gray-200 disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
        >
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {loading && recommendations.length === 0 ? (
        <p className="mt-4 text-sm text-gray-500 dark:text-slate-400">
          Loading system recommendations...
        </p>
      ) : error ? (
        <p className="mt-4 text-sm text-red-700 dark:text-red-300">{error}</p>
      ) : recommendations.length === 0 ? (
        <p className="mt-4 text-sm text-gray-500 dark:text-slate-400">
          No system recommendations right now.
        </p>
      ) : (
        <div className="mt-4 space-y-3">
          {recommendations.map((recommendation, index) => (
            <article
              key={`${recommendation.type}-${recommendation.title}-${index}`}
              className={`rounded-md border px-4 py-3 ${
                recommendation.status === "Dismissed" || recommendation.status === "Deferred"
                  ? "border-slate-200 bg-slate-50/40 opacity-85 dark:border-slate-700 dark:bg-slate-950/25"
                  : "border-slate-200 bg-slate-50/70 dark:border-slate-700 dark:bg-slate-950/35"
              }`}
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h4 className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                  ⚠️ {recommendation.title}
                </h4>
                <div className="flex flex-wrap items-center gap-1.5">
                  <span
                    className={`rounded-md px-2 py-0.5 text-[11px] font-semibold ${severityBadgeClass(
                      recommendation.severity,
                    )}`}
                  >
                    {recommendation.severity} severity
                  </span>
                  <span
                    className={`rounded-md px-2 py-0.5 text-[11px] font-semibold ${confidenceBadgeClass(
                      recommendation.confidence,
                    )}`}
                  >
                    {recommendation.confidence} confidence
                  </span>
                  <span
                    className={`rounded-md px-2 py-0.5 text-[11px] font-semibold ${statusBadgeClass(
                      recommendation.status,
                    )}`}
                  >
                    {recommendation.status}
                  </span>
                </div>
              </div>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                Generated {new Date(recommendation.generatedAtUtc).toLocaleString()}
              </p>
              <p className="mt-1 text-sm text-slate-700 dark:text-slate-200">
                {recommendation.description}
              </p>
              <p className="mt-2 text-sm text-slate-800 dark:text-slate-100">
                <span className="font-semibold">Suggested change:</span>{" "}
                {recommendation.recommendation}
              </p>
              <p className="mt-1 text-sm text-slate-700 dark:text-slate-200">
                <span className="font-semibold">
                  {recommendation.actionLabel?.trim() || "Suggested action"}:
                </span>{" "}
                {recommendation.actionPreview}
              </p>
              {recommendation.dismissedReason?.trim() ? (
                <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                  Dismissed reason: {recommendation.dismissedReason}
                </p>
              ) : null}
              {recommendation.status === "Open" ? (
                <div className="mt-2 flex flex-wrap gap-2">
                  <button
                    type="button"
                    disabled={actionInFlightId === recommendation.id}
                    onClick={() => void setRecommendationStatus(recommendation, "accept")}
                    className="rounded-md bg-emerald-600 px-2.5 py-1 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
                  >
                    Accept
                  </button>
                  <button
                    type="button"
                    disabled={actionInFlightId === recommendation.id}
                    onClick={() => void setRecommendationStatus(recommendation, "dismiss")}
                    className="rounded-md bg-slate-700 px-2.5 py-1 text-xs font-semibold text-white hover:bg-slate-800 disabled:opacity-60 dark:bg-slate-600 dark:hover:bg-slate-500"
                  >
                    Dismiss
                  </button>
                  <button
                    type="button"
                    disabled={actionInFlightId === recommendation.id}
                    onClick={() => void setRecommendationStatus(recommendation, "defer")}
                    className="rounded-md bg-cortex-blue px-2.5 py-1 text-xs font-semibold text-white hover:bg-cortex-blue-dark disabled:opacity-60"
                  >
                    Defer
                  </button>
                </div>
              ) : null}
              {recommendation.supportingFacts.length > 0 ? (
                <details className="mt-2 rounded-md border border-slate-100 bg-white px-3 py-2 dark:border-slate-800 dark:bg-slate-900/40">
                  <summary className="cursor-pointer text-xs font-semibold text-cortex-blue-dark hover:text-cortex-blue dark:text-cortex-cyan">
                    Details
                  </summary>
                  <ul className="mt-2 list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
                    {recommendation.supportingFacts.map((fact, factIndex) => (
                      <li key={`${factIndex}-${fact.slice(0, 24)}`}>{fact}</li>
                    ))}
                  </ul>
                </details>
              ) : null}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
