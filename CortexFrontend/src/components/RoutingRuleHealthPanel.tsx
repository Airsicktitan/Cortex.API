import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { RoutingRuleHealthRow } from "../types/routingRuleHealth";
import { getUserFacingErrorMessage } from "../services/api";
import { ticketRoutingService } from "../services/ticketRoutingService";
import { ConfigDetailCard, ConfigErrorBanner } from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

function healthBadgeClass(status: string): string {
  switch (status) {
    case "Healthy":
      return "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/35 dark:text-emerald-100";
    case "Watch":
      return "bg-amber-100 text-amber-900 dark:bg-amber-950/35 dark:text-amber-100";
    case "NeedsReview":
      return "bg-rose-100 text-rose-900 dark:bg-rose-950/35 dark:text-rose-100";
    case "InsufficientData":
      return "bg-slate-200 text-slate-800 dark:bg-slate-700 dark:text-slate-100";
    default:
      return "bg-gray-200 text-gray-800 dark:bg-slate-700 dark:text-slate-100";
  }
}

function formatWhen(isoUtc: string | null): string {
  if (!isoUtc) {
    return "—";
  }

  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(isoUtc));
  } catch {
    return isoUtc;
  }
}

function formatPct(p: number): string {
  if (Number.isNaN(p)) {
    return "—";
  }
  return `${p.toFixed(1)}%`;
}

/** Display-only label; API still sends enums like NeedsReview / InsufficientData. */
function displayHealthLabel(apiStatus: string): string {
  switch (apiStatus) {
    case "InsufficientData":
      return "Insufficient data";
    case "NeedsReview":
      return "Needs review";
    case "Healthy":
      return "Healthy";
    case "Watch":
      return "Watch";
    default:
      return apiStatus || "—";
  }
}

function formatSlaSuccessDisplay(sampleSize: number, slaSuccessPercent: number): string {
  if (sampleSize === 0) {
    return "—";
  }
  return formatPct(slaSuccessPercent);
}

interface Props {
  /** When routing rules refresh (e.g. save), callers can bump this so health reloads optional — parent may omit. */
  reloadKey?: number;
}

/** Advisory read-only view; does not change routing. */
export default function RoutingRuleHealthPanel({ reloadKey = 0 }: Props) {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const [rows, setRows] = useState<RoutingRuleHealthRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadHealth = useCallback(async () => {
    if (!isAuthenticated) {
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const overview = await ticketRoutingService.getRuleHealth(token);
      setRows(overview.rules ?? []);
    } catch (err) {
      console.error("Routing rule health load failed", err);
      setError(
        getUserFacingErrorMessage(err, "Unable to load routing rule health."),
      );
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [getAccessTokenSilently, isAuthenticated]);

  useEffect(() => {
    void loadHealth();
  }, [loadHealth, reloadKey]);

  const hasRows = useMemo(() => rows.length > 0, [rows.length]);

  return (
    <>
      <div className="mb-4 px-6">
        <p className="text-sm text-gray-600 dark:text-slate-400">
          Rule health is based on past routing decisions and ticket outcomes. It
          helps admins review rules but{" "}
          <span className="font-medium text-gray-800 dark:text-slate-200">
            does not change assignments automatically
          </span>
          .
        </p>
      </div>

      {error ? (
        <div className="px-6 pb-2">
          <ConfigErrorBanner>{error}</ConfigErrorBanner>
        </div>
      ) : null}

      <div className="px-6 pb-6">
        <ConfigDetailCard
          title="Routing rule health"
          subtitle="Operational learning · read-only advisory"
        >
          {loading ? (
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Loading rule health…
            </p>
          ) : !hasRows ? (
            <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50/60 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/35">
              <p className="text-sm font-medium text-gray-800 dark:text-slate-200">
                No rule health data yet.
              </p>
              <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
                Cortex will show rule health after routing decisions and outcomes
                are recorded.
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-[960px] w-full border-collapse text-left text-xs text-gray-800 dark:text-slate-100">
                <thead>
                  <tr className="border-b border-gray-200 text-[11px] uppercase tracking-wide text-gray-500 dark:border-slate-700 dark:text-slate-400">
                    <th className="py-2 pr-3 font-medium">Rule</th>
                    <th className="py-2 pr-3 font-medium">Board / priority</th>
                    <th className="py-2 pr-3 font-medium">Matches / Outcomes</th>
                    <th className="py-2 pr-3 font-medium">Overrides</th>
                    <th className="py-2 pr-3 font-medium">SLA success</th>
                    <th className="py-2 pr-3 font-medium">Returns / reassign</th>
                    <th className="py-2 pr-3 font-medium">Last matched</th>
                    <th className="py-2 pr-3 font-medium">Health</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr
                      key={row.ruleId}
                      className="border-b border-gray-100 dark:border-slate-800 align-top last:border-none"
                    >
                      <td className="py-2 pr-3">
                        <p className="font-medium">{row.ruleName}</p>
                        <p className="mt-0.5 text-[11px] text-gray-500 dark:text-slate-400">
                          {row.isEnabled ? "Enabled" : "Disabled"} · SLA breaches
                          observed: {row.slaBreachedCount}
                        </p>
                      </td>
                      <td className="py-2 pr-3 text-xs">
                        <div>{row.boardName || "—"}</div>
                        <div className="text-gray-500 dark:text-slate-400">
                          {row.priorityName || "Any priority"}
                        </div>
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {row.matchCount} / {row.sampleSize}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {formatPct(row.overridePercent)}
                        {row.overrideCount > 0 ? (
                          <span className="ml-1 text-[11px] text-gray-500 dark:text-slate-400">
                            ({row.overrideCount})
                          </span>
                        ) : null}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {formatSlaSuccessDisplay(
                          row.sampleSize,
                          row.slaSuccessPercent,
                        )}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {row.returnedForDetailCount} / {row.reassignedCount}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap text-gray-600 dark:text-slate-300">
                        {formatWhen(row.lastMatchedAtUtc)}
                      </td>
                      <td className="py-2 pr-3">
                        <span
                          className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-semibold ${healthBadgeClass(row.healthStatus)}`}
                        >
                          {displayHealthLabel(row.healthStatus)}
                        </span>
                        <p className="mt-1 text-[11px] text-gray-600 dark:text-slate-400">
                          {row.healthSummary}
                        </p>
                        <p className="mt-1 text-[11px] text-gray-600 dark:text-slate-400">
                          {row.recommendedAction}
                        </p>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="mt-3 text-[11px] text-gray-500 dark:text-slate-500">
                Matches are routing decisions. Outcomes are completed tickets
                used to judge SLA and override patterns.
              </p>
              <p className="mt-2 text-[11px] text-gray-500 dark:text-slate-500">
                Rule health is advisory. Cortex does not change routing rules
                automatically.
              </p>
            </div>
          )}
        </ConfigDetailCard>
      </div>
    </>
  );
}
