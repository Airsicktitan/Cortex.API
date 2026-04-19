import { useMemo, useState } from "react";
import type {
  ScheduledJob,
  ScheduledJobType,
  UpsertScheduledJobInput,
} from "../types/scheduledJob";
import type { StoredProcedureDefinition } from "../types/storedProcedure";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  ConfigTwoColumnWideCatalog,
  configCatalogItemClass,
  configFieldClass,
} from "./configurationAdminUi";

interface ScheduledJobAdminSectionProps {
  jobs: ScheduledJob[];
  storedProcedures: StoredProcedureDefinition[];
  loading: boolean;
  error: string | null;
  saving: boolean;
  runningJobId: number | null;
  onRefresh: () => void;
  onCreate: (job: UpsertScheduledJobInput) => Promise<void>;
  onUpdate: (id: number, job: UpsertScheduledJobInput) => Promise<void>;
  onRunNow: (id: number) => Promise<void>;
}

const EMPTY_DRAFT: UpsertScheduledJobInput = {
  name: "",
  description: "",
  jobType: "ArchiveEligibleTickets",
  intervalMinutes: 60,
  isEnabled: true,
  storedProcedureDefinitionId: undefined,
};

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

function formatInterval(intervalMinutes: number) {
  if (intervalMinutes % 1440 === 0) {
    const days = intervalMinutes / 1440;
    return `${days} day${days === 1 ? "" : "s"}`;
  }

  if (intervalMinutes % 60 === 0) {
    const hours = intervalMinutes / 60;
    return `${hours} hour${hours === 1 ? "" : "s"}`;
  }

  return `${intervalMinutes} minute${intervalMinutes === 1 ? "" : "s"}`;
}

export default function ScheduledJobAdminSection({
  jobs,
  storedProcedures,
  loading,
  error,
  saving,
  runningJobId,
  onRefresh,
  onCreate,
  onUpdate,
  onRunNow,
}: ScheduledJobAdminSectionProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] = useState<UpsertScheduledJobInput>(EMPTY_DRAFT);

  const isBusy = saving || runningJobId !== null;

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving…" : "Creating…";
    }
    return editingId ? "Save changes" : "Create job";
  }, [editingId, saving]);

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
  };

  const startEdit = (job: ScheduledJob) => {
    setEditingId(job.id);
    setDraft({
      name: job.name,
      description: job.description ?? "",
      jobType: job.jobType,
      intervalMinutes: job.intervalMinutes,
      isEnabled: job.isEnabled,
      storedProcedureDefinitionId: job.storedProcedureDefinitionId,
    });
  };

  const saveJob = async () => {
    if (!draft.name.trim() || draft.intervalMinutes <= 0) return;
    if (draft.jobType === "RunStoredProcedure" && !draft.storedProcedureDefinitionId) {
      return;
    }

    const payload: UpsertScheduledJobInput = {
      ...draft,
      name: draft.name.trim(),
      description: draft.description?.trim() || undefined,
      storedProcedureDefinitionId:
        draft.jobType === "RunStoredProcedure"
          ? draft.storedProcedureDefinitionId
          : undefined,
    };

    if (editingId) {
      await onUpdate(editingId, payload);
    } else {
      await onCreate(payload);
    }
    resetForm();
  };

  const isNewMode = editingId === null;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Scheduled jobs"
        description="Automated tasks on a fixed interval (archive, stored procedures, and more)."
        actions={
          <>
            <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
              New job
            </ConfigPrimaryButton>
            <ConfigGhostButton onClick={onRefresh} disabled={isBusy}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      <ConfigPageBody>
        <ConfigTwoColumnWideCatalog
          left={
            <div className="flex min-h-[200px] flex-col gap-2">
              {loading ? (
                <p className="py-8 text-center text-sm text-gray-500 dark:text-slate-400">Loading jobs…</p>
              ) : jobs.length === 0 ? (
                <div className="flex flex-1 flex-col justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                  <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No jobs yet</p>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                    Create a job to run on a schedule.
                  </p>
                  <div className="mt-4 flex justify-center">
                    <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
                      New job
                    </ConfigPrimaryButton>
                  </div>
                </div>
              ) : (
                <ul className="max-h-[min(420px,50vh)] space-y-1 overflow-y-auto pr-0.5">
                  {jobs.map((job) => {
                    const selected = editingId === job.id;
                    return (
                      <li key={job.id}>
                        <div
                          className={`rounded-lg border px-3 py-2.5 ${configCatalogItemClass(selected)}`}
                        >
                          <button
                            type="button"
                            onClick={() => startEdit(job)}
                            disabled={isBusy}
                            className="w-full text-left text-sm disabled:opacity-50"
                          >
                            <div className="flex items-start justify-between gap-2">
                              <span
                                className={`font-medium ${
                                  selected
                                    ? "text-cortex-blue dark:text-cortex-cyan"
                                    : "text-gray-900 dark:text-slate-100"
                                }`}
                              >
                                {job.name}
                              </span>
                              <span
                                className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                                  job.isEnabled
                                    ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                    : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                                }`}
                              >
                                {job.isEnabled ? "On" : "Off"}
                              </span>
                            </div>
                            <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                              Every {formatInterval(job.intervalMinutes)} ·{" "}
                              {job.lastRunStatus || "Never run"} · {formatDateTime(job.lastRunDateUtc)}
                            </p>
                          </button>
                          <div className="mt-2 flex gap-2 border-t border-gray-100 pt-2 dark:border-slate-700">
                            <ConfigSecondaryButton
                              className="flex-1 px-2 py-1.5 text-xs"
                              onClick={() => void onRunNow(job.id)}
                              disabled={runningJobId === job.id}
                            >
                              {runningJobId === job.id ? "Running…" : "Run now"}
                            </ConfigSecondaryButton>
                          </div>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          }
          right={
            <div className="min-w-0 space-y-4">
              <ConfigDetailCard title={isNewMode ? "New job" : "Edit job"}>
                <div className="space-y-3">
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Name
                    </label>
                    <input
                      type="text"
                      value={draft.name}
                      onChange={(event) =>
                        setDraft((current) => ({ ...current, name: event.target.value }))
                      }
                      className={configFieldClass}
                      placeholder="Job name"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Description
                    </label>
                    <textarea
                      value={draft.description ?? ""}
                      onChange={(event) =>
                        setDraft((current) => ({ ...current, description: event.target.value }))
                      }
                      rows={3}
                      className={configFieldClass}
                      placeholder="Optional"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Job type
                    </label>
                    <select
                      value={draft.jobType}
                      onChange={(event) =>
                        setDraft((current) => ({
                          ...current,
                          jobType: event.target.value as ScheduledJobType,
                          storedProcedureDefinitionId:
                            event.target.value === "RunStoredProcedure"
                              ? current.storedProcedureDefinitionId
                              : undefined,
                        }))
                      }
                      className={configFieldClass}
                    >
                      <option value="ArchiveEligibleTickets">Archive eligible tickets</option>
                      <option value="RunStoredProcedure">Run stored procedure</option>
                    </select>
                  </div>
                  {draft.jobType === "RunStoredProcedure" ? (
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Stored procedure
                      </label>
                      <select
                        value={draft.storedProcedureDefinitionId ?? ""}
                        onChange={(event) =>
                          setDraft((current) => ({
                            ...current,
                            storedProcedureDefinitionId: event.target.value
                              ? Number(event.target.value)
                              : undefined,
                          }))
                        }
                        className={configFieldClass}
                      >
                        <option value="">Select…</option>
                        {storedProcedures.map((definition) => (
                          <option key={definition.id} value={definition.id}>
                            {definition.name}
                          </option>
                        ))}
                      </select>
                    </div>
                  ) : null}
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Interval (minutes)
                    </label>
                    <input
                      type="number"
                      min={1}
                      step={1}
                      value={draft.intervalMinutes}
                      onChange={(event) =>
                        setDraft((current) => ({
                          ...current,
                          intervalMinutes: Number(event.target.value),
                        }))
                      }
                      className={configFieldClass}
                    />
                  </div>
                </div>
              </ConfigDetailCard>

              <ConfigDetailCard title="Status">
                <label className="flex cursor-pointer items-center gap-3">
                  <input
                    type="checkbox"
                    className="rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    checked={draft.isEnabled}
                    onChange={(event) =>
                      setDraft((current) => ({ ...current, isEnabled: event.target.checked }))
                    }
                  />
                  <span className="text-sm text-gray-800 dark:text-slate-200">Job is enabled</span>
                </label>
              </ConfigDetailCard>

              <ConfigDetailCard title="Actions">
                <div className="flex flex-wrap gap-2">
                  <ConfigPrimaryButton
                    onClick={() => void saveJob()}
                    disabled={
                      saving ||
                      !draft.name.trim() ||
                      draft.intervalMinutes <= 0 ||
                      (draft.jobType === "RunStoredProcedure" &&
                        !draft.storedProcedureDefinitionId)
                    }
                  >
                    {saveLabel}
                  </ConfigPrimaryButton>
                  <ConfigSecondaryButton onClick={resetForm} disabled={isBusy}>
                    Clear
                  </ConfigSecondaryButton>
                </div>
              </ConfigDetailCard>
            </div>
          }
        />
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
