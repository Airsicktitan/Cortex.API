import type {
  NotificationChannelConfiguration,
  NotificationChannelMode,
} from "../types/notificationChannelConfiguration";

const CHANNEL_OPTIONS: ReadonlyArray<NotificationChannelMode> = [
  "Neither",
  "Email",
  "Teams",
  "Both",
];

interface NotificationChannelSectionProps {
  configuration: NotificationChannelConfiguration | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
  onChange: <K extends keyof NotificationChannelConfiguration>(
    field: K,
    value: NotificationChannelConfiguration[K],
  ) => void;
  onRefresh: () => void;
  onSave: () => void;
}

export default function NotificationChannelSection({
  configuration,
  loading,
  saving,
  error,
  onChange,
  onRefresh,
  onSave,
}: NotificationChannelSectionProps) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Notification Channel Defaults
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Choose the system default for optional Email or Teams alerts when
              a user has not picked a personal preference yet.
            </p>
          </div>

          <div className="flex gap-3">
            <button
              onClick={onRefresh}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Refresh
            </button>
            <button
              onClick={onSave}
              disabled={saving || loading || !configuration}
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {saving ? "Saving..." : "Save Channels"}
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {loading || !configuration ? (
        <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
          Loading notification channel settings...
        </div>
      ) : (
        <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.2fr_1fr]">
          <div className="space-y-5">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Ticket assignment events
              </label>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Send external alerts when Syniti or Business ownership changes.
              </p>
              <select
                value={configuration.assignmentChannel}
                onChange={(event) =>
                  onChange("assignmentChannel", event.target.value as NotificationChannelMode)
                }
                className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                {CHANNEL_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                SLA at-risk and breached events
              </label>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Send external alerts when a ticket enters the warning window or
                breaches its target.
              </p>
              <select
                value={configuration.slaRiskChannel}
                onChange={(event) =>
                  onChange("slaRiskChannel", event.target.value as NotificationChannelMode)
                }
                className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                {CHANNEL_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
            <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
              Delivery Behavior
            </h4>
            <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
              In-app notifications always remain on. These settings only control
              optional external delivery from the backend.
            </p>
            <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
              If Email or Teams transport is not configured on the server yet,
              CORTEX will still save the notification in-app and quietly skip
              the external send.
            </p>
          </div>
        </div>
      )}
    </section>
  );
}
