import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import type { SlaConfiguration } from "../types/sla";

interface ConfigurationPageProps {
  slaConfigurations: SlaConfiguration[];
  slaError: string | null;
  slaLoading: boolean;
  slaSaving: boolean;
  onSlaChange: (
    priority: string,
    field: "targetHours" | "warningHours",
    value: number,
  ) => void;
  onRefreshSla: () => void;
  onSaveSla: () => void;
  archiveConfiguration: ArchiveConfiguration | null;
  archiveError: string | null;
  archiveLoading: boolean;
  archiveSaving: boolean;
  archiveRunning: boolean;
  onArchiveChange: <K extends keyof ArchiveConfiguration>(
    field: K,
    value: ArchiveConfiguration[K],
  ) => void;
  onRefreshArchive: () => void;
  onSaveArchive: () => void;
  onRunArchiveNow: () => void;
}

export default function ConfigurationPage({
  slaConfigurations,
  slaError,
  slaLoading,
  slaSaving,
  onSlaChange,
  onRefreshSla,
  onSaveSla,
  archiveConfiguration,
  archiveError,
  archiveLoading,
  archiveSaving,
  archiveRunning,
  onArchiveChange,
  onRefreshArchive,
  onSaveArchive,
  onRunArchiveNow,
}: ConfigurationPageProps) {
  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div>
          <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
            Configuration
          </h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Manage the operational rules for SLA tracking and archive policy.
          </p>
        </div>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                SLA Configuration
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Set the SLA target and warning window for each ticket priority.
              </p>
            </div>

            <div className="flex gap-3">
              <button
                onClick={onRefreshSla}
                className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              >
                Refresh
              </button>
              <button
                onClick={onSaveSla}
                disabled={slaSaving || slaLoading}
                className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-blue-700 disabled:opacity-60"
              >
                {slaSaving ? "Saving..." : "Save SLA"}
              </button>
            </div>
          </div>
        </div>

        {slaError && (
          <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{slaError}</p>
          </div>
        )}

        <div className="grid grid-cols-[1.2fr_1fr_1fr] gap-4 bg-gray-50 px-6 py-4 text-sm font-medium text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
          <span>Priority</span>
          <span>SLA Target (hours)</span>
          <span>Warning Window (hours)</span>
        </div>

        {slaLoading ? (
          <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
            Loading SLA settings...
          </div>
        ) : (
          slaConfigurations.map((configuration) => (
            <div
              key={configuration.priority}
              className="grid grid-cols-[1.2fr_1fr_1fr] items-center gap-4 border-t border-gray-100 px-6 py-4 dark:border-slate-800"
            >
              <div>
                <p className="font-medium text-gray-900 dark:text-slate-100">
                  {configuration.priority}
                </p>
                <p className="text-sm text-gray-500 dark:text-slate-400">
                  Tickets turn yellow when they enter this warning window.
                </p>
              </div>

              <input
                type="number"
                min={1}
                step={1}
                value={configuration.targetHours}
                onChange={(event) =>
                  onSlaChange(
                    configuration.priority,
                    "targetHours",
                    Number(event.target.value),
                  )
                }
                className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              />

              <input
                type="number"
                min={0}
                step={1}
                value={configuration.warningHours}
                onChange={(event) =>
                  onSlaChange(
                    configuration.priority,
                    "warningHours",
                    Number(event.target.value),
                  )
                }
                className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              />
            </div>
          ))
        )}
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                Archive Policy
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Define when resolved or closed tickets become eligible for archive.
              </p>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Ticket age is measured from the last updated date, or created date if it
                has never been updated.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                onClick={onRefreshArchive}
                className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              >
                Refresh
              </button>
              <button
                onClick={onSaveArchive}
                disabled={archiveSaving || archiveLoading || !archiveConfiguration}
                className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-blue-700 disabled:opacity-60"
              >
                {archiveSaving ? "Saving..." : "Save Archive Policy"}
              </button>
              <button
                onClick={onRunArchiveNow}
                disabled={archiveRunning || archiveLoading || !archiveConfiguration}
                className="rounded-md bg-emerald-600 px-4 py-2 text-white transition-colors hover:bg-emerald-700 disabled:opacity-60"
              >
                {archiveRunning ? "Archiving..." : "Archive Eligible Now"}
              </button>
            </div>
          </div>
        </div>

        {archiveError && (
          <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{archiveError}</p>
          </div>
        )}

        {archiveLoading || !archiveConfiguration ? (
          <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
            Loading archive policy...
          </div>
        ) : (
          <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.2fr_1fr]">
            <div className="space-y-5">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                  Archive after (days)
                </label>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Resolved or closed tickets older than this window become eligible.
                </p>
                <input
                  type="number"
                  min={1}
                  step={1}
                  value={archiveConfiguration.archiveAfterDays}
                  onChange={(event) =>
                    onArchiveChange(
                      "archiveAfterDays",
                      Number(event.target.value),
                    )
                  }
                  className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                />
              </div>

              <div>
                <p className="text-sm font-medium text-gray-700 dark:text-slate-300">
                  Eligible statuses
                </p>
                <div className="mt-3 space-y-3">
                  <label className="flex items-start gap-3 rounded-md border border-gray-200 px-4 py-3 dark:border-slate-700">
                    <input
                      type="checkbox"
                      checked={archiveConfiguration.archiveResolvedTickets}
                      onChange={(event) =>
                        onArchiveChange(
                          "archiveResolvedTickets",
                          event.target.checked,
                        )
                      }
                      className="mt-1 h-4 w-4"
                    />
                    <span>
                      <span className="block font-medium text-gray-900 dark:text-slate-100">
                        Resolved
                      </span>
                      <span className="text-sm text-gray-500 dark:text-slate-400">
                        Include resolved tickets that are older than the archive window.
                      </span>
                    </span>
                  </label>

                  <label className="flex items-start gap-3 rounded-md border border-gray-200 px-4 py-3 dark:border-slate-700">
                    <input
                      type="checkbox"
                      checked={archiveConfiguration.archiveClosedTickets}
                      onChange={(event) =>
                        onArchiveChange(
                          "archiveClosedTickets",
                          event.target.checked,
                        )
                      }
                      className="mt-1 h-4 w-4"
                    />
                    <span>
                      <span className="block font-medium text-gray-900 dark:text-slate-100">
                        Closed
                      </span>
                      <span className="text-sm text-gray-500 dark:text-slate-400">
                        Include closed tickets that are older than the archive window.
                      </span>
                    </span>
                  </label>
                </div>
              </div>
            </div>

            <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
              <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
                Current Policy
              </h4>
              <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
                Tickets will be eligible after{" "}
                <span className="font-medium text-gray-900 dark:text-slate-100">
                  {archiveConfiguration.archiveAfterDays} day
                  {archiveConfiguration.archiveAfterDays === 1 ? "" : "s"}
                </span>{" "}
                if they are in the selected final statuses below.
              </p>

              <div className="mt-4 flex flex-wrap gap-2">
                {archiveConfiguration.archiveResolvedTickets && (
                  <span className="inline-flex rounded-full bg-blue-100 px-3 py-1 text-sm text-blue-800 dark:bg-blue-950/40 dark:text-blue-200">
                    Resolved
                  </span>
                )}
                {archiveConfiguration.archiveClosedTickets && (
                  <span className="inline-flex rounded-full bg-slate-200 px-3 py-1 text-sm text-slate-800 dark:bg-slate-800 dark:text-slate-200">
                    Closed
                  </span>
                )}
                {!archiveConfiguration.archiveResolvedTickets &&
                  !archiveConfiguration.archiveClosedTickets && (
                    <span className="text-sm text-red-600 dark:text-red-300">
                      No statuses selected.
                    </span>
                  )}
              </div>

              <p className="mt-5 text-sm text-gray-500 dark:text-slate-400">
                Use <span className="font-medium">Archive Eligible Now</span> to move all
                currently eligible tickets into the Archived Tickets view in one pass.
              </p>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
