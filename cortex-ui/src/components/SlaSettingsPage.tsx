import type { SlaConfiguration } from "../types/sla";

interface SlaSettingsPageProps {
  configurations: SlaConfiguration[];
  error: string | null;
  loading: boolean;
  saving: boolean;
  onChange: (
    priority: string,
    field: "targetHours" | "warningHours",
    value: number,
  ) => void;
  onRefresh: () => void;
  onSave: () => void;
}

export default function SlaSettingsPage({
  configurations,
  error,
  loading,
  saving,
  onChange,
  onRefresh,
  onSave,
}: SlaSettingsPageProps) {
  return (
    <div className="space-y-6">
      <section className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-6">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              SLA Configuration
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Set the SLA target and warning window for each ticket priority.
            </p>
            <p className="text-sm text-gray-500 dark:text-slate-400 mt-1">
              Green means in SLA, yellow means inside the warning window, and
              red means the ticket is overdue.
            </p>
          </div>

          <div className="flex gap-3">
            <button
              onClick={onRefresh}
              className="px-4 py-2 rounded-md bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 transition-colors"
            >
              Refresh
            </button>
            <button
              onClick={onSave}
              disabled={saving || loading}
              className="px-4 py-2 rounded-md bg-cortex-blue text-white hover:bg-blue-700 transition-colors disabled:opacity-60"
            >
              {saving ? "Saving..." : "Save SLA Settings"}
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <section className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 overflow-hidden">
        <div className="grid grid-cols-[1.2fr_1fr_1fr] gap-4 px-6 py-4 bg-gray-50 dark:bg-slate-800/80 text-sm font-medium text-gray-600 dark:text-slate-300">
          <span>Priority</span>
          <span>SLA Target (hours)</span>
          <span>Warning Window (hours)</span>
        </div>

        {loading ? (
          <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
            Loading SLA settings...
          </div>
        ) : (
          configurations.map((configuration) => (
            <div
              key={configuration.priority}
              className="grid grid-cols-[1.2fr_1fr_1fr] gap-4 px-6 py-4 border-t border-gray-100 dark:border-slate-800 items-center"
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
                  onChange(
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
                  onChange(
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
    </div>
  );
}
