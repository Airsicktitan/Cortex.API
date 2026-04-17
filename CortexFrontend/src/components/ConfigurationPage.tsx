import { useMemo, useState } from "react";
import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import type {
  CustomReportDefinition,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";
import type { NotificationChannelConfiguration } from "../types/notificationChannelConfiguration";
import type { SessionConfiguration } from "../types/sessionConfiguration";
import type { SlaConfiguration } from "../types/sla";
import type { RoleDefinition } from "../types/roleDefinition";
import type { ScheduledJob, UpsertScheduledJobInput } from "../types/scheduledJob";
import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import type {
  TicketBoardDefinition,
  UpsertTicketBoardDefinitionInput,
} from "../types/ticketBoard";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";
import type { TicketRoutingRule } from "../types/ticketRouting";
import ArchivePolicySection from "./ArchivePolicySection";
import CustomReportRegistrySection from "./CustomReportRegistrySection";
import NotificationChannelSection from "./NotificationChannelSection";
import ScheduledJobAdminSection from "./ScheduledJobAdminSection";
import StoredProcedureRegistrySection from "./StoredProcedureRegistrySection";
import RoleDefinitionSection from "./RoleDefinitionSection";
import TicketBoardRegistrySection from "./TicketBoardRegistrySection";
import TicketRoutingSection from "./TicketRoutingSection";
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
  notificationChannelConfiguration: NotificationChannelConfiguration | null;
  notificationChannelError: string | null;
  notificationChannelLoading: boolean;
  notificationChannelSaving: boolean;
  onNotificationChannelChange: <K extends keyof NotificationChannelConfiguration>(
    field: K,
    value: NotificationChannelConfiguration[K],
  ) => void;
  onRefreshNotificationChannels: () => void;
  onSaveNotificationChannels: () => void;
  ticketBoards: TicketBoardDefinition[];
  ticketBoardError: string | null;
  ticketBoardLoading: boolean;
  ticketBoardSaving: boolean;
  ticketBoardDeletingId: number | null;
  onRefreshTicketBoards: () => void;
  onCreateTicketBoard: (
    definition: UpsertTicketBoardDefinitionInput,
  ) => Promise<void>;
  onUpdateTicketBoard: (
    id: number,
    definition: UpsertTicketBoardDefinitionInput,
  ) => Promise<void>;
  onDeleteTicketBoard: (id: number) => Promise<void>;
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
  ticketRoutingRules: TicketRoutingRule[];
  selectedTicketRoutingRule: TicketRoutingRule | null;
  ticketRoutingError: string | null;
  ticketRoutingLoading: boolean;
  ticketRoutingSaving: boolean;
  ticketRoutingDeletingId: number | null;
  onRefreshTicketRouting: () => void;
  onCreateTicketRoutingRule: () => void;
  onSelectTicketRoutingRule: (id: number) => void;
  onTicketRoutingChange: <K extends keyof TicketRoutingRule>(
    field: K,
    value: TicketRoutingRule[K],
  ) => void;
  onSaveTicketRoutingRule: () => Promise<void>;
  onDeleteTicketRoutingRule: () => Promise<void>;
  roleDefinitions: RoleDefinition[];
  selectedRoleDefinition: RoleDefinition | null;
  rolePermissionOptions: string[];
  roleDefinitionError: string | null;
  roleDefinitionLoading: boolean;
  roleDefinitionSaving: boolean;
  roleDefinitionDeletingId: number | null;
  onRefreshRoleDefinitions: () => void;
  onCreateRoleDefinition: () => void;
  onSelectRoleDefinition: (id: number) => void;
  onRoleDefinitionChange: <K extends keyof RoleDefinition>(
    field: K,
    value: RoleDefinition[K],
  ) => void;
  onSaveRoleDefinition: () => Promise<void>;
  onDeleteRoleDefinition: () => Promise<void>;
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
  canExportAdminLogs: boolean;
  onExportAdminLogs: (fromUtcIso: string, toUtcIso: string) => Promise<void>;
  canManageJobs: boolean;
  jobs: ScheduledJob[];
  jobsLoading: boolean;
  jobsError: string | null;
  jobsSaving: boolean;
  runningJobId: number | null;
  onRefreshJobs: () => void;
  onCreateScheduledJob: (job: UpsertScheduledJobInput) => Promise<void>;
  onUpdateScheduledJob: (
    id: number,
    job: UpsertScheduledJobInput,
  ) => Promise<void>;
  onRunScheduledJobNow: (id: number) => Promise<void>;
  /** Developer+ — custom SQL report definitions and database views. */
  canManageReportDefinitions: boolean;
  onOpenJobs: () => void;
  onOpenUsers?: () => void;
}

type ConfigSection =
  | "general"
  | "boards"
  | "statuses"
  | "routing"
  | "roles"
  | "notifications"
  | "jobs"
  | "reports"
  | "logs";

function formatUtcDateInput(value: Date) {
  return value.toISOString().slice(0, 10);
}

export default function ConfigurationPage(props: ConfigurationPageProps) {
  const {
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
    notificationChannelConfiguration,
    notificationChannelError,
    notificationChannelLoading,
    notificationChannelSaving,
    onNotificationChannelChange,
    onRefreshNotificationChannels,
    onSaveNotificationChannels,
    ticketBoards,
    ticketBoardError,
    ticketBoardLoading,
    ticketBoardSaving,
    ticketBoardDeletingId,
    onRefreshTicketBoards,
    onCreateTicketBoard,
    onUpdateTicketBoard,
    onDeleteTicketBoard,
    ticketStatuses,
    ticketStatusError,
    ticketStatusLoading,
    ticketStatusSaving,
    ticketStatusDeletingId,
    onRefreshTicketStatuses,
    onCreateTicketStatus,
    onUpdateTicketStatus,
    onDeleteTicketStatus,
    ticketRoutingRules,
    selectedTicketRoutingRule,
    ticketRoutingError,
    ticketRoutingLoading,
    ticketRoutingSaving,
    ticketRoutingDeletingId,
    onRefreshTicketRouting,
    onCreateTicketRoutingRule,
    onSelectTicketRoutingRule,
    onTicketRoutingChange,
    onSaveTicketRoutingRule,
    onDeleteTicketRoutingRule,
    roleDefinitions,
    selectedRoleDefinition,
    rolePermissionOptions,
    roleDefinitionError,
    roleDefinitionLoading,
    roleDefinitionSaving,
    roleDefinitionDeletingId,
    onRefreshRoleDefinitions,
    onCreateRoleDefinition,
    onSelectRoleDefinition,
    onRoleDefinitionChange,
    onSaveRoleDefinition,
    onDeleteRoleDefinition,
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
    canExportAdminLogs,
    onExportAdminLogs,
    canManageJobs,
    jobs,
    jobsLoading,
    jobsError,
    jobsSaving,
    runningJobId,
    onRefreshJobs,
    onCreateScheduledJob,
    onUpdateScheduledJob,
    onRunScheduledJobNow,
    canManageReportDefinitions,
    onOpenJobs,
    onOpenUsers,
  } = props;

  const [activeSection, setActiveSection] = useState<ConfigSection>("general");
  const defaultDateRange = useMemo(() => {
    const endUtc = new Date();
    const startUtc = new Date(endUtc);
    startUtc.setUTCDate(startUtc.getUTCDate() - 7);

    return {
      fromDate: formatUtcDateInput(startUtc),
      toDate: formatUtcDateInput(endUtc),
    };
  }, []);
  const [logExportFromDate, setLogExportFromDate] = useState(defaultDateRange.fromDate);
  const [logExportToDate, setLogExportToDate] = useState(defaultDateRange.toDate);
  const [logExporting, setLogExporting] = useState(false);
  const [logExportError, setLogExportError] = useState<string | null>(null);
  const [logExportSuccess, setLogExportSuccess] = useState<string | null>(null);

  const navItems: Array<{ id: ConfigSection; label: string; description: string }> = [
    { id: "general", label: "General", description: "SLA, session, and archive policy setup" },
    { id: "boards", label: "Boards", description: "Ticket board setup and behavior" },
    { id: "statuses", label: "Statuses", description: "Define workflow stages for tickets" },
    { id: "routing", label: "Routing", description: "Automatically assign tickets based on structured rules and ownership logic." },
    {
      id: "roles",
      label: "User roles",
      description: "Define roles and permissions. Assign users in the Users section.",
    },
    { id: "notifications", label: "Notifications", description: "Notification policy and delivery defaults" },
    {
      id: "jobs",
      label: "Scheduled Jobs",
      description: "Configure automation. Monitor execution in Job Activity.",
    },
    { id: "reports", label: "Reports", description: "Report definitions and procedure setup" },
    { id: "logs", label: "Log Export", description: "Administrative request log export" },
  ];

  const handleExportLogs = async () => {
    setLogExportError(null);
    setLogExportSuccess(null);

    if (!logExportFromDate || !logExportToDate) {
      setLogExportError("Select both a start date and end date.");
      return;
    }

    if (logExportToDate < logExportFromDate) {
      setLogExportError("End date must be on or after start date.");
      return;
    }

    const fromUtcIso = `${logExportFromDate}T00:00:00.000Z`;
    const toUtcIso = `${logExportToDate}T23:59:59.999Z`;

    try {
      setLogExporting(true);
      await onExportAdminLogs(fromUtcIso, toUtcIso);
      setLogExportSuccess("Log export started. Your CSV download should begin shortly.");
    } catch {
      setLogExportError("Unable to export logs. Please try again.");
    } finally {
      setLogExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">Configuration</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          Define system setup, policy, and behavior by section.
        </p>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-0 lg:grid-cols-[280px_1fr]">
          <aside className="border-b border-gray-100 p-4 dark:border-slate-800 lg:border-b-0 lg:border-r">
            <nav className="space-y-3">
              {navItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => setActiveSection(item.id)}
                  className={`w-full rounded-md border px-4 py-3 text-left transition-colors ${
                    activeSection === item.id
                      ? "border-cortex-blue bg-cortex-blue-soft text-cortex-ink dark:border-cortex-blue dark:bg-cortex-blue/20 dark:text-slate-100"
                      : "border-gray-200 text-gray-700 hover:bg-gray-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                  }`}
                >
                  <p className="text-sm font-semibold">{item.label}</p>
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">{item.description}</p>
                </button>
              ))}
            </nav>
          </aside>

          <div className="p-4 md:p-6">
            {activeSection === "general" && (
              <div className="space-y-6">
                <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                  <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
                    <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                      <div>
                        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">SLA Configuration</h3>
                        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">Set the SLA target and warning window for each ticket priority.</p>
                      </div>
                      <div className="flex gap-3">
                        <button onClick={onRefreshSla} className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700">Refresh</button>
                        <button onClick={onSaveSla} disabled={slaSaving || slaLoading} className="rounded-md bg-cortex-blue px-4 py-2 text-white hover:bg-cortex-blue-dark disabled:opacity-60">{slaSaving ? "Saving..." : "Save SLA"}</button>
                      </div>
                    </div>
                  </div>
                  {slaError && <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40"><p className="text-red-700 dark:text-red-300">{slaError}</p></div>}
                  <div className="grid grid-cols-[1.2fr_1fr_1fr] gap-4 bg-gray-50 px-6 py-4 text-sm font-medium text-gray-600 dark:bg-slate-800/80 dark:text-slate-300"><span>Priority</span><span>SLA Target (hours)</span><span>Warning Window (hours)</span></div>
                  {slaLoading ? <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">Loading SLA settings...</div> : slaConfigurations.map((configuration) => (
                    <div key={configuration.priority} className="grid grid-cols-[1.2fr_1fr_1fr] items-center gap-4 border-t border-gray-100 px-6 py-4 dark:border-slate-800">
                      <div><p className="font-medium text-gray-900 dark:text-slate-100">{configuration.priority}</p></div>
                      <input type="number" min={1} step={1} value={configuration.targetHours} onChange={(event) => onSlaChange(configuration.priority, "targetHours", Number(event.target.value))} className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" />
                      <input type="number" min={0} step={1} value={configuration.warningHours} onChange={(event) => onSlaChange(configuration.priority, "warningHours", Number(event.target.value))} className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" />
                    </div>
                  ))}
                </section>

                <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                  <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
                    <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                      <div>
                        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">Session Security</h3>
                        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">Require users to re-authenticate after inactivity.</p>
                      </div>
                      <div className="flex gap-3">
                        <button onClick={onRefreshSession} className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700">Refresh</button>
                        <button onClick={onSaveSession} disabled={sessionSaving || sessionLoading || !sessionConfiguration} className="rounded-md bg-cortex-blue px-4 py-2 text-white hover:bg-cortex-blue-dark disabled:opacity-60">{sessionSaving ? "Saving..." : "Save Session Policy"}</button>
                      </div>
                    </div>
                  </div>
                  {sessionError && <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40"><p className="text-red-700 dark:text-red-300">{sessionError}</p></div>}
                  {sessionLoading || !sessionConfiguration ? <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">Loading session policy...</div> : (
                    <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.2fr_1fr]">
                      <div className="space-y-5">
                        <div><label className="block text-sm font-medium text-gray-700 dark:text-slate-300">Inactivity timeout (minutes)</label><input type="number" min={1} step={1} value={sessionConfiguration.inactivityTimeoutMinutes} onChange={(event) => onSessionChange("inactivityTimeoutMinutes", Number(event.target.value))} className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" /></div>
                        <div><label className="block text-sm font-medium text-gray-700 dark:text-slate-300">Warning window (minutes)</label><input type="number" min={0} step={1} value={sessionConfiguration.warningMinutes} onChange={(event) => onSessionChange("warningMinutes", Number(event.target.value))} className="mt-3 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" /></div>
                      </div>
                    </div>
                  )}
                </section>

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
              </div>
            )}

            {activeSection === "boards" && (
              <div className="space-y-6">
                <TicketBoardRegistrySection
                  boards={ticketBoards}
                  loading={ticketBoardLoading}
                  error={ticketBoardError}
                  saving={ticketBoardSaving}
                  deletingId={ticketBoardDeletingId}
                  onRefresh={onRefreshTicketBoards}
                  onCreate={onCreateTicketBoard}
                  onUpdate={onUpdateTicketBoard}
                  onDelete={onDeleteTicketBoard}
                />
              </div>
            )}

            {activeSection === "statuses" && (
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
            )}

            {activeSection === "routing" && (
              <TicketRoutingSection
                rules={ticketRoutingRules}
                boards={ticketBoards}
                selectedRule={selectedTicketRoutingRule}
                loading={ticketRoutingLoading}
                saving={ticketRoutingSaving}
                deletingId={ticketRoutingDeletingId}
                error={ticketRoutingError}
                onRefresh={onRefreshTicketRouting}
                onNew={onCreateTicketRoutingRule}
                onSelect={onSelectTicketRoutingRule}
                onChange={onTicketRoutingChange}
                onSave={() => void onSaveTicketRoutingRule()}
                onDelete={() => void onDeleteTicketRoutingRule()}
              />
            )}

            {activeSection === "roles" && (
              <div className="space-y-4">
                <RoleDefinitionSection
                  roles={roleDefinitions}
                  selectedRole={selectedRoleDefinition}
                  permissions={rolePermissionOptions}
                  loading={roleDefinitionLoading}
                  saving={roleDefinitionSaving}
                  deletingId={roleDefinitionDeletingId}
                  error={roleDefinitionError}
                  onRefresh={onRefreshRoleDefinitions}
                  onNew={onCreateRoleDefinition}
                  onSelect={onSelectRoleDefinition}
                  onChange={onRoleDefinitionChange}
                  onSave={onSaveRoleDefinition}
                  onDelete={onDeleteRoleDefinition}
                />
                <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    Users are assigned roles in the Users section.
                  </p>
                  {onOpenUsers && (
                    <button
                      type="button"
                      onClick={onOpenUsers}
                      className="mt-3 inline-flex text-sm text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan dark:hover:text-cortex-blue"
                    >
                      Open Users
                    </button>
                  )}
                </section>
              </div>
            )}

            {activeSection === "notifications" && (
              <NotificationChannelSection
                configuration={notificationChannelConfiguration}
                loading={notificationChannelLoading}
                saving={notificationChannelSaving}
                error={notificationChannelError}
                onChange={onNotificationChannelChange}
                onRefresh={onRefreshNotificationChannels}
                onSave={onSaveNotificationChannels}
              />
            )}

            {activeSection === "jobs" && (
              <div className="space-y-4">
                <ScheduledJobAdminSection
                  jobs={jobs}
                  storedProcedures={storedProcedures}
                  loading={jobsLoading}
                  error={jobsError}
                  saving={jobsSaving}
                  runningJobId={runningJobId}
                  onRefresh={onRefreshJobs}
                  onCreate={onCreateScheduledJob}
                  onUpdate={onUpdateScheduledJob}
                  onRunNow={onRunScheduledJobNow}
                />
                {onOpenJobs && canManageJobs && (
                  <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      Job Activity provides monitoring for recent job runs and outcomes.
                    </p>
                    <button
                      type="button"
                      onClick={onOpenJobs}
                      className="mt-3 inline-flex text-sm text-cortex-blue hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan dark:hover:text-cortex-blue"
                    >
                      Open Job Activity
                    </button>
                  </section>
                )}
              </div>
            )}

            {activeSection === "reports" && (
              <div className="space-y-6">
                {canManageReportDefinitions && (
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
                )}
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
            )}

            {activeSection === "logs" && (
              <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">Admin Log Export</h3>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">Export request logs as CSV for a selected UTC date range.</p>
                </div>
                <div className="space-y-4 px-6 py-6">
                  {!canExportAdminLogs ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">You do not have permission to export admin logs.</p>
                  ) : (
                    <>
                      <div className="grid gap-4 md:grid-cols-2">
                        <div><label className="block text-sm font-medium text-gray-700 dark:text-slate-300">From (UTC date)</label><input type="date" value={logExportFromDate} onChange={(event) => setLogExportFromDate(event.target.value)} className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" /></div>
                        <div><label className="block text-sm font-medium text-gray-700 dark:text-slate-300">To (UTC date)</label><input type="date" value={logExportToDate} onChange={(event) => setLogExportToDate(event.target.value)} className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100" /></div>
                      </div>
                      <button onClick={() => void handleExportLogs()} disabled={logExporting} className="rounded-md bg-cortex-blue px-4 py-2 text-white hover:bg-cortex-blue-dark disabled:opacity-60">{logExporting ? "Exporting..." : "Export Logs (CSV)"}</button>
                    </>
                  )}
                  {logExportError && <div className="rounded border-l-4 border-red-500 bg-red-50 px-4 py-3 dark:bg-red-950/40"><p className="text-red-700 dark:text-red-300">{logExportError}</p></div>}
                  {logExportSuccess && <div className="rounded border-l-4 border-green-500 bg-green-50 px-4 py-3 dark:bg-green-950/40"><p className="text-green-700 dark:text-green-300">{logExportSuccess}</p></div>}
                </div>
              </section>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}
