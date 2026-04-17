/**
 * useConfiguration — owns all configuration-domain state and handlers:
 * SLA, session config, notification channels, ticket boards, ticket statuses,
 * ticket routing rules, archive configuration, custom reports, stored
 * procedures, and scheduled jobs.
 *
 * Cross-domain side-effects (renaming a board/status updates active tickets,
 * running an archive job refreshes the ticket list, etc.) are handled via
 * callback params so this hook never reaches into ticket or layout state
 * directly.
 */

import { useState, useCallback } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import type {
  CustomReportDefinition,
  CustomReportResult,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";
import type { NotificationChannelConfiguration } from "../types/notificationChannelConfiguration";
import type { ScheduledJob, UpsertScheduledJobInput } from "../types/scheduledJob";
import type { SessionConfiguration } from "../types/sessionConfiguration";
import type { SlaConfiguration } from "../types/sla";
import type {
  TicketBoardDefinition,
  UpsertTicketBoardDefinitionInput,
} from "../types/ticketBoard";
import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";
import type {
  TicketRoutingRule,
  UpsertTicketRoutingRuleInput,
} from "../types/ticketRouting";
import type { Ticket } from "../types/ticket";
import type { ArchivedTicket } from "../types/archivedTicket";
import {
  getUserFacingErrorMessage,
  isLikelyNetworkError,
  ApiError,
} from "../services/api";
import { archiveConfigurationService } from "../services/archiveConfigurationService";
import { customReportService } from "../services/customReportService";
import { notificationChannelConfigurationService } from "../services/notificationChannelConfigurationService";
import { reportService } from "../services/reportService";
import { scheduledJobService } from "../services/scheduledJobService";
import { sessionConfigurationService } from "../services/sessionConfigurationService";
import { slaService } from "../services/slaService";
import { storedProcedureService } from "../services/storedProcedureService";
import { ticketBoardService } from "../services/ticketBoardService";
import { ticketRoutingService } from "../services/ticketRoutingService";
import { ticketStatusService } from "../services/ticketStatusService";
import toast from "react-hot-toast";

// ── Pure helpers (duplicated from App.tsx to avoid coupling) ─────────────────

function isForbiddenError(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

function sortTicketBoards(boards: TicketBoardDefinition[]): TicketBoardDefinition[] {
  return [...boards].sort((left, right) => {
    const leftIsDefault = left.name.toLowerCase() === "ticket";
    const rightIsDefault = right.name.toLowerCase() === "ticket";
    if (leftIsDefault && !rightIsDefault) return -1;
    if (!leftIsDefault && rightIsDefault) return 1;
    return left.name.localeCompare(right.name);
  });
}

function sortTicketStatuses(
  statuses: TicketStatusDefinition[],
): TicketStatusDefinition[] {
  return [...statuses].sort((left, right) => left.id - right.id);
}

function sortTicketRoutingRules(rules: TicketRoutingRule[]): TicketRoutingRule[] {
  return [...rules].sort((left, right) => {
    const leftKey =
      `${left.titleContains}|${left.department}|${left.synitiOwner}|${left.businessOwner}`.toLowerCase();
    const rightKey =
      `${right.titleContains}|${right.department}|${right.synitiOwner}|${right.businessOwner}`.toLowerCase();
    const keyComparison = leftKey.localeCompare(rightKey);
    return keyComparison !== 0 ? keyComparison : left.id - right.id;
  });
}

function sortArchiveConfigurations(
  configurations: ArchiveConfiguration[],
): ArchiveConfiguration[] {
  return [...configurations].sort((left, right) => {
    if (left.archiveAfterDays !== right.archiveAfterDays) {
      return left.archiveAfterDays - right.archiveAfterDays;
    }
    return left.id - right.id;
  });
}

function getDefaultArchiveEligibleStatuses(
  statuses: TicketStatusDefinition[],
): string[] {
  return statuses
    .filter((s) => s.name === "Resolved" || s.name === "Closed")
    .map((s) => s.name);
}

function createDraftTicketRoutingRule(): TicketRoutingRule {
  return {
    id: 0,
    department: "",
    titleContains: "",
    synitiOwner: "",
    businessOwner: "",
    isEnabled: true,
    createdDateUtc: "",
  };
}

function createDraftArchiveConfiguration(
  statuses: TicketStatusDefinition[],
): ArchiveConfiguration {
  return {
    id: 0,
    archiveAfterDays: 30,
    eligibleStatuses: getDefaultArchiveEligibleStatuses(statuses),
  };
}

// ── Types ────────────────────────────────────────────────────────────────────

type ReportSection = "sla" | "online-users" | "custom";

export interface UseConfigurationParams {
  getApiToken: () => Promise<string>;
  setApiUnavailable: Dispatch<SetStateAction<boolean>>;
  /** Called when a board/status rename needs to be reflected in the ticket list. */
  setAllTickets: Dispatch<SetStateAction<Ticket[]>>;
  /** Called when a board/status rename needs to be reflected in archived tickets. */
  setArchivedTickets: Dispatch<SetStateAction<ArchivedTicket[]>>;
  /** Called when a board/status rename applies to the currently open ticket. */
  setSelectedTicket: Dispatch<SetStateAction<Ticket | null>>;
  /** Called when the active board filter must be reset (board deleted/disabled). */
  setSelectedBoardId: Dispatch<SetStateAction<number | "all">>;
  /** Silently refreshes the active ticket list (e.g., after SLA save or archive run). */
  refreshTicketsSilently: (token?: string) => Promise<void>;
  /** Reloads the archived ticket list (e.g., after archive run or job run). */
  loadArchivedTickets: (
    token?: string,
    options?: { fullCatalog?: boolean },
  ) => Promise<void>;
  /** Called when a config action needs to change the active Reports tab. */
  onActiveReportSectionChange: (section: ReportSection) => void;
  /** Called when a config action needs to update the selected custom report. */
  onSelectedCustomReportIdChange: (id: number | null) => void;
}

// ── Hook ─────────────────────────────────────────────────────────────────────

export function useConfiguration({
  getApiToken,
  setApiUnavailable,
  setAllTickets,
  setArchivedTickets,
  setSelectedTicket,
  setSelectedBoardId,
  refreshTicketsSilently,
  loadArchivedTickets,
  onActiveReportSectionChange,
  onSelectedCustomReportIdChange,
}: UseConfigurationParams) {
  // ── SLA ───────────────────────────────────────────────────────────────────
  const [slaConfigurations, setSlaConfigurations] = useState<SlaConfiguration[]>([]);
  const [slaLoading, setSlaLoading] = useState(false);
  const [slaSaving, setSlaSaving] = useState(false);
  const [slaError, setSlaError] = useState<string | null>(null);

  // ── Session configuration ─────────────────────────────────────────────────
  const [sessionConfiguration, setSessionConfiguration] =
    useState<SessionConfiguration | null>(null);
  const [sessionLoadedOnce, setSessionLoadedOnce] = useState(false);
  const [sessionLoading, setSessionLoading] = useState(false);
  const [sessionSaving, setSessionSaving] = useState(false);
  const [sessionError, setSessionError] = useState<string | null>(null);

  // ── Notification channels ─────────────────────────────────────────────────
  const [notificationChannelConfiguration, setNotificationChannelConfiguration] =
    useState<NotificationChannelConfiguration | null>(null);
  const [notificationChannelsLoadedOnce, setNotificationChannelsLoadedOnce] =
    useState(false);
  const [notificationChannelLoading, setNotificationChannelLoading] = useState(false);
  const [notificationChannelSaving, setNotificationChannelSaving] = useState(false);
  const [notificationChannelError, setNotificationChannelError] =
    useState<string | null>(null);

  // ── Ticket boards ─────────────────────────────────────────────────────────
  const [ticketBoards, setTicketBoards] = useState<TicketBoardDefinition[]>([]);
  const [ticketBoardLoading, setTicketBoardLoading] = useState(false);
  const [ticketBoardSaving, setTicketBoardSaving] = useState(false);
  const [deletingTicketBoardId, setDeletingTicketBoardId] = useState<number | null>(null);
  const [ticketBoardError, setTicketBoardError] = useState<string | null>(null);

  // ── Ticket statuses ───────────────────────────────────────────────────────
  const [ticketStatuses, setTicketStatuses] = useState<TicketStatusDefinition[]>([]);
  const [ticketStatusLoading, setTicketStatusLoading] = useState(false);
  const [ticketStatusSaving, setTicketStatusSaving] = useState(false);
  const [deletingTicketStatusId, setDeletingTicketStatusId] = useState<number | null>(
    null,
  );
  const [ticketStatusError, setTicketStatusError] = useState<string | null>(null);

  // ── Ticket routing rules ──────────────────────────────────────────────────
  const [ticketRoutingRules, setTicketRoutingRules] = useState<TicketRoutingRule[]>([]);
  const [selectedTicketRoutingRule, setSelectedTicketRoutingRule] =
    useState<TicketRoutingRule | null>(null);
  const [ticketRoutingLoadedOnce, setTicketRoutingLoadedOnce] = useState(false);
  const [ticketRoutingLoading, setTicketRoutingLoading] = useState(false);
  const [ticketRoutingSaving, setTicketRoutingSaving] = useState(false);
  const [deletingTicketRoutingRuleId, setDeletingTicketRoutingRuleId] = useState<
    number | null
  >(null);
  const [ticketRoutingError, setTicketRoutingError] = useState<string | null>(null);

  // ── Archive configuration ─────────────────────────────────────────────────
  const [archiveConfigurations, setArchiveConfigurations] = useState<
    ArchiveConfiguration[]
  >([]);
  const [archiveConfiguration, setArchiveConfiguration] =
    useState<ArchiveConfiguration | null>(null);
  const [archiveLoadedOnce, setArchiveLoadedOnce] = useState(false);
  const [archiveLoading, setArchiveLoading] = useState(false);
  const [archiveSaving, setArchiveSaving] = useState(false);
  const [archiveRunning, setArchiveRunning] = useState(false);
  const [deletingArchiveConfigurationId, setDeletingArchiveConfigurationId] =
    useState<number | null>(null);
  const [archiveError, setArchiveError] = useState<string | null>(null);

  // ── Custom reports ────────────────────────────────────────────────────────
  const [customReports, setCustomReports] = useState<CustomReportDefinition[]>([]);
  const [databaseViews, setDatabaseViews] = useState<DatabaseViewDefinition[]>([]);
  const [databaseViewsLoading, setDatabaseViewsLoading] = useState(false);
  const [customReportsLoadedOnce, setCustomReportsLoadedOnce] = useState(false);
  const [customReportsLoading, setCustomReportsLoading] = useState(false);
  const [customReportsSaving, setCustomReportsSaving] = useState(false);
  const [deletingCustomReportId, setDeletingCustomReportId] = useState<number | null>(
    null,
  );
  const [customReportsError, setCustomReportsError] = useState<string | null>(null);
  const [customReportResult, setCustomReportResult] =
    useState<CustomReportResult | null>(null);
  const [customReportResultLoading, setCustomReportResultLoading] = useState(false);
  const [customReportResultError, setCustomReportResultError] = useState<
    string | null
  >(null);

  // ── Stored procedures ─────────────────────────────────────────────────────
  const [storedProcedures, setStoredProcedures] = useState<
    StoredProcedureDefinition[]
  >([]);
  const [databaseStoredProcedures, setDatabaseStoredProcedures] = useState<
    DatabaseStoredProcedureDefinition[]
  >([]);
  const [databaseStoredProceduresLoading, setDatabaseStoredProceduresLoading] =
    useState(false);
  const [storedProcedureLoading, setStoredProcedureLoading] = useState(false);
  const [storedProcedureSaving, setStoredProcedureSaving] = useState(false);
  const [deletingStoredProcedureId, setDeletingStoredProcedureId] = useState<
    number | null
  >(null);
  const [storedProcedureError, setStoredProcedureError] = useState<string | null>(
    null,
  );

  // ── Scheduled jobs ────────────────────────────────────────────────────────
  const [jobs, setJobs] = useState<ScheduledJob[]>([]);
  const [jobsLoading, setJobsLoading] = useState(false);
  const [jobsLoaded, setJobsLoaded] = useState(false);
  const [jobsSaving, setJobsSaving] = useState(false);
  const [jobsError, setJobsError] = useState<string | null>(null);
  const [runningJobId, setRunningJobId] = useState<number | null>(null);

  // ── Data loaders ──────────────────────────────────────────────────────────

  const loadSlaConfigurations = useCallback(
    async (providedToken?: string) => {
      setSlaLoading(true);
      setSlaError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await slaService.getAll(token);
        setSlaConfigurations(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load SLA settings", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setSlaError("You do not have permission to manage SLA settings.");
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setSlaError("Failed to load SLA settings.");
        }
      } finally {
        setSlaLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadSessionConfiguration = useCallback(
    async (providedToken?: string) => {
      setSessionLoading(true);
      setSessionError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await sessionConfigurationService.get(token);
        setSessionConfiguration(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load session configuration", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setSessionError(
            "You do not have permission to view session security settings.",
          );
        } else {
          setApiUnavailable(false);
          setSessionError("Failed to load session configuration.");
        }
      } finally {
        setSessionLoadedOnce(true);
        setSessionLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadNotificationChannelConfiguration = useCallback(
    async (providedToken?: string) => {
      setNotificationChannelLoading(true);
      setNotificationChannelError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await notificationChannelConfigurationService.get(token);
        setNotificationChannelConfiguration(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load notification channel configuration", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setNotificationChannelError(
            "You do not have permission to view notification channel settings.",
          );
        } else {
          setApiUnavailable(false);
          setNotificationChannelError("Failed to load notification channel settings.");
        }
      } finally {
        setNotificationChannelsLoadedOnce(true);
        setNotificationChannelLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadTicketBoards = useCallback(
    async (providedToken?: string) => {
      setTicketBoardLoading(true);
      setTicketBoardError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = sortTicketBoards(await ticketBoardService.getAll(token));
        setTicketBoards(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load ticket boards", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setTicketBoardError("You do not have permission to view ticket boards.");
        } else {
          setApiUnavailable(false);
          setTicketBoardError(
            getUserFacingErrorMessage(error, "Failed to load ticket boards."),
          );
        }
      } finally {
        setTicketBoardLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadTicketStatuses = useCallback(
    async (providedToken?: string) => {
      setTicketStatusLoading(true);
      setTicketStatusError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = sortTicketStatuses(await ticketStatusService.getAll(token));
        setTicketStatuses(data);
        setArchiveConfiguration((currentConfiguration) =>
          currentConfiguration?.id === 0
            ? {
                ...currentConfiguration,
                eligibleStatuses:
                  currentConfiguration.eligibleStatuses.length > 0
                    ? currentConfiguration.eligibleStatuses
                    : getDefaultArchiveEligibleStatuses(data),
              }
            : currentConfiguration,
        );
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load ticket statuses", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setTicketStatusError("Failed to load ticket statuses.");
        }
      } finally {
        setTicketStatusLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadTicketRoutingRules = useCallback(
    async (providedToken?: string) => {
      setTicketRoutingLoading(true);
      setTicketRoutingError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = sortTicketRoutingRules(
          await ticketRoutingService.getAll(token),
        );
        setTicketRoutingRules(data);
        setSelectedTicketRoutingRule((currentRule) => {
          if (currentRule?.id === 0) return currentRule;
          if (currentRule) {
            const matchingRule = data.find((rule) => rule.id === currentRule.id);
            if (matchingRule) return matchingRule;
          }
          return data[0] ?? null;
        });
        setTicketRoutingLoadedOnce(true);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load ticket routing rules", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setTicketRoutingError("Failed to load ticket routing rules.");
        }
      } finally {
        setTicketRoutingLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadArchiveConfigurations = useCallback(
    async (providedToken?: string) => {
      setArchiveLoading(true);
      setArchiveError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = sortArchiveConfigurations(
          await archiveConfigurationService.getAll(token),
        );
        setArchiveConfigurations(data);
        setArchiveConfiguration((currentConfiguration) => {
          if (currentConfiguration?.id === 0) return currentConfiguration;
          if (currentConfiguration) {
            const matching = data.find((c) => c.id === currentConfiguration.id);
            if (matching) return matching;
          }
          return data[0] ?? null;
        });
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load archive configuration", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setArchiveError(
            "You do not have permission to manage archive configuration.",
          );
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setArchiveError("Failed to load archive configuration.");
        }
      } finally {
        setArchiveLoadedOnce(true);
        setArchiveLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadStoredProcedures = useCallback(
    async (providedToken?: string) => {
      setStoredProcedureLoading(true);
      setStoredProcedureError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await storedProcedureService.getAll(token);
        setStoredProcedures(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load stored procedures", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setStoredProcedureError(
            "You do not have permission to manage stored procedures.",
          );
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setStoredProcedureError("Failed to load stored procedures.");
        }
      } finally {
        setStoredProcedureLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadDatabaseStoredProcedures = useCallback(
    async (providedToken?: string) => {
      setDatabaseStoredProceduresLoading(true);
      try {
        const token = providedToken ?? (await getApiToken());
        const data =
          await storedProcedureService.getAvailableDatabaseProcedures(token);
        setDatabaseStoredProcedures(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load database stored procedures", error);
        if (isLikelyNetworkError(error)) setApiUnavailable(true);
      } finally {
        setDatabaseStoredProceduresLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadJobs = useCallback(
    async (providedToken?: string) => {
      setJobsLoading(true);
      setJobsError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await scheduledJobService.getAll(token);
        setJobs(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load jobs", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setJobsError("You do not have permission to manage jobs.");
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setJobsError("Failed to load jobs.");
        }
      } finally {
        setJobsLoaded(true);
        setJobsLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadCustomReports = useCallback(
    async (providedToken?: string) => {
      setCustomReportsLoading(true);
      setCustomReportsError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await customReportService.listRunnable(token);
        setCustomReports(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load custom reports", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setCustomReportsError(
            "You do not have permission to manage custom reports.",
          );
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setCustomReportsError("Failed to load custom reports.");
        }
      } finally {
        setCustomReportsLoadedOnce(true);
        setCustomReportsLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  /** Full definitions for the Configuration workspace (Developer+). */
  const loadCustomReportDefinitions = useCallback(
    async (providedToken?: string) => {
      setCustomReportsLoading(true);
      setCustomReportsError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await customReportService.getAll(token);
        setCustomReports(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load custom report definitions", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setCustomReportsError(
            "You do not have permission to manage custom report definitions.",
          );
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setCustomReportsError("Failed to load custom reports.");
        }
      } finally {
        setCustomReportsLoadedOnce(true);
        setCustomReportsLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const loadDatabaseViews = useCallback(
    async (providedToken?: string) => {
      setDatabaseViewsLoading(true);
      try {
        const token = providedToken ?? (await getApiToken());
        const data = await customReportService.getAvailableViews(token);
        setDatabaseViews(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load database views", error);
        if (isLikelyNetworkError(error)) setApiUnavailable(true);
      } finally {
        setDatabaseViewsLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const runCustomReport = useCallback(
    async (reportId: number, providedToken?: string) => {
      setCustomReportResultLoading(true);
      setCustomReportResultError(null);
      try {
        const token = providedToken ?? (await getApiToken());
        const result = await customReportService.run(reportId, token);
        setCustomReportResult(result);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to run custom report", error);
        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setCustomReportResultError(
            "You do not have permission to run this custom report.",
          );
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setCustomReportResultError(
            getUserFacingErrorMessage(error, "Unable to run this custom report."),
          );
        }
      } finally {
        setCustomReportResultLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  // ── Stateless export helpers ──────────────────────────────────────────────

  const exportReportCsv = useCallback(
    async (googleSheetsCompatible = false) => {
      try {
        const token = await getApiToken();
        await reportService.exportCsv(
          token,
          googleSheetsCompatible ? "cortex-report-google-sheets.csv" : "cortex-report.csv",
        );
      } catch (error) {
        console.error("Failed to export report", error);
        toast.error(getUserFacingErrorMessage(error, "Failed to export report"));
      }
    },
    [getApiToken],
  );

  const exportAdminLogsCsv = useCallback(
    async (fromUtcIso: string, toUtcIso: string) => {
      const token = await getApiToken();
      await reportService.exportAdminLogsCsv(
        token,
        fromUtcIso,
        toUtcIso,
        "cortex-request-logs.csv",
      );
    },
    [getApiToken],
  );

  // ── SLA handlers ──────────────────────────────────────────────────────────

  const handleSlaConfigurationChange = useCallback(
    (priority: string, field: "targetHours" | "warningHours", value: number) => {
      setSlaConfigurations((prev) =>
        prev.map((configuration) =>
          configuration.priority === priority
            ? { ...configuration, [field]: Number.isNaN(value) ? 0 : value }
            : configuration,
        ),
      );
    },
    [],
  );

  const saveSlaConfigurations = useCallback(async () => {
    try {
      setSlaSaving(true);
      setSlaError(null);
      const token = await getApiToken();
      const savedConfigurations = await slaService.update(slaConfigurations, token);
      setSlaConfigurations(savedConfigurations);
      toast.success("SLA settings saved");
      await refreshTicketsSilently(token);
    } catch (error) {
      console.error("Failed to save SLA settings", error);
      setSlaError(getUserFacingErrorMessage(error, "Failed to save SLA settings."));
      toast.error("Failed to save SLA settings");
    } finally {
      setSlaSaving(false);
    }
  }, [getApiToken, refreshTicketsSilently, slaConfigurations]);

  // ── Session config handlers ───────────────────────────────────────────────

  const handleSessionConfigurationChange = useCallback(
    <K extends keyof SessionConfiguration>(field: K, value: SessionConfiguration[K]) => {
      setSessionConfiguration((currentConfiguration) =>
        currentConfiguration
          ? {
              ...currentConfiguration,
              [field]:
                typeof value === "number"
                  ? Number.isNaN(value)
                    ? 0
                    : value
                  : value,
            }
          : currentConfiguration,
      );
    },
    [],
  );

  const saveSessionConfiguration = useCallback(async () => {
    if (!sessionConfiguration) return;
    try {
      setSessionSaving(true);
      setSessionError(null);
      const token = await getApiToken();
      const savedConfiguration = await sessionConfigurationService.update(
        sessionConfiguration,
        token,
      );
      setSessionConfiguration(savedConfiguration);
      toast.success("Session security policy saved");
    } catch (error) {
      console.error("Failed to save session configuration", error);
      setSessionError(
        getUserFacingErrorMessage(error, "Failed to save session configuration."),
      );
      toast.error("Failed to save session policy");
    } finally {
      setSessionSaving(false);
    }
  }, [getApiToken, sessionConfiguration]);

  // ── Notification channel handlers ─────────────────────────────────────────

  const handleNotificationChannelConfigurationChange = useCallback(
    <K extends keyof NotificationChannelConfiguration>(
      field: K,
      value: NotificationChannelConfiguration[K],
    ) => {
      setNotificationChannelConfiguration((currentConfiguration) =>
        currentConfiguration
          ? { ...currentConfiguration, [field]: value }
          : currentConfiguration,
      );
    },
    [],
  );

  const saveNotificationChannelConfiguration = useCallback(async () => {
    if (!notificationChannelConfiguration) return;
    try {
      setNotificationChannelSaving(true);
      setNotificationChannelError(null);
      const token = await getApiToken();
      const savedConfiguration =
        await notificationChannelConfigurationService.update(
          notificationChannelConfiguration,
          token,
        );
      setNotificationChannelConfiguration(savedConfiguration);
      toast.success("Notification channels saved");
    } catch (error) {
      console.error("Failed to save notification channel configuration", error);
      setNotificationChannelError(
        getUserFacingErrorMessage(
          error,
          "Failed to save notification channel settings.",
        ),
      );
      toast.error("Failed to save notification channel settings");
    } finally {
      setNotificationChannelSaving(false);
    }
  }, [getApiToken, notificationChannelConfiguration]);

  // ── Ticket board handlers ─────────────────────────────────────────────────

  const createTicketBoard = useCallback(
    async (definition: UpsertTicketBoardDefinitionInput) => {
      try {
        setTicketBoardSaving(true);
        setTicketBoardError(null);
        const token = await getApiToken();
        const createdDefinition = await ticketBoardService.create(definition, token);
        setTicketBoards((current) =>
          sortTicketBoards([...current, createdDefinition]),
        );
        toast.success("Ticket board created");
      } catch (error) {
        console.error("Failed to create ticket board", error);
        setTicketBoardError(
          getUserFacingErrorMessage(error, "Failed to create ticket board."),
        );
        toast.error("Failed to create ticket board");
        throw error;
      } finally {
        setTicketBoardSaving(false);
      }
    },
    [getApiToken],
  );

  const updateTicketBoard = useCallback(
    async (id: number, definition: UpsertTicketBoardDefinitionInput) => {
      try {
        setTicketBoardSaving(true);
        setTicketBoardError(null);
        const existingDefinition = ticketBoards.find((b) => b.id === id);
        const token = await getApiToken();
        const updatedDefinition = await ticketBoardService.update(
          id,
          definition,
          token,
        );
        setTicketBoards((current) =>
          sortTicketBoards(
            current.map((b) => (b.id === updatedDefinition.id ? updatedDefinition : b)),
          ),
        );
        // Propagate board rename/disable to active and archived tickets.
        setAllTickets((current) =>
          current.map((ticket) =>
            ticket.boardId === id
              ? {
                  ...ticket,
                  boardId: updatedDefinition.id,
                  boardName: updatedDefinition.name,
                  storyPoints: updatedDefinition.requiresStoryPoints
                    ? ticket.storyPoints ?? 1
                    : undefined,
                }
              : ticket,
          ),
        );
        setArchivedTickets((current) =>
          current.map((ticket) =>
            ticket.boardId === id
              ? {
                  ...ticket,
                  boardId: updatedDefinition.id,
                  boardName: updatedDefinition.name,
                  storyPoints: updatedDefinition.requiresStoryPoints
                    ? ticket.storyPoints ?? 1
                    : undefined,
                }
              : ticket,
          ),
        );
        setSelectedTicket((currentTicket) =>
          currentTicket && currentTicket.boardId === id
            ? {
                ...currentTicket,
                boardId: updatedDefinition.id,
                boardName: updatedDefinition.name,
                storyPoints: updatedDefinition.requiresStoryPoints
                  ? currentTicket.storyPoints ?? 1
                  : undefined,
              }
            : currentTicket,
        );
        if (
          existingDefinition?.isEnabled &&
          !updatedDefinition.isEnabled
        ) {
          setSelectedBoardId((currentId) =>
            currentId === id ? "all" : currentId,
          );
        }
        toast.success("Ticket board updated");
      } catch (error) {
        console.error("Failed to update ticket board", error);
        setTicketBoardError(
          getUserFacingErrorMessage(error, "Failed to update ticket board."),
        );
        toast.error("Failed to update ticket board");
        throw error;
      } finally {
        setTicketBoardSaving(false);
      }
    },
    [
      getApiToken,
      setAllTickets,
      setArchivedTickets,
      setSelectedBoardId,
      setSelectedTicket,
      ticketBoards,
    ],
  );

  const deleteTicketBoard = useCallback(
    async (id: number) => {
      try {
        setDeletingTicketBoardId(id);
        setTicketBoardError(null);
        const token = await getApiToken();
        await ticketBoardService.delete(id, token);
        setTicketBoards((current) => current.filter((b) => b.id !== id));
        setSelectedBoardId((currentId) => (currentId === id ? "all" : currentId));
        toast.success("Ticket board deleted");
      } catch (error) {
        console.error("Failed to delete ticket board", error);
        setTicketBoardError(
          getUserFacingErrorMessage(error, "Failed to delete ticket board."),
        );
        toast.error("Failed to delete ticket board");
        throw error;
      } finally {
        setDeletingTicketBoardId(null);
      }
    },
    [getApiToken, setSelectedBoardId],
  );

  // ── Ticket status handlers ────────────────────────────────────────────────

  const createTicketStatusDefinition = useCallback(
    async (definition: UpsertTicketStatusDefinitionInput) => {
      try {
        setTicketStatusSaving(true);
        setTicketStatusError(null);
        const token = await getApiToken();
        const createdDefinition = await ticketStatusService.create(definition, token);
        setTicketStatuses((current) =>
          sortTicketStatuses([...current, createdDefinition]),
        );
        toast.success("Ticket status created");
      } catch (error) {
        console.error("Failed to create ticket status", error);
        setTicketStatusError(
          getUserFacingErrorMessage(error, "Failed to create ticket status."),
        );
        toast.error("Failed to create ticket status");
        throw error;
      } finally {
        setTicketStatusSaving(false);
      }
    },
    [getApiToken],
  );

  const updateTicketStatusDefinition = useCallback(
    async (id: number, definition: UpsertTicketStatusDefinitionInput) => {
      try {
        setTicketStatusSaving(true);
        setTicketStatusError(null);
        const existingDefinition = ticketStatuses.find((s) => s.id === id);
        const token = await getApiToken();
        const updatedDefinition = await ticketStatusService.update(
          id,
          definition,
          token,
        );
        setTicketStatuses((current) =>
          sortTicketStatuses(
            current.map((s) =>
              s.id === updatedDefinition.id ? updatedDefinition : s,
            ),
          ),
        );
        // Propagate status rename to archive configs and tickets.
        setArchiveConfigurations((current) =>
          current.map((config) => ({
            ...config,
            eligibleStatuses: config.eligibleStatuses.map((name) =>
              name === existingDefinition?.name ? updatedDefinition.name : name,
            ),
          })),
        );
        setArchiveConfiguration((current) =>
          current
            ? {
                ...current,
                eligibleStatuses: current.eligibleStatuses.map((name) =>
                  name === existingDefinition?.name ? updatedDefinition.name : name,
                ),
              }
            : current,
        );
        setAllTickets((current) =>
          current.map((ticket) =>
            ticket.status === existingDefinition?.name
              ? { ...ticket, status: updatedDefinition.name }
              : ticket,
          ),
        );
        setArchivedTickets((current) =>
          current.map((ticket) =>
            ticket.status === existingDefinition?.name
              ? { ...ticket, status: updatedDefinition.name }
              : ticket,
          ),
        );
        setSelectedTicket((currentTicket) =>
          currentTicket && currentTicket.status === existingDefinition?.name
            ? { ...currentTicket, status: updatedDefinition.name }
            : currentTicket,
        );
        toast.success("Ticket status updated");
      } catch (error) {
        console.error("Failed to update ticket status", error);
        setTicketStatusError(
          getUserFacingErrorMessage(error, "Failed to update ticket status."),
        );
        toast.error("Failed to update ticket status");
        throw error;
      } finally {
        setTicketStatusSaving(false);
      }
    },
    [
      getApiToken,
      setAllTickets,
      setArchivedTickets,
      setSelectedTicket,
      ticketStatuses,
    ],
  );

  const deleteTicketStatusDefinition = useCallback(
    async (id: number) => {
      try {
        setDeletingTicketStatusId(id);
        setTicketStatusError(null);
        const token = await getApiToken();
        await ticketStatusService.delete(id, token);
        setTicketStatuses((current) =>
          current.filter((s) => s.id !== id),
        );
        toast.success("Ticket status deleted");
      } catch (error) {
        console.error("Failed to delete ticket status", error);
        setTicketStatusError(
          getUserFacingErrorMessage(error, "Failed to delete ticket status."),
        );
        toast.error("Failed to delete ticket status");
        throw error;
      } finally {
        setDeletingTicketStatusId(null);
      }
    },
    [getApiToken],
  );

  // ── Ticket routing rule handlers ──────────────────────────────────────────

  const handleTicketRoutingRuleChange = useCallback(
    <K extends keyof TicketRoutingRule>(field: K, value: TicketRoutingRule[K]) => {
      setSelectedTicketRoutingRule((currentRule) =>
        currentRule ? { ...currentRule, [field]: value } : currentRule,
      );
    },
    [],
  );

  const createTicketRoutingRule = useCallback(() => {
    setTicketRoutingError(null);
    setSelectedTicketRoutingRule(createDraftTicketRoutingRule());
  }, []);

  const selectTicketRoutingRule = useCallback(
    (id: number) => {
      const selectedRule = ticketRoutingRules.find((rule) => rule.id === id);
      if (!selectedRule) return;
      setTicketRoutingError(null);
      setSelectedTicketRoutingRule(selectedRule);
    },
    [ticketRoutingRules],
  );

  const saveTicketRoutingRule = useCallback(async () => {
    if (!selectedTicketRoutingRule) return;
    try {
      setTicketRoutingSaving(true);
      setTicketRoutingError(null);
      const payload: UpsertTicketRoutingRuleInput = {
        department: selectedTicketRoutingRule.department.trim() || undefined,
        titleContains: selectedTicketRoutingRule.titleContains.trim() || undefined,
        synitiOwner: selectedTicketRoutingRule.synitiOwner.trim() || undefined,
        businessOwner: selectedTicketRoutingRule.businessOwner.trim() || undefined,
        isEnabled: selectedTicketRoutingRule.isEnabled,
      };
      const token = await getApiToken();
      const isNewRule = selectedTicketRoutingRule.id === 0;
      const savedRule = isNewRule
        ? await ticketRoutingService.create(payload, token)
        : await ticketRoutingService.update(
            selectedTicketRoutingRule.id,
            payload,
            token,
          );
      setTicketRoutingRules((current) =>
        sortTicketRoutingRules(
          isNewRule
            ? [...current, savedRule]
            : current.map((rule) => (rule.id === savedRule.id ? savedRule : rule)),
        ),
      );
      setSelectedTicketRoutingRule(savedRule);
      setTicketRoutingLoadedOnce(true);
      toast.success(
        isNewRule ? "Ticket routing rule created" : "Ticket routing rule saved",
      );
    } catch (error) {
      console.error("Failed to save ticket routing rule", error);
      setTicketRoutingError(
        getUserFacingErrorMessage(error, "Failed to save ticket routing rule."),
      );
      toast.error("Failed to save ticket routing rule");
      throw error;
    } finally {
      setTicketRoutingSaving(false);
    }
  }, [getApiToken, selectedTicketRoutingRule]);

  const deleteTicketRoutingRule = useCallback(async () => {
    if (!selectedTicketRoutingRule || selectedTicketRoutingRule.id === 0) return;
    const confirmed = window.confirm(
      `Delete the routing rule for ${selectedTicketRoutingRule.department}?`,
    );
    if (!confirmed) return;
    try {
      setDeletingTicketRoutingRuleId(selectedTicketRoutingRule.id);
      setTicketRoutingError(null);
      const token = await getApiToken();
      await ticketRoutingService.delete(selectedTicketRoutingRule.id, token);
      setTicketRoutingRules((current) =>
        current.filter((rule) => rule.id !== selectedTicketRoutingRule.id),
      );
      setSelectedTicketRoutingRule((currentRule) => {
        if (currentRule?.id !== selectedTicketRoutingRule.id) return currentRule;
        const remaining = ticketRoutingRules.filter(
          (rule) => rule.id !== selectedTicketRoutingRule.id,
        );
        return remaining[0] ?? null;
      });
      toast.success("Ticket routing rule deleted");
    } catch (error) {
      console.error("Failed to delete ticket routing rule", error);
      setTicketRoutingError(
        getUserFacingErrorMessage(error, "Failed to delete ticket routing rule."),
      );
      toast.error("Failed to delete ticket routing rule");
      throw error;
    } finally {
      setDeletingTicketRoutingRuleId(null);
    }
  }, [getApiToken, selectedTicketRoutingRule, ticketRoutingRules]);

  // ── Archive configuration handlers ────────────────────────────────────────

  const handleArchiveConfigurationChange = useCallback(
    <K extends keyof ArchiveConfiguration>(field: K, value: ArchiveConfiguration[K]) => {
      setArchiveConfiguration((current) =>
        current
          ? {
              ...current,
              [field]:
                field === "archiveAfterDays" && typeof value === "number"
                  ? Number.isNaN(value)
                    ? 0
                    : value
                  : value,
            }
          : current,
      );
    },
    [],
  );

  const createArchivePolicy = useCallback(() => {
    setArchiveError(null);
    setArchiveConfiguration(createDraftArchiveConfiguration(ticketStatuses));
  }, [ticketStatuses]);

  const selectArchivePolicy = useCallback(
    (id: number) => {
      const selected = archiveConfigurations.find((c) => c.id === id);
      if (!selected) return;
      setArchiveError(null);
      setArchiveConfiguration(selected);
    },
    [archiveConfigurations],
  );

  const saveArchiveConfiguration = useCallback(async () => {
    if (!archiveConfiguration) return;
    try {
      setArchiveSaving(true);
      setArchiveError(null);
      const token = await getApiToken();
      const isNew = archiveConfiguration.id === 0;
      const saved = isNew
        ? await archiveConfigurationService.create(archiveConfiguration, token)
        : await archiveConfigurationService.update(
            archiveConfiguration.id,
            archiveConfiguration,
            token,
          );
      setArchiveConfigurations((current) =>
        sortArchiveConfigurations(
          isNew
            ? [...current, saved]
            : current.map((c) => (c.id === saved.id ? saved : c)),
        ),
      );
      setArchiveConfiguration(saved);
      toast.success(isNew ? "Archive policy created" : "Archive policy saved");
    } catch (error) {
      console.error("Failed to save archive configuration", error);
      setArchiveError(
        getUserFacingErrorMessage(error, "Failed to save archive configuration."),
      );
      toast.error("Failed to save archive configuration");
    } finally {
      setArchiveSaving(false);
    }
  }, [archiveConfiguration, getApiToken]);

  const deleteArchiveConfiguration = useCallback(async () => {
    if (!archiveConfiguration || archiveConfiguration.id === 0) return;
    const confirmed = window.confirm(
      `Delete archive policy #${archiveConfiguration.id}?`,
    );
    if (!confirmed) return;
    try {
      setDeletingArchiveConfigurationId(archiveConfiguration.id);
      setArchiveError(null);
      const token = await getApiToken();
      await archiveConfigurationService.delete(archiveConfiguration.id, token);
      const remaining = archiveConfigurations.filter(
        (c) => c.id !== archiveConfiguration.id,
      );
      setArchiveConfigurations(remaining);
      setArchiveConfiguration(remaining[0] ?? null);
      toast.success("Archive policy deleted");
    } catch (error) {
      console.error("Failed to delete archive configuration", error);
      setArchiveError(
        getUserFacingErrorMessage(error, "Failed to delete archive configuration."),
      );
      toast.error("Failed to delete archive configuration");
    } finally {
      setDeletingArchiveConfigurationId(null);
    }
  }, [archiveConfiguration, archiveConfigurations, getApiToken]);

  const runArchiveNow = useCallback(async () => {
    try {
      setArchiveRunning(true);
      setArchiveError(null);
      const token = await getApiToken();
      const result = await archiveConfigurationService.runNow(token);
      await Promise.all([
        refreshTicketsSilently(token),
        loadArchivedTickets(token, { fullCatalog: true }),
      ]);
      toast.success(
        result.archivedTicketCount === 1
          ? "Archived 1 ticket"
          : `Archived ${result.archivedTicketCount} tickets`,
      );
    } catch (error) {
      console.error("Failed to archive eligible tickets", error);
      setArchiveError(
        getUserFacingErrorMessage(error, "Failed to archive eligible tickets."),
      );
      toast.error("Failed to archive eligible tickets");
    } finally {
      setArchiveRunning(false);
    }
  }, [getApiToken, loadArchivedTickets, refreshTicketsSilently]);

  // ── Custom report handlers ────────────────────────────────────────────────

  const createCustomReport = useCallback(
    async (definition: UpsertCustomReportDefinitionInput) => {
      try {
        setCustomReportsSaving(true);
        setCustomReportsError(null);
        const token = await getApiToken();
        const createdDefinition = await customReportService.create(definition, token);
        setCustomReports((current) =>
          [...current, createdDefinition].sort((a, b) =>
            a.name.localeCompare(b.name),
          ),
        );
        if (createdDefinition.isEnabled) {
          onActiveReportSectionChange("custom");
          onSelectedCustomReportIdChange(createdDefinition.id);
        }
        await loadDatabaseViews(token);
        toast.success("Custom report created");
      } catch (error) {
        console.error("Failed to create custom report", error);
        setCustomReportsError(
          getUserFacingErrorMessage(error, "Failed to create custom report."),
        );
        toast.error("Failed to create custom report");
        throw error;
      } finally {
        setCustomReportsSaving(false);
      }
    },
    [
      getApiToken,
      loadDatabaseViews,
      onActiveReportSectionChange,
      onSelectedCustomReportIdChange,
    ],
  );

  const updateCustomReport = useCallback(
    async (id: number, definition: UpsertCustomReportDefinitionInput) => {
      try {
        setCustomReportsSaving(true);
        setCustomReportsError(null);
        const token = await getApiToken();
        const updatedDefinition = await customReportService.update(
          id,
          definition,
          token,
        );
        setCustomReports((current) =>
          current
            .map((r) => (r.id === updatedDefinition.id ? updatedDefinition : r))
            .sort((a, b) => a.name.localeCompare(b.name)),
        );
        if (!updatedDefinition.isEnabled) {
          // Reset report view if the currently-selected report was disabled.
          onActiveReportSectionChange("sla");
          onSelectedCustomReportIdChange(null);
          setCustomReportResult(null);
        } else {
          await runCustomReport(updatedDefinition.id, token);
        }
        await loadDatabaseViews(token);
        toast.success("Custom report updated");
      } catch (error) {
        console.error("Failed to update custom report", error);
        setCustomReportsError(
          getUserFacingErrorMessage(error, "Failed to update custom report."),
        );
        toast.error("Failed to update custom report");
        throw error;
      } finally {
        setCustomReportsSaving(false);
      }
    },
    [
      getApiToken,
      loadDatabaseViews,
      onActiveReportSectionChange,
      onSelectedCustomReportIdChange,
      runCustomReport,
    ],
  );

  const deleteCustomReport = useCallback(
    async (id: number) => {
      try {
        setDeletingCustomReportId(id);
        setCustomReportsError(null);
        const token = await getApiToken();
        await customReportService.delete(id, token);
        const remaining = customReports.filter((r) => r.id !== id);
        const remainingEnabled = remaining.filter((r) => r.isEnabled);
        setCustomReports(remaining);
        // Reset report view if the deleted report was currently selected.
        onSelectedCustomReportIdChange(null);
        setCustomReportResult(null);
        if (remainingEnabled.length > 0) {
          onSelectedCustomReportIdChange(remainingEnabled[0].id);
        } else {
          onActiveReportSectionChange("sla");
        }
        await loadDatabaseViews(token);
        toast.success("Custom report deleted");
      } catch (error) {
        console.error("Failed to delete custom report", error);
        setCustomReportsError(
          getUserFacingErrorMessage(error, "Failed to delete custom report."),
        );
        toast.error("Failed to delete custom report");
        throw error;
      } finally {
        setDeletingCustomReportId(null);
      }
    },
    [
      customReports,
      getApiToken,
      loadDatabaseViews,
      onActiveReportSectionChange,
      onSelectedCustomReportIdChange,
    ],
  );

  // ── Stored procedure handlers ─────────────────────────────────────────────

  const createStoredProcedureDefinition = useCallback(
    async (definition: UpsertStoredProcedureDefinitionInput) => {
      try {
        setStoredProcedureSaving(true);
        setStoredProcedureError(null);
        const token = await getApiToken();
        const createdDefinition = await storedProcedureService.create(
          definition,
          token,
        );
        setStoredProcedures((current) =>
          [...current, createdDefinition].sort((a, b) =>
            a.name.localeCompare(b.name),
          ),
        );
        await loadDatabaseStoredProcedures(token);
        toast.success("Stored procedure created");
      } catch (error) {
        console.error("Failed to create stored procedure", error);
        setStoredProcedureError(
          getUserFacingErrorMessage(error, "Failed to create stored procedure."),
        );
        toast.error("Failed to create stored procedure");
        throw error;
      } finally {
        setStoredProcedureSaving(false);
      }
    },
    [getApiToken, loadDatabaseStoredProcedures],
  );

  const updateStoredProcedureDefinition = useCallback(
    async (id: number, definition: UpsertStoredProcedureDefinitionInput) => {
      try {
        setStoredProcedureSaving(true);
        setStoredProcedureError(null);
        const token = await getApiToken();
        const updatedDefinition = await storedProcedureService.update(
          id,
          definition,
          token,
        );
        setStoredProcedures((current) =>
          current
            .map((s) => (s.id === updatedDefinition.id ? updatedDefinition : s))
            .sort((a, b) => a.name.localeCompare(b.name)),
        );
        // Propagate procedure rename to jobs.
        setJobs((currentJobs) =>
          currentJobs.map((job) =>
            job.storedProcedureDefinitionId === updatedDefinition.id
              ? { ...job, storedProcedureName: updatedDefinition.name }
              : job,
          ),
        );
        await loadDatabaseStoredProcedures(token);
        toast.success("Stored procedure updated");
      } catch (error) {
        console.error("Failed to update stored procedure", error);
        setStoredProcedureError(
          getUserFacingErrorMessage(error, "Failed to update stored procedure."),
        );
        toast.error("Failed to update stored procedure");
        throw error;
      } finally {
        setStoredProcedureSaving(false);
      }
    },
    [getApiToken, loadDatabaseStoredProcedures],
  );

  const deleteStoredProcedureDefinition = useCallback(
    async (id: number) => {
      try {
        setDeletingStoredProcedureId(id);
        setStoredProcedureError(null);
        const token = await getApiToken();
        await storedProcedureService.delete(id, token);
        setStoredProcedures((current) =>
          current.filter((s) => s.id !== id),
        );
        setJobs((currentJobs) =>
          currentJobs.map((job) =>
            job.storedProcedureDefinitionId === id
              ? {
                  ...job,
                  storedProcedureDefinitionId: undefined,
                  storedProcedureName: undefined,
                  isEnabled: false,
                  nextRunDateUtc: undefined,
                  lastRunStatus: "Failed" as const,
                  lastRunMessage:
                    "Stored procedure was deleted. Select a replacement procedure before re-enabling this job.",
                }
              : job,
          ),
        );
        void loadJobs(token);
        await loadDatabaseStoredProcedures(token);
        toast.success("Stored procedure deleted");
      } catch (error) {
        console.error("Failed to delete stored procedure", error);
        setStoredProcedureError(
          getUserFacingErrorMessage(error, "Failed to delete stored procedure."),
        );
        toast.error("Failed to delete stored procedure");
        throw error;
      } finally {
        setDeletingStoredProcedureId(null);
      }
    },
    [getApiToken, loadDatabaseStoredProcedures, loadJobs],
  );

  // ── Scheduled job handlers ────────────────────────────────────────────────

  const createScheduledJob = useCallback(
    async (job: UpsertScheduledJobInput) => {
      try {
        setJobsSaving(true);
        setJobsError(null);
        const token = await getApiToken();
        const createdJob = await scheduledJobService.create(job, token);
        setJobs((current) =>
          [...current, createdJob].sort((a, b) => a.name.localeCompare(b.name)),
        );
        toast.success("Job created");
      } catch (error) {
        console.error("Failed to create job", error);
        setJobsError(getUserFacingErrorMessage(error, "Failed to create job."));
        toast.error("Failed to create job");
        throw error;
      } finally {
        setJobsSaving(false);
      }
    },
    [getApiToken],
  );

  const updateScheduledJob = useCallback(
    async (id: number, job: UpsertScheduledJobInput) => {
      try {
        setJobsSaving(true);
        setJobsError(null);
        const token = await getApiToken();
        const updatedJob = await scheduledJobService.update(id, job, token);
        setJobs((current) =>
          current
            .map((j) => (j.id === updatedJob.id ? updatedJob : j))
            .sort((a, b) => a.name.localeCompare(b.name)),
        );
        toast.success("Job updated");
      } catch (error) {
        console.error("Failed to update job", error);
        setJobsError(getUserFacingErrorMessage(error, "Failed to update job."));
        toast.error("Failed to update job");
        throw error;
      } finally {
        setJobsSaving(false);
      }
    },
    [getApiToken],
  );

  const runScheduledJobNow = useCallback(
    async (id: number) => {
      try {
        setRunningJobId(id);
        setJobsError(null);
        const token = await getApiToken();
        const updatedJob = await scheduledJobService.runNow(id, token);
        setJobs((current) =>
          current
            .map((j) => (j.id === updatedJob.id ? updatedJob : j))
            .sort((a, b) => a.name.localeCompare(b.name)),
        );
        await Promise.all([
          refreshTicketsSilently(token),
          loadArchivedTickets(token, { fullCatalog: true }),
        ]);
        toast.success("Job ran successfully");
      } catch (error) {
        console.error("Failed to run job", error);
        setJobsError(getUserFacingErrorMessage(error, "Failed to run job."));
        toast.error("Failed to run job");
        throw error;
      } finally {
        setRunningJobId(null);
      }
    },
    [getApiToken, loadArchivedTickets, refreshTicketsSilently],
  );

  // ── Return ────────────────────────────────────────────────────────────────

  return {
    // SLA
    slaConfigurations,
    slaLoading,
    slaSaving,
    slaError,
    loadSlaConfigurations,
    handleSlaConfigurationChange,
    saveSlaConfigurations,
    // Session
    sessionConfiguration,
    sessionLoadedOnce,
    sessionLoading,
    sessionSaving,
    sessionError,
    loadSessionConfiguration,
    handleSessionConfigurationChange,
    saveSessionConfiguration,
    // Notification channels
    notificationChannelConfiguration,
    notificationChannelsLoadedOnce,
    notificationChannelLoading,
    notificationChannelSaving,
    notificationChannelError,
    loadNotificationChannelConfiguration,
    handleNotificationChannelConfigurationChange,
    saveNotificationChannelConfiguration,
    // Ticket boards
    ticketBoards,
    ticketBoardLoading,
    ticketBoardSaving,
    deletingTicketBoardId,
    ticketBoardError,
    loadTicketBoards,
    createTicketBoard,
    updateTicketBoard,
    deleteTicketBoard,
    // Ticket statuses
    ticketStatuses,
    ticketStatusLoading,
    ticketStatusSaving,
    deletingTicketStatusId,
    ticketStatusError,
    loadTicketStatuses,
    createTicketStatusDefinition,
    updateTicketStatusDefinition,
    deleteTicketStatusDefinition,
    // Ticket routing rules
    ticketRoutingRules,
    selectedTicketRoutingRule,
    ticketRoutingLoadedOnce,
    ticketRoutingLoading,
    ticketRoutingSaving,
    deletingTicketRoutingRuleId,
    ticketRoutingError,
    loadTicketRoutingRules,
    handleTicketRoutingRuleChange,
    createTicketRoutingRule,
    selectTicketRoutingRule,
    saveTicketRoutingRule,
    deleteTicketRoutingRule,
    // Archive configuration
    archiveConfigurations,
    archiveConfiguration,
    archiveLoadedOnce,
    archiveLoading,
    archiveSaving,
    archiveRunning,
    deletingArchiveConfigurationId,
    archiveError,
    loadArchiveConfigurations,
    handleArchiveConfigurationChange,
    createArchivePolicy,
    selectArchivePolicy,
    saveArchiveConfiguration,
    deleteArchiveConfiguration,
    runArchiveNow,
    // Custom reports
    customReports,
    databaseViews,
    databaseViewsLoading,
    customReportsLoadedOnce,
    customReportsLoading,
    customReportsSaving,
    deletingCustomReportId,
    customReportsError,
    customReportResult,
    customReportResultLoading,
    customReportResultError,
    loadCustomReports,
    loadCustomReportDefinitions,
    loadDatabaseViews,
    runCustomReport,
    createCustomReport,
    updateCustomReport,
    deleteCustomReport,
    exportReportCsv,
    exportAdminLogsCsv,
    // Stored procedures
    storedProcedures,
    databaseStoredProcedures,
    databaseStoredProceduresLoading,
    storedProcedureLoading,
    storedProcedureSaving,
    deletingStoredProcedureId,
    storedProcedureError,
    loadStoredProcedures,
    loadDatabaseStoredProcedures,
    createStoredProcedureDefinition,
    updateStoredProcedureDefinition,
    deleteStoredProcedureDefinition,
    // Scheduled jobs
    jobs,
    jobsLoading,
    jobsLoaded,
    jobsSaving,
    jobsError,
    runningJobId,
    loadJobs,
    createScheduledJob,
    updateScheduledJob,
    runScheduledJobNow,
    /** Clears the current custom report result — call when the active report is deselected. */
    clearCustomReportResult: () => setCustomReportResult(null),
  };
}
