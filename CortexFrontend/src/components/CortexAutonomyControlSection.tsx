import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configFieldClass,
} from "./configurationAdminUi";
import { formatDisplayDateTime } from "../utils/presentation";
import { systemAutonomyService } from "../services/systemAutonomyService";
import type {
  CortexAutonomyRecentDecision,
  CortexAutonomySettings,
  CortexAutonomySummary,
  UpdateCortexAutonomySettingsInput,
} from "../types/cortexAutonomy";

const API_AUDIENCE = "https://cortex-api";

interface CortexAutonomyControlSectionProps {
  canEdit: boolean;
}

function clamp(value: number, min: number, max: number) {
  if (Number.isNaN(value)) {
    return min;
  }
  return Math.max(min, Math.min(max, value));
}

function modeBadgeClasses(mode: string) {
  switch (mode) {
    case "Active":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200";
    case "Shadow":
      return "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200";
    case "Disabled":
    default:
      return "bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-200";
  }
}

function resultBadgeClasses(result: string) {
  switch (result) {
    case "AutoApplied":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200";
    case "Eligible":
      return "bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200";
    case "Blocked":
    default:
      return "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200";
  }
}

function StatTile({
  label,
  value,
  hint,
}: {
  label: string;
  value: number;
  hint?: string;
}) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
        {label}
      </p>
      <p className="mt-1 text-2xl font-semibold tabular-nums text-gray-900 dark:text-slate-100">
        {value.toLocaleString()}
      </p>
      {hint ? (
        <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">{hint}</p>
      ) : null}
    </div>
  );
}

function formatPercent(value: number) {
  return `${Math.round(clamp(value, 0, 1) * 100)}%`;
}

function RecentDecisionRow({ row }: { row: CortexAutonomyRecentDecision }) {
  const owner =
    row.recommendedOwnerName?.trim() ||
    row.recommendedOwnerId?.trim() ||
    "—";
  return (
    <tr className="border-t border-gray-100 dark:border-slate-800">
      <td className="px-4 py-3 align-top">
        <div className="font-medium text-gray-900 dark:text-slate-100">
          {row.ticketId}
        </div>
        <div className="mt-0.5 line-clamp-1 text-xs text-gray-500 dark:text-slate-400">
          {row.ticketTitle?.trim() || "Ticket title unavailable"}
        </div>
      </td>
      <td className="px-4 py-3 align-top text-gray-700 dark:text-slate-200">
        {owner}
      </td>
      <td className="px-4 py-3 align-top text-gray-700 tabular-nums dark:text-slate-200">
        {formatPercent(row.confidence)}
      </td>
      <td className="px-4 py-3 align-top">
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${resultBadgeClasses(row.result)}`}
        >
          {row.resultLabel}
        </span>
      </td>
      <td className="px-4 py-3 align-top text-gray-600 dark:text-slate-300">
        {row.reasonSummary}
      </td>
      <td className="px-4 py-3 align-top text-xs text-gray-500 dark:text-slate-400">
        {formatDisplayDateTime(row.evaluatedAtUtc)}
      </td>
    </tr>
  );
}

interface DraftSettings {
  enabled: boolean;
  shadowMode: boolean;
  minConfidence: number;
}

function settingsToDraft(settings: CortexAutonomySettings): DraftSettings {
  return {
    enabled: settings.enabled,
    shadowMode: settings.shadowMode,
    minConfidence: settings.minConfidence,
  };
}

function draftMatchesSettings(
  draft: DraftSettings,
  settings: CortexAutonomySettings,
): boolean {
  return (
    draft.enabled === settings.enabled &&
    draft.shadowMode === settings.shadowMode &&
    Math.abs(draft.minConfidence - settings.minConfidence) < 1e-6
  );
}

export default function CortexAutonomyControlSection({
  canEdit,
}: CortexAutonomyControlSectionProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [summary, setSummary] = useState<CortexAutonomySummary | null>(null);
  const [draft, setDraft] = useState<DraftSettings | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedFlash, setSavedFlash] = useState<string | null>(null);

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const next = await systemAutonomyService.getSummary(token, signal);
        if (signal?.aborted) {
          return;
        }
        setSummary(next);
        setDraft(settingsToDraft(next.settings));
      } catch (err) {
        if (signal?.aborted) {
          return;
        }
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load Cortex autonomy state",
        );
      } finally {
        if (!signal?.aborted) {
          setLoading(false);
        }
      }
    },
    [getAccessTokenSilently],
  );

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);
    return () => controller.abort();
  }, [refresh]);

  const dirty = useMemo(() => {
    if (!summary || !draft) {
      return false;
    }
    return !draftMatchesSettings(draft, summary.settings);
  }, [draft, summary]);

  const handleSave = useCallback(async () => {
    if (!summary || !draft || !canEdit || !dirty) {
      return;
    }
    setSaving(true);
    setError(null);
    setSavedFlash(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const payload: UpdateCortexAutonomySettingsInput = {};
      if (draft.enabled !== summary.settings.enabled) {
        payload.enabled = draft.enabled;
      }
      if (draft.shadowMode !== summary.settings.shadowMode) {
        payload.shadowMode = draft.shadowMode;
      }
      if (
        Math.abs(draft.minConfidence - summary.settings.minConfidence) >
        1e-6
      ) {
        payload.minConfidence = draft.minConfidence;
      }
      const updatedSettings = await systemAutonomyService.updateConfig(
        payload,
        token,
      );
      setSummary({ ...summary, settings: updatedSettings });
      setDraft(settingsToDraft(updatedSettings));
      setSavedFlash("Saved.");
      // Refresh full summary so the Mode badge + counts reflect any side-effects.
      void refresh();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to update Cortex autonomy config",
      );
    } finally {
      setSaving(false);
    }
  }, [canEdit, dirty, draft, getAccessTokenSilently, refresh, summary]);

  const handleResetDraft = useCallback(() => {
    if (summary) {
      setDraft(settingsToDraft(summary.settings));
      setSavedFlash(null);
    }
  }, [summary]);

  const settings = summary?.settings;
  const counts = summary?.counts;
  const mode = settings?.mode ?? "Disabled";
  const minConfidencePercent = draft
    ? Math.round(clamp(draft.minConfidence, 0, 1) * 100)
    : 85;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Cortex Safe Autonomy"
        description="Cortex safely applies decisions when confidence is high. Human override is always respected. Only low-risk assignment routing is in scope."
        actions={
          <ConfigSecondaryButton
            onClick={() => void refresh()}
            disabled={loading}
          >
            {loading ? "Refreshing…" : "Refresh"}
          </ConfigSecondaryButton>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      <ConfigPageBody>
        <div className="space-y-6">
          <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-gray-50/60 p-4 dark:border-slate-700 dark:bg-slate-800/40 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <span
                className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold ${modeBadgeClasses(mode)}`}
              >
                {mode === "Active"
                  ? "Active — Cortex may apply assignments"
                  : mode === "Shadow"
                    ? "Shadow mode — Cortex evaluates but never mutates"
                    : "Disabled — Cortex does not evaluate or apply"}
              </span>
              {settings?.lastModifiedDateUtc ? (
                <span className="text-xs text-gray-500 dark:text-slate-400">
                  Last changed {formatDisplayDateTime(settings.lastModifiedDateUtc)}
                  {settings.lastModifiedByDisplayName
                    ? ` by ${settings.lastModifiedByDisplayName}`
                    : ""}
                </span>
              ) : null}
            </div>
            <p className="max-w-md text-xs text-gray-500 dark:text-slate-400">
              Priority, status, SLA, and approval are never auto-mutated. Only
              assignment routing is in scope.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile
              label="Evaluated (24h)"
              value={counts?.evaluated ?? 0}
              hint="Tickets Cortex looked at"
            />
            <StatTile
              label="Eligible (24h)"
              value={counts?.eligible ?? 0}
              hint="Met every safety check"
            />
            <StatTile
              label="Auto-applied (24h)"
              value={counts?.autoApplied ?? 0}
              hint="Assignments Cortex safely applied"
            />
            <StatTile
              label="Blocked (24h)"
              value={counts?.blocked ?? 0}
              hint="Kept as recommendation only"
            />
          </div>

          <ConfigDetailCard
            title="Controls"
            subtitle="Take effect on the next evaluation. Existing tickets are not touched."
          >
            <div className="grid gap-4 lg:grid-cols-2">
              <div className="space-y-3">
                <label className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    checked={draft?.enabled ?? false}
                    disabled={!canEdit || saving || !draft}
                    onChange={(event) =>
                      setDraft((prev) =>
                        prev ? { ...prev, enabled: event.target.checked } : prev,
                      )
                    }
                  />
                  <span>
                    <span className="block text-sm font-medium text-gray-900 dark:text-slate-100">
                      Autonomy enabled
                    </span>
                    <span className="block text-xs text-gray-500 dark:text-slate-400">
                      When off, Cortex never evaluates or applies — recommendations only.
                    </span>
                  </span>
                </label>
                <label className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-1 h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    checked={draft?.shadowMode ?? true}
                    disabled={!canEdit || saving || !draft}
                    onChange={(event) =>
                      setDraft((prev) =>
                        prev
                          ? { ...prev, shadowMode: event.target.checked }
                          : prev,
                      )
                    }
                  />
                  <span>
                    <span className="block text-sm font-medium text-gray-900 dark:text-slate-100">
                      Shadow mode
                    </span>
                    <span className="block text-xs text-gray-500 dark:text-slate-400">
                      Evaluate and record outcomes, but never mutate tickets.
                      Turn off only after a soak period.
                    </span>
                  </span>
                </label>
              </div>
              <div>
                <label
                  htmlFor="cortex-autonomy-confidence"
                  className="block text-sm font-medium text-gray-900 dark:text-slate-100"
                >
                  Min confidence threshold
                </label>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                  Cortex only acts when its confidence is at or above this value.
                </p>
                <div className="mt-2 flex items-center gap-3">
                  <input
                    id="cortex-autonomy-confidence"
                    type="range"
                    min={0.5}
                    max={0.99}
                    step={0.01}
                    value={draft?.minConfidence ?? 0.85}
                    disabled={!canEdit || saving || !draft}
                    onChange={(event) =>
                      setDraft((prev) =>
                        prev
                          ? { ...prev, minConfidence: Number(event.target.value) }
                          : prev,
                      )
                    }
                    className="h-2 flex-1 cursor-pointer appearance-none rounded-full bg-slate-200 disabled:opacity-50 dark:bg-slate-700"
                  />
                  <input
                    type="number"
                    min={0.5}
                    max={0.99}
                    step={0.01}
                    value={draft?.minConfidence ?? 0.85}
                    disabled={!canEdit || saving || !draft}
                    onChange={(event) =>
                      setDraft((prev) =>
                        prev
                          ? {
                              ...prev,
                              minConfidence: clamp(
                                Number(event.target.value),
                                0,
                                1,
                              ),
                            }
                          : prev,
                      )
                    }
                    className={`${configFieldClass} w-24 tabular-nums`}
                  />
                  <span className="w-12 text-right text-sm text-gray-700 tabular-nums dark:text-slate-300">
                    {minConfidencePercent}%
                  </span>
                </div>
              </div>
            </div>

            <div className="mt-4 flex flex-wrap items-center gap-3">
              <ConfigPrimaryButton
                onClick={() => void handleSave()}
                disabled={!canEdit || saving || !dirty}
              >
                {saving ? "Saving…" : "Save changes"}
              </ConfigPrimaryButton>
              {dirty ? (
                <ConfigSecondaryButton
                  onClick={handleResetDraft}
                  disabled={saving}
                >
                  Discard
                </ConfigSecondaryButton>
              ) : null}
              {!canEdit ? (
                <span className="text-xs text-gray-500 dark:text-slate-400">
                  Read-only — admin role required to change autonomy settings.
                </span>
              ) : null}
              {savedFlash && !dirty ? (
                <span className="text-xs text-emerald-600 dark:text-emerald-300">
                  {savedFlash}
                </span>
              ) : null}
            </div>
          </ConfigDetailCard>

          <ConfigDetailCard
            title="Recent decisions"
            subtitle="Latest 20 evaluations across the last 24 hours."
          >
            {loading && !summary ? (
              <p className="text-sm text-gray-500 dark:text-slate-400">
                Loading…
              </p>
            ) : summary && summary.recent.length === 0 ? (
              <p className="text-sm text-gray-500 dark:text-slate-400">
                No autonomy evaluations recorded in the last 24 hours.
              </p>
            ) : (
              <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                <table className="min-w-full text-left text-sm">
                  <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500 dark:bg-slate-800/80 dark:text-slate-300">
                    <tr>
                      <th className="px-4 py-3 font-medium">Ticket</th>
                      <th className="px-4 py-3 font-medium">Recommended owner</th>
                      <th className="px-4 py-3 font-medium">Confidence</th>
                      <th className="px-4 py-3 font-medium">Result</th>
                      <th className="px-4 py-3 font-medium">Reason</th>
                      <th className="px-4 py-3 font-medium">Evaluated</th>
                    </tr>
                  </thead>
                  <tbody>
                    {summary?.recent.map((row) => (
                      <RecentDecisionRow
                        key={`${row.ticketId}-${row.evaluatedAtUtc}`}
                        row={row}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </ConfigDetailCard>
        </div>
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
