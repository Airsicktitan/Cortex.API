import { useMemo, useState } from "react";
import type {
  ScheduledJob,
  ScheduledJobType,
  UpsertScheduledJobInput,
} from "../types/scheduledJob";
import type { StoredProcedureDefinition } from "../types/storedProcedure";

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

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving..." : "Creating...";
    }
    return editingId ? "Save Job" : "Create Job";
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

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
            Scheduled Jobs
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Define automation schedule and execution behavior.
          </p>
        </div>
        <button
          onClick={onRefresh}
          className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
        >
          Refresh
        </button>
      </div>

      {error && (
        <div className="mt-4 rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <div className="mt-6 grid gap-6 xl:grid-cols-[1.3fr_0.9fr]">
        <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-slate-800">
          {loading ? (
            <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
              Loading jobs...
            </div>
          ) : jobs.length === 0 ? (
            <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
              No jobs configured yet.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                  <tr>
                    <th className="px-4 py-3 font-medium">Job</th>
                    <th className="px-4 py-3 font-medium">Schedule</th>
                    <th className="px-4 py-3 font-medium">Last Run</th>
                    <th className="px-4 py-3 font-medium">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map((job) => (
                    <tr
                      key={job.id}
                      className="border-t border-gray-100 dark:border-slate-800"
                    >
                      <td className="px-4 py-3">
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {job.name}
                        </p>
                        <p className="text-xs text-gray-500 dark:text-slate-400">
                          {job.jobType === "ArchiveEligibleTickets"
                            ? "Archive Eligible Tickets"
                            : job.storedProcedureName || "Stored procedure"}
                        </p>
                      </td>
                      <td className="px-4 py-3">Every {formatInterval(job.intervalMinutes)}</td>
                      <td className="px-4 py-3">
                        <p>{job.lastRunStatus || "Never run"}</p>
                        <p className="text-xs text-gray-500 dark:text-slate-400">
                          {formatDateTime(job.lastRunDateUtc)}
                        </p>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-col gap-2">
                          <button
                            onClick={() => startEdit(job)}
                            className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => void onRunNow(job.id)}
                            disabled={runningJobId === job.id}
                            className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white hover:bg-cortex-blue-dark disabled:opacity-60"
                          >
                            {runningJobId === job.id ? "Running..." : "Run Now"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="rounded-lg border border-gray-200 p-5 dark:border-slate-800">
          <h4 className="text-base font-semibold text-gray-900 dark:text-slate-100">
            {editingId ? "Edit Job" : "Create Job"}
          </h4>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Manage scheduling, enablement, and execution behavior.
          </p>

          <div className="mt-4 space-y-3">
            <input
              type="text"
              placeholder="Job name"
              value={draft.name}
              onChange={(event) =>
                setDraft((current) => ({ ...current, name: event.target.value }))
              }
              className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
            />
            <textarea
              placeholder="Description (optional)"
              value={draft.description ?? ""}
              onChange={(event) =>
                setDraft((current) => ({ ...current, description: event.target.value }))
              }
              rows={3}
              className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
            />
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
              className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
            >
              <option value="ArchiveEligibleTickets">Archive Eligible Tickets</option>
              <option value="RunStoredProcedure">Run Stored Procedure</option>
            </select>
            {draft.jobType === "RunStoredProcedure" && (
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
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                <option value="">Select a stored procedure</option>
                {storedProcedures.map((definition) => (
                  <option key={definition.id} value={definition.id}>
                    {definition.name}
                  </option>
                ))}
              </select>
            )}
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
              className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
            />
            <label className="flex items-center gap-2 text-sm text-gray-700 dark:text-slate-300">
              <input
                type="checkbox"
                checked={draft.isEnabled}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, isEnabled: event.target.checked }))
                }
              />
              Job enabled
            </label>

            <div className="flex gap-2">
              <button
                onClick={() => void saveJob()}
                disabled={
                  saving ||
                  !draft.name.trim() ||
                  draft.intervalMinutes <= 0 ||
                  (draft.jobType === "RunStoredProcedure" &&
                    !draft.storedProcedureDefinitionId)
                }
                className="rounded-md bg-cortex-blue px-4 py-2 text-white hover:bg-cortex-blue-dark disabled:opacity-60"
              >
                {saveLabel}
              </button>
              <button
                onClick={resetForm}
                className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                Clear
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
