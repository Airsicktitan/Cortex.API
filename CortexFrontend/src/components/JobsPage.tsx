import { useMemo, useState } from "react";
import type {
  ScheduledJob,
  ScheduledJobType,
  UpsertScheduledJobInput,
} from "../types/scheduledJob";
import type { StoredProcedureDefinition } from "../types/storedProcedure";

interface JobsPageProps {
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

function getJobTypeLabel(job: ScheduledJob) {
  return job.jobType === "ArchiveEligibleTickets"
    ? "Archive eligible tickets"
    : job.storedProcedureName || "Stored procedure";
}

function getResultBadgeClass(status?: string) {
  switch (status) {
    case "Succeeded":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200";
    case "Failed":
      return "bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-200";
    default:
      return "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300";
  }
}

function getFailureGuidance(job: ScheduledJob) {
  const message = job.lastRunMessage?.trim() || "The scheduler reported a failure without a detailed message.";
  const normalizedMessage = message.toLowerCase();

  if (normalizedMessage.includes("stored procedure definition is missing")) {
    return {
      whatWentWrong:
        "This job points at a stored procedure definition that no longer exists or was never saved correctly.",
      howToFix:
        "Edit the job, choose a valid stored procedure from the Configuration registry, save it, and then run the job again.",
    };
  }

  if (normalizedMessage.includes("stored procedure definition is disabled")) {
    return {
      whatWentWrong:
        "The selected stored procedure is currently disabled, so the scheduler refused to execute it.",
      howToFix:
        "Go to Configuration, enable that stored procedure or select a different enabled one on the job, then retry the run.",
    };
  }

  if (normalizedMessage.includes("could not find stored procedure")) {
    return {
      whatWentWrong:
        "SQL Server could not find the stored procedure name registered for this job.",
      howToFix:
        "Verify the procedure exists in the database, check the schema-qualified name in Configuration, save the correction, and rerun the job.",
    };
  }

  if (
    normalizedMessage.includes("execute permission was denied") ||
    normalizedMessage.includes("permission was denied")
  ) {
    return {
      whatWentWrong:
        "The API database identity does not have permission to execute the configured stored procedure.",
      howToFix:
        "Grant EXECUTE permission on the procedure to the database user used by CORTEX, or run the job through an account with the correct privileges.",
    };
  }

  if (
    normalizedMessage.includes("archive after days must be greater than zero") ||
    normalizedMessage.includes("select at least one archive status")
  ) {
    return {
      whatWentWrong:
        "The archive job could not run because the current archive policy is invalid.",
      howToFix:
        "Open Configuration, correct the archive policy values, save them, and then rerun the failed job.",
    };
  }

  if (job.jobType === "ArchiveEligibleTickets") {
    return {
      whatWentWrong:
        "The archive scheduler hit a runtime error while trying to evaluate or move eligible tickets.",
      howToFix:
        "Review the archive policy, confirm the archive stored procedure path is working, and rerun the job once the configuration issue is corrected.",
    };
  }

  return {
    whatWentWrong: "The scheduled job failed during execution.",
    howToFix:
      "Review the failure message, verify the job configuration and dependencies, save any needed corrections, and use Run Now to test the fix.",
  };
}

export default function JobsPage({
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
}: JobsPageProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] = useState<UpsertScheduledJobInput>(EMPTY_DRAFT);
  const failedJobs = useMemo(
    () =>
      jobs
        .filter((job) => job.lastRunStatus === "Failed")
        .sort((left, right) => {
          const leftTime = left.lastRunDateUtc
            ? new Date(left.lastRunDateUtc).getTime()
            : 0;
          const rightTime = right.lastRunDateUtc
            ? new Date(right.lastRunDateUtc).getTime()
            : 0;

          return rightTime - leftTime;
        }),
    [jobs],
  );

  const selectableStoredProcedures = useMemo(() => {
    const selectedDefinition = storedProcedures.find(
      (definition) => definition.id === draft.storedProcedureDefinitionId,
    );

    if (
      selectedDefinition &&
      !selectedDefinition.isEnabled &&
      !storedProcedures.some(
        (definition) =>
          definition.id === selectedDefinition.id && definition.isEnabled,
      )
    ) {
      return [selectedDefinition, ...storedProcedures.filter(
        (definition) => definition.id !== selectedDefinition.id,
      )];
    }

    return storedProcedures;
  }, [draft.storedProcedureDefinitionId, storedProcedures]);

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
    if (!draft.name.trim() || draft.intervalMinutes <= 0) {
      return;
    }

    if (
      draft.jobType === "RunStoredProcedure" &&
      !draft.storedProcedureDefinitionId
    ) {
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
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Jobs
            </h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Create and manage the automated jobs that keep CORTEX running in the background.
            </p>
            <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
              Archive jobs use your archive policy. If you want browser tabs to auto-refresh,
              that should be a separate UI polling feature rather than a backend scheduled job.
            </p>
          </div>

          <button
            onClick={onRefresh}
            className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
          >
            Refresh
          </button>
        </div>
      </section>

      {error && (
        <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {!loading && failedJobs.length > 0 && (
        <section
          id="failed-jobs-queue"
          className="rounded-lg border border-red-200 bg-red-50/70 p-6 dark:border-red-900/40 dark:bg-red-950/20"
        >
          <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
            <div>
              <h3 className="text-lg font-semibold text-red-900 dark:text-red-100">
                Failed Jobs Queue
              </h3>
              <p className="mt-1 text-sm text-red-800/80 dark:text-red-200/80">
                {failedJobs.length === 1
                  ? "1 job needs attention before it can run cleanly again."
                  : `${failedJobs.length} jobs need attention before they can run cleanly again.`}
              </p>
            </div>
            <span className="inline-flex w-fit rounded-full bg-red-100 px-3 py-1 text-sm font-medium text-red-800 dark:bg-red-950/40 dark:text-red-200">
              {failedJobs.length} failed
            </span>
          </div>

          <div className="mt-5 space-y-4">
            {failedJobs.map((job) => {
              const guidance = getFailureGuidance(job);

              return (
                <article
                  key={`failed-${job.id}`}
                  className="rounded-lg border border-red-200 bg-white p-5 shadow-sm dark:border-red-900/40 dark:bg-slate-900"
                >
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div>
                      <div className="flex flex-wrap items-center gap-3">
                        <h4 className="text-base font-semibold text-gray-900 dark:text-slate-100">
                          {job.name}
                        </h4>
                        <span className="inline-flex rounded-full bg-red-100 px-3 py-1 text-xs font-medium text-red-800 dark:bg-red-950/40 dark:text-red-200">
                          Failed
                        </span>
                      </div>
                      <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                        {getJobTypeLabel(job)} · Last failed{" "}
                        {formatDateTime(job.lastRunDateUtc)}
                      </p>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <button
                        onClick={() => startEdit(job)}
                        className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        Edit Job
                      </button>
                      <button
                        onClick={() => void onRunNow(job.id)}
                        disabled={runningJobId === job.id}
                        className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
                      >
                        {runningJobId === job.id ? "Running..." : "Retry Now"}
                      </button>
                    </div>
                  </div>

                  <div className="mt-4 grid gap-4 lg:grid-cols-2">
                    <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/40 dark:bg-red-950/30">
                      <p className="text-xs font-semibold uppercase tracking-wide text-red-700 dark:text-red-300">
                        What Went Wrong
                      </p>
                      <p className="mt-2 text-sm text-red-900 dark:text-red-100">
                        {guidance.whatWentWrong}
                      </p>
                      <p className="mt-3 text-xs text-red-800/80 dark:text-red-200/80">
                        Raw error: {job.lastRunMessage || "No failure message was captured."}
                      </p>
                    </div>

                    <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 dark:border-amber-900/40 dark:bg-amber-950/30">
                      <p className="text-xs font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-300">
                        How To Fix
                      </p>
                      <p className="mt-2 text-sm text-amber-900 dark:text-amber-100">
                        {guidance.howToFix}
                      </p>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        </section>
      )}

      <div className="grid gap-6 xl:grid-cols-[1.3fr_0.9fr]">
        <section className="overflow-hidden rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
          {loading ? (
            <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
              Loading jobs...
            </div>
          ) : jobs.length === 0 ? (
            <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
              No jobs created yet.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                  <tr>
                    <th className="px-4 py-3 font-medium">Job</th>
                    <th className="px-4 py-3 font-medium">Schedule</th>
                    <th className="px-4 py-3 font-medium">Next Run</th>
                    <th className="px-4 py-3 font-medium">Last Result</th>
                    <th className="px-4 py-3 font-medium">Run As</th>
                    <th className="px-4 py-3 font-medium">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map((job) => (
                    <tr
                      key={job.id}
                      className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                    >
                      <td className="px-4 py-3 align-top">
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {job.name}
                        </p>
                        <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                          {job.jobType === "ArchiveEligibleTickets"
                            ? "Archive eligible tickets"
                            : job.storedProcedureName || "Stored procedure"}
                        </p>
                        {job.description && (
                          <p className="mt-1 max-w-sm text-xs text-gray-500 dark:text-slate-400">
                            {job.description}
                          </p>
                        )}
                        <div className="mt-2">
                          <span
                            className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${
                              job.isEnabled
                                ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                                : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                            }`}
                          >
                            {job.isEnabled ? "Enabled" : "Disabled"}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 align-top whitespace-nowrap">
                        Every {formatInterval(job.intervalMinutes)}
                      </td>
                      <td className="px-4 py-3 align-top whitespace-nowrap">
                        {formatDateTime(job.nextRunDateUtc)}
                      </td>
                      <td className="px-4 py-3 align-top">
                        <span
                          className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${getResultBadgeClass(job.lastRunStatus)}`}
                        >
                          {job.lastRunStatus || "Never run"}
                        </span>
                        <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                          {formatDateTime(job.lastRunDateUtc)}
                        </p>
                        {job.lastRunMessage && (
                          <p className="mt-1 max-w-xs text-xs text-gray-500 dark:text-slate-400">
                            {job.lastRunMessage}
                          </p>
                        )}
                      </td>
                      <td className="px-4 py-3 align-top whitespace-nowrap">
                        {job.runAsDisplayName}
                      </td>
                      <td className="px-4 py-3 align-top">
                        <div className="flex flex-col gap-2">
                          <button
                            onClick={() => startEdit(job)}
                            className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => void onRunNow(job.id)}
                            disabled={runningJobId === job.id}
                            className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
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
        </section>

        <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                {editingId ? "Edit Job" : "Create Job"}
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Jobs run on a repeating interval and store their last result.
              </p>
            </div>

            <button
              onClick={resetForm}
              className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Clear
            </button>
          </div>

          <div className="mt-6 space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Job name
              </label>
              <input
                type="text"
                value={draft.name}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, name: event.target.value }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Nightly archive job"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Description
              </label>
              <textarea
                value={draft.description ?? ""}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    description: event.target.value,
                  }))
                }
                rows={3}
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Optional details for other admins."
              />
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
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
                  className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                >
                  <option value="ArchiveEligibleTickets">
                    Archive Eligible Tickets
                  </option>
                  <option value="RunStoredProcedure">Run Stored Procedure</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
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
                  className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                />
              </div>
            </div>

            {draft.jobType === "RunStoredProcedure" && (
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
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
                  className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                >
                  <option value="">Select a stored procedure</option>
                  {selectableStoredProcedures.map((definition) => (
                    <option key={definition.id} value={definition.id}>
                      {definition.name}
                      {definition.isEnabled ? "" : " (Disabled)"}
                    </option>
                  ))}
                </select>
                <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                  Stored procedures are managed from the Configuration page.
                </p>
              </div>
            )}

            <label className="flex items-start gap-3 rounded-md border border-gray-200 bg-gray-50 px-4 py-3 dark:border-slate-700 dark:bg-slate-950/40">
              <input
                type="checkbox"
                checked={draft.isEnabled}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    isEnabled: event.target.checked,
                  }))
                }
                className="mt-1 h-4 w-4"
              />
              <span>
                <span className="block font-medium text-gray-900 dark:text-slate-100">
                  Enabled
                </span>
                <span className="text-sm text-gray-500 dark:text-slate-400">
                  Disabled jobs stay saved but won’t be executed by the scheduler.
                </span>
              </span>
            </label>

            <button
              onClick={() => void saveJob()}
              disabled={
                saving ||
                !draft.name.trim() ||
                draft.intervalMinutes <= 0 ||
                (draft.jobType === "RunStoredProcedure" &&
                  !draft.storedProcedureDefinitionId)
              }
              className="w-full rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {saveLabel}
            </button>
          </div>
        </section>
      </div>
    </div>
  );
}
