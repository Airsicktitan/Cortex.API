import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import type { TicketStatusDefinition } from "../types/ticketStatus";

interface ArchivePolicySectionProps {
  policies: ArchiveConfiguration[];
  selectedPolicy: ArchiveConfiguration | null;
  availableStatuses: TicketStatusDefinition[];
  loading: boolean;
  saving: boolean;
  deletingId: number | null;
  running: boolean;
  error: string | null;
  onRefresh: () => void;
  onNew: () => void;
  onSelect: (id: number) => void;
  onChange: <K extends keyof ArchiveConfiguration>(
    field: K,
    value: ArchiveConfiguration[K],
  ) => void;
  onSave: () => void;
  onDelete: () => void;
  onRunNow: () => void;
}

function describePolicy(policy: ArchiveConfiguration) {
  if (policy.eligibleStatuses.length === 0) {
    return `No statuses selected after ${policy.archiveAfterDays} day${
      policy.archiveAfterDays === 1 ? "" : "s"
    }`;
  }

  return `${policy.eligibleStatuses.join(" / ")} after ${policy.archiveAfterDays} day${
    policy.archiveAfterDays === 1 ? "" : "s"
  }`;
}

export default function ArchivePolicySection({
  policies,
  selectedPolicy,
  availableStatuses,
  loading,
  saving,
  deletingId,
  running,
  error,
  onRefresh,
  onNew,
  onSelect,
  onChange,
  onSave,
  onDelete,
  onRunNow,
}: ArchivePolicySectionProps) {
  const isBusy = saving || deletingId !== null;
  const isNewPolicy = selectedPolicy?.id === 0;

  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Archive Policies
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Create multiple rules for when tickets in selected statuses become eligible for archive.
            </p>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Ticket age is measured from the last updated date, or created date if it has never been updated.
            </p>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Saving a policy also keeps the background archive automation scheduled. You can fine-tune its interval from Jobs.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              onClick={onRefresh}
              disabled={isBusy}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Refresh
            </button>
            <button
              onClick={onNew}
              disabled={isBusy}
              className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              New Policy
            </button>
            <button
              onClick={onRunNow}
              disabled={running || loading || policies.length === 0}
              className="rounded-md bg-cortex-ink px-4 py-2 text-white transition-colors hover:bg-cortex-ink-dark disabled:opacity-60"
            >
              {running ? "Archiving..." : "Archive Eligible Now"}
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {loading ? (
        <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
          Loading archive policies...
        </div>
      ) : (
        <div className="grid gap-6 px-6 py-6 lg:grid-cols-[0.9fr_1.1fr]">
          <div className="space-y-3">
            {policies.length === 0 ? (
              <div className="rounded-lg border border-dashed border-gray-300 px-5 py-8 text-center text-sm text-gray-500 dark:border-slate-700 dark:text-slate-400">
                No archive policies have been added yet.
              </div>
            ) : (
              policies.map((policy) => {
                const isSelected =
                  selectedPolicy?.id === policy.id && selectedPolicy.id !== 0;

                return (
                  <button
                    key={policy.id}
                    onClick={() => onSelect(policy.id)}
                    disabled={isBusy}
                    className={`w-full rounded-lg border px-4 py-4 text-left transition-colors disabled:opacity-60 ${
                      isSelected
                        ? "border-cortex-blue bg-cortex-blue-soft/70 dark:border-cortex-cyan dark:bg-cortex-blue/15"
                        : "border-gray-200 hover:bg-gray-50 dark:border-slate-700 dark:hover:bg-slate-800/70"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          Archive Policy #{policy.id}
                        </p>
                        <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
                          {describePolicy(policy)}
                        </p>
                      </div>
                      <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                        {policy.archiveAfterDays}d
                      </span>
                    </div>
                  </button>
                );
              })
            )}
          </div>

          <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
            {selectedPolicy ? (
              <>
                <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
                  {isNewPolicy ? "New Archive Policy" : `Edit Policy #${selectedPolicy.id}`}
                </h4>

                <div className="mt-4 space-y-5">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Archive after (days)
                    </label>
                    <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                      Tickets older than this window become eligible when they match one of the statuses below.
                    </p>
                    <input
                      type="number"
                      min={1}
                      step={1}
                      value={selectedPolicy.archiveAfterDays}
                      onChange={(event) =>
                        onChange("archiveAfterDays", Number(event.target.value))
                      }
                      className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                    />
                  </div>

                  <div>
                    <p className="text-sm font-medium text-gray-700 dark:text-slate-300">
                      Eligible statuses
                    </p>
                    {availableStatuses.length === 0 ? (
                      <div className="mt-3 rounded-md border border-dashed border-gray-300 px-4 py-4 text-sm text-gray-500 dark:border-slate-700 dark:text-slate-400">
                        Add at least one ticket status before configuring archive rules.
                      </div>
                    ) : (
                      <div className="mt-3 space-y-3">
                        {availableStatuses.map((status) => {
                          const isSelected = selectedPolicy.eligibleStatuses.includes(
                            status.name,
                          );

                          return (
                            <label
                              key={status.id}
                              className="flex items-start gap-3 rounded-md border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900/60"
                            >
                              <input
                                type="checkbox"
                                checked={isSelected}
                                onChange={(event) =>
                                  onChange(
                                    "eligibleStatuses",
                                    event.target.checked
                                      ? [
                                          ...selectedPolicy.eligibleStatuses,
                                          status.name,
                                        ]
                                      : selectedPolicy.eligibleStatuses.filter(
                                          (value) => value !== status.name,
                                        ),
                                  )
                                }
                                className="mt-1 h-4 w-4"
                              />
                              <span>
                                <span className="block font-medium text-gray-900 dark:text-slate-100">
                                  {status.name}
                                  {!status.isEnabled && (
                                    <span className="ml-2 text-xs font-normal text-amber-600 dark:text-amber-300">
                                      Disabled
                                    </span>
                                  )}
                                </span>
                                <span className="text-sm text-gray-500 dark:text-slate-400">
                                  {status.description ||
                                    "Include tickets in this status when they age past the archive window."}
                                </span>
                              </span>
                            </label>
                          );
                        })}
                      </div>
                    )}
                  </div>

                  <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900/70">
                    <p className="text-sm font-medium text-gray-900 dark:text-slate-100">
                      {describePolicy(selectedPolicy)}
                    </p>
                  </div>

                  <div className="flex flex-wrap gap-3">
                    <button
                      onClick={onSave}
                      disabled={isBusy}
                      className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
                    >
                      {saving ? "Saving..." : isNewPolicy ? "Create Policy" : "Save Policy"}
                    </button>
                    <button
                      onClick={onDelete}
                      disabled={isBusy || isNewPolicy}
                      className="rounded-md border border-red-200 px-4 py-2 text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      {deletingId === selectedPolicy.id ? "Deleting..." : "Delete Policy"}
                    </button>
                  </div>
                </div>
              </>
            ) : (
              <div className="flex h-full min-h-56 items-center justify-center text-center text-sm text-gray-500 dark:text-slate-400">
                Select an archive policy or create a new one to get started.
              </div>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
