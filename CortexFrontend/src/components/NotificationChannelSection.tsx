import type {
  NotificationChannelConfiguration,
  NotificationChannelMode,
} from "../types/notificationChannelConfiguration";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  configFieldClass,
} from "./configurationAdminUi";

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
    <ConfigPageShell>
      <ConfigPageHeader
        title="Notifications"
        description="Default Email/Teams delivery when a user has not set a personal preference."
        actions={
          <>
            <ConfigPrimaryButton
              onClick={onSave}
              disabled={saving || loading || !configuration}
            >
              {saving ? "Saving…" : "Save changes"}
            </ConfigPrimaryButton>
            <ConfigGhostButton onClick={onRefresh} disabled={saving}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      {loading || !configuration ? (
        <div className="px-6 py-10 text-center text-sm text-gray-500 dark:text-slate-400">
          Loading notification settings…
        </div>
      ) : (
        <ConfigPageBody>
          <div className="grid gap-6 lg:grid-cols-2">
            <ConfigDetailCard title="Channels" subtitle="External delivery for key events">
              <div className="space-y-5">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                    Ticket assignment
                  </label>
                  <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                    When Syniti or business ownership changes.
                  </p>
                  <select
                    value={configuration.assignmentChannel}
                    onChange={(event) =>
                      onChange("assignmentChannel", event.target.value as NotificationChannelMode)
                    }
                    className={`${configFieldClass} mt-2`}
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
                    SLA risk &amp; breach
                  </label>
                  <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                    Warning window and missed targets.
                  </p>
                  <select
                    value={configuration.slaRiskChannel}
                    onChange={(event) =>
                      onChange("slaRiskChannel", event.target.value as NotificationChannelMode)
                    }
                    className={`${configFieldClass} mt-2`}
                  >
                    {CHANNEL_OPTIONS.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </ConfigDetailCard>

            <ConfigDetailCard title="Delivery" subtitle="How this behaves in Cortex">
              <p className="text-sm text-gray-600 dark:text-slate-400">
                In-app notifications stay on. These defaults only affect optional Email or Teams sends from the server.
              </p>
              <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
                If transport is not configured, Cortex still records the notification and skips the external send.
              </p>
            </ConfigDetailCard>
          </div>
        </ConfigPageBody>
      )}
    </ConfigPageShell>
  );
}
