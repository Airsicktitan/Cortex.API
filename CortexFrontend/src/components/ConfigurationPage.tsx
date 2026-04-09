import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import type {
  CustomReportDefinition,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";
import type { SessionConfiguration } from "../types/sessionConfiguration";
import type { SlaConfiguration } from "../types/sla";
import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";
import ArchivePolicySection from "./ArchivePolicySection";
import CustomReportRegistrySection from "./CustomReportRegistrySection";
import StoredProcedureRegistrySection from "./StoredProcedureRegistrySection";
import TicketStatusRegistrySection from "./TicketStatusRegistrySection";

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
  sessionConfiguration: SessionConfiguration | null;
  sessionError: string | null;
  sessionLoading: boolean;
  sessionSaving: boolean;
  onSessionChange: <K extends keyof SessionConfiguration>(
    field: K,
    value: SessionConfiguration[K],
  ) => void;
  onRefreshSession: () => void;
  onSaveSession: () => void;
  ticketStatuses: TicketStatusDefinition[];
  ticketStatusError: string | null;
  ticketStatusLoading: boolean;
  ticketStatusSaving: boolean;
  ticketStatusDeletingId: number | null;
  onRefreshTicketStatuses: () => void;
  onCreateTicketStatus: (
    definition: UpsertTicketStatusDefinitionInput,
  ) => Promise<void>;
  onUpdateTicketStatus: (
    id: number,
    definition: UpsertTicketStatusDefinitionInput,
  ) => Promise<void>;
  onDeleteTicketStatus: (id: number) => Promise<void>;
  archiveConfigurations: ArchiveConfiguration[];
  archiveConfiguration: ArchiveConfiguration | null;
  archiveError: string | null;
  archiveLoading: boolean;
  archiveSaving: boolean;
  archiveDeletingId: number | null;
  archiveRunning: boolean;
  onCreateArchivePolicy: () => void;
  onSelectArchivePolicy: (id: number) => void;
  onArchiveChange: <K extends keyof ArchiveConfiguration>(
    field: K,
    value: ArchiveConfiguration[K],
  ) => void;
  onRefreshArchive: () => void;
  onSaveArchive: () => void;
  onDeleteArchive: () => void;
  onRunArchiveNow: () => void;
  customReports: CustomReportDefinition[];
  databaseViews: DatabaseViewDefinition[];
  databaseViewsLoading: boolean;
  customReportError: string | null;
  customReportLoading: boolean;
  customReportSaving: boolean;
  customReportDeletingId: number | null;
  onRefreshCustomReports: () => void;
  onCreateCustomReport: (
    definition: UpsertCustomReportDefinitionInput,
  ) => Promise<void>;
  onUpdateCustomReport: (
    id: number,
    definition: UpsertCustomReportDefinitionInput,
  ) => Promise<void>;
  onDeleteCustomReport: (id: number) => Promise<void>;
  storedProcedures: StoredProcedureDefinition[];
  databaseStoredProcedures: DatabaseStoredProcedureDefinition[];
  databaseStoredProceduresLoading: boolean;
  storedProcedureError: string | null;
  storedProcedureLoading: boolean;
  storedProcedureSaving: boolean;
  storedProcedureDeletingId: number | null;
  onRefreshStoredProcedures: () => void;
  onCreateStoredProcedure: (
    definition: UpsertStoredProcedureDefinitionInput,
  ) => Promise<void>;
  onUpdateStoredProcedure: (
    id: number,
    definition: UpsertStoredProcedureDefinitionInput,
  ) => Promise<void>;
  onDeleteStoredProcedure: (id: number) => Promise<void>;
}

export default function ConfigurationPage({
  slaConfigurations,
  slaError,
  slaLoading,
  slaSaving,
  onSlaChange,
  onRefreshSla,
  onSaveSla,
  sessionConfiguration,
  sessionError,
  sessionLoading,
  sessionSaving,
  onSessionChange,
  onRefreshSession,
  onSaveSession,
  ticketStatuses,
  ticketStatusError,
  ticketStatusLoading,
  ticketStatusSaving,
  ticketStatusDeletingId,
  onRefreshTicketStatuses,
  onCreateTicketStatus,
  onUpdateTicketStatus,
  onDeleteTicketStatus,
  archiveConfigurations,
  archiveConfiguration,
  archiveError,
  archiveLoading,
  archiveSaving,
  archiveDeletingId,
  archiveRunning,
  onCreateArchivePolicy,
  onSelectArchivePolicy,
  onArchiveChange,
  onRefreshArchive,
  onSaveArchive,
  onDeleteArchive,
  onRunArchiveNow,
  customReports,
  databaseViews,
  databaseViewsLoading,
  customReportError,
  customReportLoading,
  customReportSaving,
  customReportDeletingId,
  onRefreshCustomReports,
  onCreateCustomReport,
  onUpdateCustomReport,
  onDeleteCustomReport,
  storedProcedures,
  databaseStoredProcedures,
  databaseStoredProceduresLoading,
  storedProcedureError,
  storedProcedureLoading,
  storedProcedureSaving,
  storedProcedureDeletingId,
  onRefreshStoredProcedures,
  onCreateStoredProcedure,
  onUpdateStoredProcedure,
  onDeleteStoredProcedure,
}: ConfigurationPageProps) {
  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div>
          <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
            Configuration
          </h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Manage the operational rules for SLA tracking, session security, and archive policy.
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
                className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
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
                Session Security
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Require users to re-authenticate after a period of inactivity.
              </p>
            </div>

            <div className="flex gap-3">
              <button
                onClick={onRefreshSession}
                className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              >
                Refresh
              </button>
              <button
                onClick={onSaveSession}
                disabled={sessionSaving || sessionLoading || !sessionConfiguration}
                className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
              >
                {sessionSaving ? "Saving..." : "Save Session Policy"}
              </button>
            </div>
          </div>
        </div>

        {sessionError && (
          <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{sessionError}</p>
          </div>
        )}

        {sessionLoading || !sessionConfiguration ? (
          <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
            Loading session policy...
          </div>
        ) : (
          <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.2fr_1fr]">
            <div className="space-y-5">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                  Inactivity timeout (minutes)
                </label>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Users must re-authenticate after this many idle minutes.
                </p>
                <input
                  type="number"
                  min={1}
                  step={1}
                  value={sessionConfiguration.inactivityTimeoutMinutes}
                  onChange={(event) =>
                    onSessionChange(
                      "inactivityTimeoutMinutes",
                      Number(event.target.value),
                    )
                  }
                  className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                  Warning window (minutes)
                </label>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Show a countdown prompt before the session locks.
                </p>
                <input
                  type="number"
                  min={0}
                  step={1}
                  value={sessionConfiguration.warningMinutes}
                  onChange={(event) =>
                    onSessionChange("warningMinutes", Number(event.target.value))
                  }
                  className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                />
              </div>
            </div>

            <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
              <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
                Current Policy
              </h4>
              <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
                Users can stay idle for{" "}
                <span className="font-medium text-gray-900 dark:text-slate-100">
                  {sessionConfiguration.inactivityTimeoutMinutes} minute
                  {sessionConfiguration.inactivityTimeoutMinutes === 1 ? "" : "s"}
                </span>{" "}
                before the app requires Auth0 sign-in again.
              </p>
              <p className="mt-3 text-sm text-gray-600 dark:text-slate-400">
                A warning appears{" "}
                <span className="font-medium text-gray-900 dark:text-slate-100">
                  {sessionConfiguration.warningMinutes} minute
                  {sessionConfiguration.warningMinutes === 1 ? "" : "s"}
                </span>{" "}
                before lockout.
              </p>
            </div>
          </div>
        )}
      </section>

      <TicketStatusRegistrySection
        statuses={ticketStatuses}
        loading={ticketStatusLoading}
        error={ticketStatusError}
        saving={ticketStatusSaving}
        deletingId={ticketStatusDeletingId}
        onRefresh={onRefreshTicketStatuses}
        onCreate={onCreateTicketStatus}
        onUpdate={onUpdateTicketStatus}
        onDelete={onDeleteTicketStatus}
      />

      <ArchivePolicySection
        policies={archiveConfigurations}
        selectedPolicy={archiveConfiguration}
        availableStatuses={ticketStatuses}
        loading={archiveLoading}
        saving={archiveSaving}
        deletingId={archiveDeletingId}
        running={archiveRunning}
        error={archiveError}
        onRefresh={onRefreshArchive}
        onNew={onCreateArchivePolicy}
        onSelect={onSelectArchivePolicy}
        onChange={onArchiveChange}
        onSave={onSaveArchive}
        onDelete={onDeleteArchive}
        onRunNow={onRunArchiveNow}
      />

      <CustomReportRegistrySection
        reports={customReports}
        databaseViews={databaseViews}
        databaseViewsLoading={databaseViewsLoading}
        loading={customReportLoading}
        error={customReportError}
        saving={customReportSaving}
        deletingId={customReportDeletingId}
        onRefresh={onRefreshCustomReports}
        onCreate={onCreateCustomReport}
        onUpdate={onUpdateCustomReport}
        onDelete={onDeleteCustomReport}
      />

      <StoredProcedureRegistrySection
        storedProcedures={storedProcedures}
        databaseStoredProcedures={databaseStoredProcedures}
        databaseStoredProceduresLoading={databaseStoredProceduresLoading}
        loading={storedProcedureLoading}
        error={storedProcedureError}
        saving={storedProcedureSaving}
        deletingId={storedProcedureDeletingId}
        onRefresh={onRefreshStoredProcedures}
        onCreate={onCreateStoredProcedure}
        onUpdate={onUpdateStoredProcedure}
        onDelete={onDeleteStoredProcedure}
      />
    </div>
  );
}
