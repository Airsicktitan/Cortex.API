import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
} from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { CreateTicketInput, Ticket, TicketMutationInput } from "./types/ticket";
import type { ArchivedTicket } from "./types/archivedTicket";
import type { ArchiveConfiguration } from "./types/archiveConfiguration";
import type {
  CustomReportDefinition,
  CustomReportResult,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "./types/customReport";
import type { NotificationChannelConfiguration } from "./types/notificationChannelConfiguration";
import type { UserNotification } from "./types/notification";
import type { RealtimeEvent } from "./types/realtime";
import type { ScheduledJob, UpsertScheduledJobInput } from "./types/scheduledJob";
import type { SessionConfiguration } from "./types/sessionConfiguration";
import type { SlaConfiguration } from "./types/sla";
import type {
  TicketBoardDefinition,
  UpsertTicketBoardDefinitionInput,
} from "./types/ticketBoard";
import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "./types/storedProcedure";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "./types/ticketStatus";
import type {
  TicketRoutingRule,
  UpsertTicketRoutingRuleInput,
} from "./types/ticketRouting";
import type {
  AdminUpdateUserInput,
  CreateUserInput,
  OnlineUser,
  UpdateUserProfileInput,
  UserProfile,
  UserRecord,
} from "./types/user";
import {
  ApiError,
  attachmentService,
  ticketService,
  userService,
} from "./services/api";
import { archiveConfigurationService } from "./services/archiveConfigurationService";
import { customReportService } from "./services/customReportService";
import { notificationChannelConfigurationService } from "./services/notificationChannelConfigurationService";
import { notificationService } from "./services/notificationService";
import { realtimeService } from "./services/realtimeService";
import { reportService } from "./services/reportService";
import { scheduledJobService } from "./services/scheduledJobService";
import { sessionConfigurationService } from "./services/sessionConfigurationService";
import { slaService } from "./services/slaService";
import { storedProcedureService } from "./services/storedProcedureService";
import { ticketBoardService } from "./services/ticketBoardService";
import { ticketRoutingService } from "./services/ticketRoutingService";
import { ticketStatusService } from "./services/ticketStatusService";
import TicketCard from "./components/TicketCard";
import TicketModal from "./components/TicketModal";
import ConfirmDeleteModal from "./components/ConfirmDeleteModal";
import DashboardPage from "./components/DashboardPage";
import ArchivedTicketsPage from "./components/ArchivedTicketsPage";
import ReportsPage from "./components/ReportsPage";
import ConfigurationPage from "./components/ConfigurationPage";
import JobsPage from "./components/JobsPage";
import SessionTimeoutModal from "./components/SessionTimeoutModal";
import UsersPage from "./components/UsersPage";
import UserProfileModal from "./components/UserProfileModal";
import AdminUserEditModal from "./components/AdminUserEditModal";
import AdminUserCreateModal from "./components/AdminUserCreateModal";
import SaveTicketFilterModal from "./components/SaveTicketFilterModal";
import NotificationPanel from "./components/NotificationPanel";
import {
  ConfigurationSkeleton,
  TicketGridSkeleton,
} from "./components/LoadingSkeletons";
import { applyTheme, getPreferredTheme, type ThemeMode } from "./theme";
import toast from "react-hot-toast";

const API_AUDIENCE = "https://cortex-api";
const ADMIN_PERMISSION = "admin:system";
const DEVELOPER_PERMISSION = "developer";
const TICKETS_READ_PERMISSION = "tickets:read";
const TICKETS_CREATE_PERMISSION = "tickets:create";
const TICKETS_UPDATE_PERMISSION = "tickets:update";
const API_AUTHORIZATION_PARAMS = {
  audience: API_AUDIENCE,
} as const;
const SIDEBAR_WIDTH_STORAGE_KEY = "cortex:sidebar-width";
const SESSION_LAST_ACTIVITY_STORAGE_KEY_PREFIX = "cortex:session-last-activity";
const SESSION_REAUTH_PENDING_STORAGE_KEY_PREFIX = "cortex:session-reauth-pending";
const SIDEBAR_MIN_WIDTH = 232;
const SIDEBAR_MAX_WIDTH = 440;
const SIDEBAR_DEFAULT_WIDTH = 296;
const REALTIME_REFRESH_DEBOUNCE_MS = 500;
const DEFAULT_SESSION_CONFIGURATION: SessionConfiguration = {
  inactivityTimeoutMinutes: 10,
  warningMinutes: 1,
};
const SESSION_ACTIVITY_EVENTS: ReadonlyArray<keyof WindowEventMap> = [
  "mousedown",
  "mousemove",
  "keydown",
  "scroll",
  "touchstart",
  "focus",
];

const APP_VIEW_LABELS: Record<AppView, string> = {
  dashboard: "Dashboard",
  tickets: "Tickets",
  archived: "Archived Tickets",
  reports: "Reports",
  sla: "Configuration",
  jobs: "Jobs",
  users: "Users",
};

type Permission =
  | typeof ADMIN_PERMISSION
  | typeof DEVELOPER_PERMISSION
  | typeof TICKETS_READ_PERMISSION
  | typeof TICKETS_CREATE_PERMISSION
  | typeof TICKETS_UPDATE_PERMISSION;
type FilterOption = "all" | "status" | "priority" | "sla";
type AppView =
  | "dashboard"
  | "tickets"
  | "archived"
  | "reports"
  | "sla"
  | "jobs"
  | "users";
type ReportSection = "sla" | "online-users" | "custom";
type PageSizeOption = 10 | 25 | 50 | "all";
type SessionPromptState = "warning" | "expired" | null;
type SavedTicketFilter = {
  id: string;
  name: string;
  filter: FilterOption;
  filterValue: string;
  searchQuery: string;
  pageSize: PageSizeOption;
};

const SLA_FILTER_OPTIONS = ["Breached", "At Risk", "Met"] as const;
const PAGE_SIZE_OPTIONS: ReadonlyArray<PageSizeOption> = [10, 25, 50, "all"];
const DEFAULT_TICKET_STATUS_NAMES = [
  "New",
  "In Progress",
  "Pending Business Review",
  "Resolved",
  "Closed",
] as const;
const DEFAULT_TICKET_BOARDS: ReadonlyArray<TicketBoardDefinition> = [
  {
    id: 1,
    name: "Ticket",
    description: "Standard operational ticket board.",
    requiresStoryPoints: false,
    isEnabled: true,
    createdDateUtc: "",
  },
  {
    id: 2,
    name: "Hypercare",
    description: "High-touch stabilization and production support work.",
    requiresStoryPoints: false,
    isEnabled: true,
    createdDateUtc: "",
  },
  {
    id: 3,
    name: "Enhancement",
    description: "Planned improvements and backlog work.",
    requiresStoryPoints: true,
    isEnabled: true,
    createdDateUtc: "",
  },
] as const;

function clampSidebarWidth(width: number) {
  const maxWidth =
    typeof window === "undefined"
      ? SIDEBAR_MAX_WIDTH
      : Math.min(SIDEBAR_MAX_WIDTH, Math.max(320, Math.floor(window.innerWidth * 0.24)));

  return Math.min(maxWidth, Math.max(SIDEBAR_MIN_WIDTH, width));
}

function readStoredTimestamp(storageKey: string) {
  if (typeof window === "undefined") {
    return null;
  }

  const rawValue = window.sessionStorage.getItem(storageKey);
  if (!rawValue) {
    return null;
  }

  const parsedValue = Number(rawValue);
  return Number.isFinite(parsedValue) ? parsedValue : null;
}

function getInitialSidebarWidth() {
  if (typeof window === "undefined") {
    return SIDEBAR_DEFAULT_WIDTH;
  }

  const storedValue = Number(window.localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY));
  if (Number.isNaN(storedValue)) {
    return SIDEBAR_DEFAULT_WIDTH;
  }

  return clampSidebarWidth(storedValue);
}

function useDebouncedValue<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}

function normalize(value: string) {
  return value.trim().toLowerCase();
}

function isFilterOption(value: string): value is FilterOption {
  return (
    value === "all" ||
    value === "status" ||
    value === "priority" ||
    value === "sla"
  );
}

function isPageSizeOption(value: unknown): value is PageSizeOption {
  return value === "all" || value === 10 || value === 25 || value === 50;
}

function createSavedFilterId() {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function parseSavedFilters(rawValue: string | null): SavedTicketFilter[] {
  if (!rawValue) {
    return [];
  }

  try {
    const parsed = JSON.parse(rawValue) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.flatMap((item) => {
      if (typeof item !== "object" || item === null) {
        return [];
      }

      const candidate = item as Partial<SavedTicketFilter>;
      const filterOption = candidate.filter;
      const pageSizeValue =
        candidate.pageSize === "all"
          ? "all"
          : Number(candidate.pageSize ?? 0);

      if (
        typeof candidate.id !== "string" ||
        typeof candidate.name !== "string" ||
        typeof filterOption !== "string" ||
        !isFilterOption(filterOption) ||
        !isPageSizeOption(pageSizeValue)
      ) {
        return [];
      }

      return [
        {
          id: candidate.id,
          name: candidate.name,
          filter: filterOption,
          filterValue:
            typeof candidate.filterValue === "string" ? candidate.filterValue : "",
          searchQuery:
            typeof candidate.searchQuery === "string"
              ? candidate.searchQuery
              : "",
          pageSize: pageSizeValue,
        },
      ];
    });
  } catch {
    return [];
  }
}

function ticketMatchesSearch(ticket: Ticket, searchValue: string) {
  const searchableValues = [
    ticket.id,
    ticket.title,
    ticket.description,
    ticket.boardName,
    ticket.storyPoints,
    ticket.status,
    ticket.priority,
    ticket.synitiOwner,
    ticket.businessOwner,
    ticket.createdByDisplayName,
  ];

  return searchableValues.some((value) =>
    normalize(String(value ?? "")).includes(searchValue),
  );
}

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const payload = token.split(".")[1];
  if (!payload) return null;

  try {
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
    return JSON.parse(atob(padded)) as Record<string, unknown>;
  } catch (error) {
    console.error("Failed to decode token payload", error);
    return null;
  }
}

function parsePermissionsFromToken(token: string | undefined): string[] {
  if (!token) return [];

  const payload = decodeJwtPayload(token);
  const value = payload?.permissions;

  if (Array.isArray(value)) {
    return value.filter((permission): permission is string => {
      return typeof permission === "string";
    });
  }

  if (typeof value === "string" && value.trim()) {
    return [value];
  }

  return [];
}

function isConsentRequiredError(error: unknown) {
  if (typeof error !== "object" || error === null) return false;

  const authError = error as {
    error?: string;
    message?: string;
  };

  return (
    authError.error === "consent_required" ||
    authError.message?.toLowerCase().includes("consent required") === true
  );
}

function isForbiddenError(error: unknown) {
  return error instanceof ApiError && error.status === 403;
}

function isApiUnavailableError(error: unknown) {
  if (error instanceof ApiError) {
    return false;
  }

  if (error instanceof TypeError) {
    return true;
  }

  if (error instanceof Error) {
    const normalizedMessage = error.message.toLowerCase();
    return (
      normalizedMessage.includes("failed to fetch") ||
      normalizedMessage.includes("networkerror") ||
      normalizedMessage.includes("load failed")
    );
  }

  return false;
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError && error.message.trim()) {
    return error.message;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

function isUserExpired(user: UserProfile | null) {
  if (!user?.expiryDate) {
    return false;
  }

  const expiryDate = new Date(user.expiryDate);
  if (Number.isNaN(expiryDate.getTime())) {
    return false;
  }

  return expiryDate.getTime() <= Date.now();
}

function isUserInactive(user: UserProfile | null) {
  return user?.isActive === false;
}

function getEnabledTicketStatuses(statuses: TicketStatusDefinition[]) {
  return statuses.filter((status) => status.isEnabled);
}

function getDefaultTicketStatusName(statuses: TicketStatusDefinition[]) {
  const enabledStatuses = getEnabledTicketStatuses(statuses);

  return (
    enabledStatuses.find((status) => status.name === "New")?.name ??
    enabledStatuses[0]?.name ??
    "New"
  );
}

function getDefaultTicketBoard(boards: TicketBoardDefinition[]) {
  const enabledBoards = boards.filter((board) => board.isEnabled);

  return (
    enabledBoards.find((board) => board.name === "Ticket") ??
    enabledBoards[0] ??
    DEFAULT_TICKET_BOARDS[0]
  );
}

function getDefaultArchiveEligibleStatuses(statuses: TicketStatusDefinition[]) {
  const preferredStatuses = statuses
    .filter((status) =>
      status.name === "Resolved" || status.name === "Closed",
    )
    .map((status) => status.name);

  return preferredStatuses;
}

function sortTicketStatuses(statuses: TicketStatusDefinition[]) {
  return [...statuses].sort((left, right) => left.id - right.id);
}

function sortTicketBoards(boards: TicketBoardDefinition[]) {
  return [...boards].sort((left, right) => {
    const leftIsDefault = left.name.toLowerCase() === "ticket";
    const rightIsDefault = right.name.toLowerCase() === "ticket";

    if (leftIsDefault && !rightIsDefault) {
      return -1;
    }

    if (!leftIsDefault && rightIsDefault) {
      return 1;
    }

    return left.name.localeCompare(right.name);
  });
}

function sortTicketRoutingRules(rules: TicketRoutingRule[]) {
  return [...rules].sort((left, right) => {
    const leftKey =
      `${left.titleContains}|${left.department}|${left.synitiOwner}|${left.businessOwner}`.toLowerCase();
    const rightKey =
      `${right.titleContains}|${right.department}|${right.synitiOwner}|${right.businessOwner}`.toLowerCase();
    const keyComparison = leftKey.localeCompare(rightKey);
    if (keyComparison !== 0) {
      return keyComparison;
    }

    return left.id - right.id;
  });
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

async function loadBootstrapData(token: string) {
  const [fetchedTickets, fetchedCurrentUser] = await Promise.all([
    ticketService.getAll(token),
    userService.getCurrentUser(token).catch(
      (error) => {
        console.warn("Current user profile could not be loaded", error);
        return null;
      },
    ),
  ]);

  return { fetchedCurrentUser, fetchedTickets };
}

function createDraftTicket(
  statuses: TicketStatusDefinition[],
  boards: TicketBoardDefinition[],
  createdByDisplayName = "",
  department = "",
): Ticket {
  const createdDate = new Date().toISOString();
  const targetDate = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();
  const defaultBoard = getDefaultTicketBoard(boards);

  return {
    id: "",
    title: "",
    description: "",
    priority: "Medium",
    status: getDefaultTicketStatusName(statuses),
    boardId: defaultBoard.id,
    boardName: defaultBoard.name,
    storyPoints: defaultBoard.requiresStoryPoints ? 1 : undefined,
    department,
    createdDate,
    slaTargetDate: targetDate,
    slaStatus: "On Track",
    slaRemainingMinutes: 24 * 60,
    isSlaBreached: false,
    createdByDisplayName,
  } as Ticket;
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

function sortArchiveConfigurations(configurations: ArchiveConfiguration[]) {
  return [...configurations].sort((left, right) => {
    if (left.archiveAfterDays !== right.archiveAfterDays) {
      return left.archiveAfterDays - right.archiveAfterDays;
    }

    return left.id - right.id;
  });
}

function App() {
  const {
    isAuthenticated,
    isLoading,
    user,
    logout,
    getAccessTokenSilently,
    getAccessTokenWithPopup,
    loginWithRedirect,
  } = useAuth0();

  const [activeView, setActiveView] = useState<AppView>("tickets");
  const [theme, setTheme] = useState<ThemeMode>(getPreferredTheme);
  const [allTickets, setAllTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [apiUnavailable, setApiUnavailable] = useState(false);
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);

  const [filter, setFilter] = useState<FilterOption>("all");
  const [filterValue, setFilterValue] = useState("");
  const debouncedFilterValue = useDebouncedValue(filterValue, 300);
  const [selectedBoardId, setSelectedBoardId] = useState<number | "all">("all");
  const [searchQuery, setSearchQuery] = useState("");
  const debouncedSearchQuery = useDebouncedValue(searchQuery, 300);
  const [pageSize, setPageSize] = useState<PageSizeOption>(10);
  const [currentPage, setCurrentPage] = useState(1);
  const [showReportSlaLegend, setShowReportSlaLegend] = useState(false);
  const [activeReportSection, setActiveReportSection] =
    useState<ReportSection>("sla");
  const [selectedCustomReportId, setSelectedCustomReportId] = useState<number | null>(
    null,
  );
  const [savedFilters, setSavedFilters] = useState<SavedTicketFilter[]>([]);
  const [selectedSavedFilterId, setSelectedSavedFilterId] = useState("");
  const [isSaveFilterModalOpen, setIsSaveFilterModalOpen] = useState(false);
  const [savedFilterName, setSavedFilterName] = useState("");

  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [latestRealtimeEvent, setLatestRealtimeEvent] = useState<RealtimeEvent | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [archivedTickets, setArchivedTickets] = useState<ArchivedTicket[]>([]);
  const [archivedLoading, setArchivedLoading] = useState(false);
  const [archivedError, setArchivedError] = useState<string | null>(null);
  const [highlightedArchivedTicketId, setHighlightedArchivedTicketId] =
    useState<string | null>(null);
  const [reactivatingArchivedTicketId, setReactivatingArchivedTicketId] =
    useState<string | null>(null);

  const [permissions, setPermissions] = useState<string[]>([]);
  const [permissionsLoaded, setPermissionsLoaded] = useState(false);
  const [needsConsent, setNeedsConsent] = useState(false);

  const [ticketToDelete, setTicketToDelete] = useState<Ticket | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [slaConfigurations, setSlaConfigurations] = useState<SlaConfiguration[]>(
    [],
  );
  const [slaLoading, setSlaLoading] = useState(false);
  const [slaSaving, setSlaSaving] = useState(false);
  const [slaError, setSlaError] = useState<string | null>(null);
  const [sessionConfiguration, setSessionConfiguration] =
    useState<SessionConfiguration | null>(null);
  const [sessionLoadedOnce, setSessionLoadedOnce] = useState(false);
  const [sessionLoading, setSessionLoading] = useState(false);
  const [sessionSaving, setSessionSaving] = useState(false);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const [notificationChannelConfiguration, setNotificationChannelConfiguration] =
    useState<NotificationChannelConfiguration | null>(null);
  const [notificationChannelsLoadedOnce, setNotificationChannelsLoadedOnce] =
    useState(false);
  const [notificationChannelLoading, setNotificationChannelLoading] =
    useState(false);
  const [notificationChannelSaving, setNotificationChannelSaving] =
    useState(false);
  const [notificationChannelError, setNotificationChannelError] =
    useState<string | null>(null);
  const [ticketBoards, setTicketBoards] = useState<TicketBoardDefinition[]>([]);
  const [ticketBoardLoading, setTicketBoardLoading] = useState(false);
  const [ticketBoardSaving, setTicketBoardSaving] = useState(false);
  const [deletingTicketBoardId, setDeletingTicketBoardId] = useState<
    number | null
  >(null);
  const [ticketBoardError, setTicketBoardError] = useState<string | null>(null);
  const [sessionPromptState, setSessionPromptState] =
    useState<SessionPromptState>(null);
  const [sessionRemainingSeconds, setSessionRemainingSeconds] = useState(
    DEFAULT_SESSION_CONFIGURATION.inactivityTimeoutMinutes * 60,
  );
  const [ticketStatuses, setTicketStatuses] = useState<TicketStatusDefinition[]>(
    [],
  );
  const [ticketStatusLoading, setTicketStatusLoading] = useState(false);
  const [ticketStatusSaving, setTicketStatusSaving] = useState(false);
  const [deletingTicketStatusId, setDeletingTicketStatusId] = useState<
    number | null
  >(null);
  const [ticketStatusError, setTicketStatusError] = useState<string | null>(
    null,
  );
  const [ticketRoutingRules, setTicketRoutingRules] = useState<TicketRoutingRule[]>(
    [],
  );
  const [selectedTicketRoutingRule, setSelectedTicketRoutingRule] =
    useState<TicketRoutingRule | null>(null);
  const [ticketRoutingLoadedOnce, setTicketRoutingLoadedOnce] = useState(false);
  const [ticketRoutingLoading, setTicketRoutingLoading] = useState(false);
  const [ticketRoutingSaving, setTicketRoutingSaving] = useState(false);
  const [deletingTicketRoutingRuleId, setDeletingTicketRoutingRuleId] =
    useState<number | null>(null);
  const [ticketRoutingError, setTicketRoutingError] = useState<string | null>(
    null,
  );
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
  const [customReportResultLoading, setCustomReportResultLoading] =
    useState(false);
  const [customReportResultError, setCustomReportResultError] =
    useState<string | null>(null);
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
  const [jobs, setJobs] = useState<ScheduledJob[]>([]);
  const [jobsLoading, setJobsLoading] = useState(false);
  const [jobsLoaded, setJobsLoaded] = useState(false);
  const [jobsSaving, setJobsSaving] = useState(false);
  const [jobsError, setJobsError] = useState<string | null>(null);
  const [runningJobId, setRunningJobId] = useState<number | null>(null);
  const [notifications, setNotifications] = useState<UserNotification[]>([]);
  const [notificationsLoading, setNotificationsLoading] = useState(false);
  const [notificationsLoaded, setNotificationsLoaded] = useState(false);
  const [notificationsError, setNotificationsError] = useState<string | null>(null);
  const [notificationUnreadCount, setNotificationUnreadCount] = useState(0);
  const [isNotificationPanelOpen, setIsNotificationPanelOpen] = useState(false);
  const [markingNotificationId, setMarkingNotificationId] = useState<number | null>(
    null,
  );
  const [markingAllNotificationsRead, setMarkingAllNotificationsRead] =
    useState(false);
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [onlineUsers, setOnlineUsers] = useState<OnlineUser[]>([]);
  const [onlineUsersLoading, setOnlineUsersLoading] = useState(false);
  const [onlineUsersError, setOnlineUsersError] = useState<string | null>(null);
  const [isCreateUserModalOpen, setIsCreateUserModalOpen] = useState(false);
  const [createUserDraft, setCreateUserDraft] = useState<CreateUserInput>({
    displayName: "",
    nickName: "",
    email: "",
    password: "",
    phoneNumber: "",
    department: "",
    role: "User",
    isActive: true,
    expiryDate: "",
  });
  const [createUserSaving, setCreateUserSaving] = useState(false);
  const [editingAdminUser, setEditingAdminUser] = useState<UserRecord | null>(
    null,
  );
  const [adminUserDraft, setAdminUserDraft] = useState<AdminUpdateUserInput>({});
  const [adminUserSaving, setAdminUserSaving] = useState(false);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);
  const [isAppMenuOpen, setIsAppMenuOpen] = useState(false);
  const [sidebarWidth, setSidebarWidth] = useState(getInitialSidebarWidth);
  const [isSidebarResizing, setIsSidebarResizing] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileDraft, setProfileDraft] = useState<UpdateUserProfileInput>({});
  const appMenuRef = useRef<HTMLDivElement | null>(null);
  const sidebarResizeStartXRef = useRef(0);
  const sidebarResizeStartWidthRef = useRef(sidebarWidth);
  const userMenuRef = useRef<HTMLDivElement | null>(null);
  const notificationPanelRef = useRef<HTMLDivElement | null>(null);
  const sessionPromptStateRef = useRef<SessionPromptState>(null);
  const sessionLastActivityAtRef = useRef(Date.now());
  const lastPresenceSyncAtRef = useRef(0);
  const presenceSyncInFlightRef = useRef(false);
  const realtimeRefreshTimerRef = useRef<number | null>(null);

  const permissionSet = useMemo(() => new Set(permissions), [permissions]);
  const isAdmin = permissionSet.has(ADMIN_PERMISSION);
  const isDeveloper = permissionSet.has(DEVELOPER_PERMISSION);
  const isDarkMode = theme === "dark";
  const isAccountExpired = isUserExpired(currentUser);
  const isAccountInactive = isUserInactive(currentUser);
  const effectiveSessionConfiguration =
    sessionConfiguration ?? DEFAULT_SESSION_CONFIGURATION;
  const sessionTimeoutSeconds = Math.max(
    60,
    effectiveSessionConfiguration.inactivityTimeoutMinutes * 60,
  );
  const sessionWarningSeconds = Math.max(
    0,
    Math.min(
      sessionTimeoutSeconds - 1,
      effectiveSessionConfiguration.warningMinutes * 60,
    ),
  );
  const savedFilterStorageKey = useMemo(() => {
    if (currentUser?.id) {
      return `cortex:saved-ticket-filters:${currentUser.id}`;
    }

    if (user?.sub) {
      return `cortex:saved-ticket-filters:${user.sub}`;
    }

    return "cortex:saved-ticket-filters";
  }, [currentUser?.id, user?.sub]);
  const sessionStorageIdentity = useMemo(
    () => user?.sub ?? currentUser?.id?.toString() ?? "current-user",
    [currentUser?.id, user?.sub],
  );
  const sessionLastActivityStorageKey = useMemo(
    () => `${SESSION_LAST_ACTIVITY_STORAGE_KEY_PREFIX}:${sessionStorageIdentity}`,
    [sessionStorageIdentity],
  );
  const sessionReauthPendingStorageKey = useMemo(
    () => `${SESSION_REAUTH_PENDING_STORAGE_KEY_PREFIX}:${sessionStorageIdentity}`,
    [sessionStorageIdentity],
  );

  const hasPermission = (permission: Permission) => {
    return isAdmin || permissionSet.has(permission);
  };

  const canCreateTickets =
    permissionsLoaded &&
    !needsConsent &&
    hasPermission(TICKETS_CREATE_PERMISSION);
  const canUpdateTickets =
    permissionsLoaded &&
    !needsConsent &&
    hasPermission(TICKETS_UPDATE_PERMISSION);
  const canViewTicketSections =
    permissionsLoaded &&
    !needsConsent &&
    hasPermission(TICKETS_READ_PERMISSION);
  const canViewDashboard = canViewTicketSections;
  const canViewReports = canViewTicketSections;
  const canViewOnlineUsersReport =
    permissionsLoaded && !needsConsent && (isAdmin || isDeveloper);
  const canManageCustomReports = canViewOnlineUsersReport;
  const canViewArchived = canViewTicketSections;
  const canManageConfiguration =
    permissionsLoaded && !needsConsent && (isAdmin || isDeveloper);
  const canManageJobs =
    permissionsLoaded && !needsConsent && (isAdmin || isDeveloper);
  const canViewUsers =
    permissionsLoaded && !needsConsent && (isAdmin || isDeveloper);
  const canCreateUsers = canViewUsers;
  const canEditUsers = permissionsLoaded && !needsConsent && isAdmin;
  const canDeleteUsers = permissionsLoaded && !needsConsent && isAdmin;
  const failedJobsCount = useMemo(
    () => jobs.filter((job) => job.lastRunStatus === "Failed").length,
    [jobs],
  );
  const activeViewLabel = APP_VIEW_LABELS[activeView];
  const navigationItems = useMemo(() => {
    const items: Array<{
      view: AppView;
      label: string;
      description: string;
      enabled: boolean;
    }> = [
      {
        view: "dashboard",
        label: "Dashboard",
        description: "See queue health and quick operational summaries.",
        enabled: canViewDashboard,
      },
      {
        view: "tickets",
        label: "Tickets",
        description: "Browse and manage the active ticket queue.",
        enabled: canViewTicketSections,
      },
      {
        view: "archived",
        label: "Archived Tickets",
        description: "Review tickets moved out of the active queue.",
        enabled: canViewArchived,
      },
      {
        view: "reports",
        label: "Reports",
        description: "Drill into SLA trends and detailed reporting.",
        enabled: canViewReports,
      },
      {
        view: "jobs",
        label: "Jobs",
        description: "Create and manage background automation jobs.",
        enabled: canManageJobs,
      },
      {
        view: "sla",
        label: "Configuration",
        description: "Manage SLA rules and archive policy.",
        enabled: canManageConfiguration,
      },
      {
        view: "users",
        label: "Users",
        description: "Manage the registered user directory.",
        enabled: canViewUsers,
      },
    ];

    return items.filter((item) => item.enabled || item.view === activeView);
  }, [
    activeView,
    canManageJobs,
    canManageConfiguration,
    canViewArchived,
    canViewDashboard,
    canViewReports,
    canViewTicketSections,
    canViewUsers,
  ]);
  const activeNavigationItem =
    navigationItems.find((item) => item.view === activeView) ?? null;

  const getApiToken = useCallback(async () => {
    return await getAccessTokenSilently({
      authorizationParams: API_AUTHORIZATION_PARAMS,
    });
  }, [getAccessTokenSilently]);

  const persistSessionLastActivity = useCallback(
    (timestamp: number) => {
      if (typeof window === "undefined") {
        return;
      }

      window.sessionStorage.setItem(
        sessionLastActivityStorageKey,
        String(timestamp),
      );
    },
    [sessionLastActivityStorageKey],
  );

  const clearSessionTimeoutState = useCallback(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.sessionStorage.removeItem(sessionLastActivityStorageKey);
    window.sessionStorage.removeItem(sessionReauthPendingStorageKey);
  }, [sessionLastActivityStorageKey, sessionReauthPendingStorageKey]);

  const markSessionActivity = useCallback(
    (timestamp = Date.now()) => {
      sessionLastActivityAtRef.current = timestamp;
      sessionPromptStateRef.current = null;
      persistSessionLastActivity(timestamp);
      setSessionRemainingSeconds(sessionTimeoutSeconds);
      setSessionPromptState(null);
    },
    [persistSessionLastActivity, sessionTimeoutSeconds],
  );

  const performLogout = useCallback(() => {
    sessionPromptStateRef.current = null;
    clearSessionTimeoutState();
    logout({
      logoutParams: { returnTo: window.location.origin },
    });
  }, [clearSessionTimeoutState, logout]);

  const refreshTicketsSilently = async (providedToken?: string) => {
    try {
      const token = providedToken ?? (await getApiToken());
      const data = await ticketService.getAll(token);
      setAllTickets(data);
      setApiUnavailable(false);
    } catch (error) {
      console.error("Failed to refresh tickets silently", error);

      if (isApiUnavailableError(error)) {
        setApiUnavailable(true);
      }
    }
  };

  const loadAllTickets = useCallback(
    async (providedToken?: string) => {
      setLoading(true);
      setError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await ticketService.getAll(token);
        setAllTickets(data);
        setNeedsConsent(false);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load tickets", error);

        if (isConsentRequiredError(error)) {
          setApiUnavailable(false);
          setNeedsConsent(true);
          setError("CORTEX API consent is required before tickets can load.");
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setError("You do not have permission to view tickets.");
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setError("Failed to load tickets. Make sure the API is running.");
        }
      } finally {
        setLoading(false);
      }
    },
    [getApiToken],
  );

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
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setSlaError("Failed to load SLA settings.");
        }
      } finally {
        setSlaLoading(false);
      }
    },
    [getApiToken],
  );

  const continueSessionAfterWarning = useCallback(() => {
    markSessionActivity();
  }, [markSessionActivity]);

  const reauthenticateDueToInactivity = useCallback(() => {
    sessionPromptStateRef.current = "expired";
    setSessionPromptState("expired");
    setIsUserMenuOpen(false);
    setIsAppMenuOpen(false);

    if (typeof window !== "undefined") {
      window.sessionStorage.setItem(sessionReauthPendingStorageKey, "1");
    }

    void loginWithRedirect({
      authorizationParams: {
        ...API_AUTHORIZATION_PARAMS,
        prompt: "login",
        max_age: 0,
      },
    });
  }, [loginWithRedirect, sessionReauthPendingStorageKey]);

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

        if (isApiUnavailableError(error)) {
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
    [getApiToken],
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

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setNotificationChannelError(
            "You do not have permission to view notification channel settings.",
          );
        } else {
          setApiUnavailable(false);
          setNotificationChannelError(
            "Failed to load notification channel settings.",
          );
        }
      } finally {
        setNotificationChannelsLoadedOnce(true);
        setNotificationChannelLoading(false);
      }
    },
    [getApiToken],
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

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setTicketBoardError("You do not have permission to view ticket boards.");
        } else {
          setApiUnavailable(false);
          setTicketBoardError("Failed to load ticket boards.");
        }
      } finally {
        setTicketBoardLoading(false);
      }
    },
    [getApiToken],
  );

  const syncPresence = useCallback(
    async (providedToken?: string) => {
      if (!isAuthenticated || presenceSyncInFlightRef.current) {
        return;
      }

      presenceSyncInFlightRef.current = true;

      try {
        const token = providedToken ?? (await getApiToken());
        await userService.updatePresence(token);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to update presence", error);

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        }
      } finally {
        presenceSyncInFlightRef.current = false;
      }
    },
    [getApiToken, isAuthenticated],
  );

  const syncPresenceIfDue = useCallback(
    (force = false, providedToken?: string) => {
      if (!isAuthenticated || sessionPromptStateRef.current === "expired") {
        return;
      }

      const now = Date.now();
      if (!force && now - lastPresenceSyncAtRef.current < 60_000) {
        return;
      }

      lastPresenceSyncAtRef.current = now;
      void syncPresence(providedToken);
    },
    [isAuthenticated, syncPresence],
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

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setTicketStatusError("Failed to load ticket statuses.");
        }
      } finally {
        setTicketStatusLoading(false);
      }
    },
    [getApiToken],
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
          if (currentRule?.id === 0) {
            return currentRule;
          }

          if (currentRule) {
            const matchingRule = data.find((rule) => rule.id === currentRule.id);
            if (matchingRule) {
              return matchingRule;
            }
          }

          return data[0] ?? null;
        });
        setTicketRoutingLoadedOnce(true);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load ticket routing rules", error);

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setTicketRoutingError("Failed to load ticket routing rules.");
        }
      } finally {
        setTicketRoutingLoading(false);
      }
    },
    [getApiToken],
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
          if (currentConfiguration?.id === 0) {
            return currentConfiguration;
          }

          if (currentConfiguration) {
            const matchingConfiguration = data.find(
              (configuration) => configuration.id === currentConfiguration.id,
            );

            if (matchingConfiguration) {
              return matchingConfiguration;
            }
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
        } else if (isApiUnavailableError(error)) {
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
    [getApiToken],
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
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setStoredProcedureError("Failed to load stored procedures.");
        }
      } finally {
        setStoredProcedureLoading(false);
      }
    },
    [getApiToken],
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

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        }
      } finally {
        setDatabaseStoredProceduresLoading(false);
      }
    },
    [getApiToken],
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
        } else if (isApiUnavailableError(error)) {
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
    [getApiToken],
  );

  const loadNotifications = useCallback(
    async (providedToken?: string, options?: { silent?: boolean }) => {
      const silent = options?.silent ?? false;

      if (!silent) {
        setNotificationsLoading(true);
      }

      setNotificationsError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const feed = await notificationService.getFeed(token, 20);
        setNotifications(feed.items);
        setNotificationUnreadCount(feed.unreadCount);
        setNotificationsLoaded(true);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load notifications", error);

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setNotificationsError("You do not have permission to view notifications.");
        } else {
          setApiUnavailable(false);
          setNotificationsError("Failed to load notifications.");
        }
      } finally {
        if (!silent) {
          setNotificationsLoading(false);
        }
      }
    },
    [getApiToken],
  );

  const loadUsers = useCallback(
    async (providedToken?: string) => {
      setUsersLoading(true);
      setUsersError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await userService.getAll(token);
        setUsers(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load users", error);

        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setUsersError("You do not have permission to view users.");
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setUsersError("Failed to load users.");
        }
      } finally {
        setUsersLoading(false);
      }
    },
    [getApiToken],
  );

  const loadOnlineUsers = useCallback(
    async (providedToken?: string) => {
      setOnlineUsersLoading(true);
      setOnlineUsersError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await userService.getOnlineUsers(token);
        setOnlineUsers(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load online users", error);

        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setOnlineUsersError(
            "You do not have permission to view online users.",
          );
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setOnlineUsersError("Failed to load online users.");
        }
      } finally {
        setOnlineUsersLoading(false);
      }
    },
    [getApiToken],
  );

  const loadCustomReports = useCallback(
    async (providedToken?: string) => {
      setCustomReportsLoading(true);
      setCustomReportsError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await customReportService.getAll(token);
        setCustomReports(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load custom reports", error);

        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setCustomReportsError(
            "You do not have permission to manage custom reports.",
          );
        } else if (isApiUnavailableError(error)) {
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
    [getApiToken],
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

        if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        }
      } finally {
        setDatabaseViewsLoading(false);
      }
    },
    [getApiToken],
  );

  const exportReportCsv = useCallback(
    async (googleSheetsCompatible = false) => {
      try {
        const token = await getApiToken();
        await reportService.exportCsv(
          token,
          googleSheetsCompatible
            ? "cortex-report-google-sheets.csv"
            : "cortex-report.csv",
        );
      } catch (error) {
        console.error("Failed to export report", error);
        toast.error(getErrorMessage(error, "Failed to export report"));
      }
    },
    [getApiToken],
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
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setCustomReportResultError("Failed to run custom report.");
        }
      } finally {
        setCustomReportResultLoading(false);
      }
    },
    [getApiToken],
  );

  const loadArchivedTickets = useCallback(
    async (providedToken?: string) => {
      setArchivedLoading(true);
      setArchivedError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await ticketService.getArchived(token);
        setArchivedTickets(data);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load archived tickets", error);

        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setArchivedError("You do not have permission to view archived tickets.");
        } else if (isApiUnavailableError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setArchivedError("Failed to load archived tickets.");
        }
      } finally {
        setArchivedLoading(false);
      }
    },
    [getApiToken],
  );

  const refreshTicketDataFromRealtime = useCallback(
    async (providedToken?: string) => {
      const token = providedToken ?? (await getApiToken());
      await Promise.allSettled([
        loadAllTickets(token),
        loadArchivedTickets(token),
      ]);
    },
    [getApiToken, loadAllTickets, loadArchivedTickets],
  );

  const scheduleRealtimeTicketRefresh = useCallback(
    (event: RealtimeEvent, providedToken?: string) => {
      if (!event?.eventType || typeof event.eventType !== "string") {
        return;
      }

      if (
        !event.eventType.startsWith("ticket.") &&
        !event.eventType.startsWith("comment.") &&
        !event.eventType.startsWith("attachment.")
      ) {
        return;
      }

      if (realtimeRefreshTimerRef.current !== null) {
        window.clearTimeout(realtimeRefreshTimerRef.current);
      }

      realtimeRefreshTimerRef.current = window.setTimeout(() => {
        realtimeRefreshTimerRef.current = null;
        void refreshTicketDataFromRealtime(providedToken);
      }, REALTIME_REFRESH_DEBOUNCE_MS);
    },
    [refreshTicketDataFromRealtime],
  );

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    sessionPromptStateRef.current = sessionPromptState;
  }, [sessionPromptState]);

  useEffect(() => {
    setSavedFilters(
      parseSavedFilters(window.localStorage.getItem(savedFilterStorageKey)),
    );
    setSelectedSavedFilterId("");
  }, [savedFilterStorageKey]);

  useEffect(() => {
    window.localStorage.setItem(
      savedFilterStorageKey,
      JSON.stringify(savedFilters),
    );
  }, [savedFilters, savedFilterStorageKey]);

  useEffect(() => {
    sidebarResizeStartWidthRef.current = sidebarWidth;

    window.localStorage.setItem(
      SIDEBAR_WIDTH_STORAGE_KEY,
      String(clampSidebarWidth(sidebarWidth)),
    );
  }, [sidebarWidth]);

  useEffect(() => {
    const handleResize = () => {
      setSidebarWidth((currentWidth) => clampSidebarWidth(currentWidth));
    };

    window.addEventListener("resize", handleResize);
    return () => window.removeEventListener("resize", handleResize);
  }, []);

  useEffect(() => {
    return () => {
      if (realtimeRefreshTimerRef.current !== null) {
        window.clearTimeout(realtimeRefreshTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!isAuthenticated || !permissionsLoaded || needsConsent || !canViewTicketSections) {
      return;
    }

    const connection = realtimeService.connect({
      getToken: getApiToken,
      onEvent: (event) => {
        if (!event?.eventType || typeof event.eventType !== "string") {
          return;
        }

        setLatestRealtimeEvent(event);

        if (event.eventType === "notification.created") {
          const recipients = event.recipientUserIds ?? [];
          if (
            currentUser &&
            (recipients.length === 0 || recipients.includes(currentUser.id))
          ) {
            void loadNotifications(undefined, { silent: true });
          }
          return;
        }

        scheduleRealtimeTicketRefresh(event);
      },
      onError: (error) => {
        console.error("Realtime connection issue", error);
      },
    });

    return () => {
      connection.close();
    };
  }, [
    canViewTicketSections,
    currentUser,
    getApiToken,
    isAuthenticated,
    loadNotifications,
    needsConsent,
    permissionsLoaded,
    scheduleRealtimeTicketRefresh,
  ]);

  useEffect(() => {
    if (!isUserMenuOpen && !isAppMenuOpen && !isNotificationPanelOpen) return;

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node;

      if (isUserMenuOpen && !userMenuRef.current?.contains(target)) {
        setIsUserMenuOpen(false);
      }

      if (isAppMenuOpen && !appMenuRef.current?.contains(target)) {
        setIsAppMenuOpen(false);
      }

      if (
        isNotificationPanelOpen &&
        !notificationPanelRef.current?.contains(target)
      ) {
        setIsNotificationPanelOpen(false);
      }
    };

    document.addEventListener("mousedown", handlePointerDown);
    return () => document.removeEventListener("mousedown", handlePointerDown);
  }, [isAppMenuOpen, isNotificationPanelOpen, isUserMenuOpen]);

  useEffect(() => {
    if (!isAuthenticated || isLoading || isAccountExpired || isAccountInactive) {
      sessionPromptStateRef.current = null;
      sessionLastActivityAtRef.current = Date.now();
      setSessionPromptState(null);
      setSessionRemainingSeconds(sessionTimeoutSeconds);
      return;
    }

    const now = Date.now();
    const hasReauthPending =
      typeof window !== "undefined" &&
      window.sessionStorage.getItem(sessionReauthPendingStorageKey) === "1";
    const storedLastActivity = hasReauthPending
      ? null
      : readStoredTimestamp(sessionLastActivityStorageKey);
    const restoredLastActivity = storedLastActivity ?? now;
    const elapsedSeconds = Math.floor((now - restoredLastActivity) / 1000);
    const remainingSeconds = Math.max(0, sessionTimeoutSeconds - elapsedSeconds);
    const initialPromptState: SessionPromptState =
      remainingSeconds === 0
        ? "expired"
        : sessionWarningSeconds > 0 && remainingSeconds <= sessionWarningSeconds
          ? "warning"
          : null;

    if (typeof window !== "undefined") {
      if (hasReauthPending) {
        window.sessionStorage.removeItem(sessionReauthPendingStorageKey);
      }

      if (storedLastActivity === null || hasReauthPending) {
        window.sessionStorage.setItem(
          sessionLastActivityStorageKey,
          String(restoredLastActivity),
        );
      }
    }

    sessionLastActivityAtRef.current = restoredLastActivity;
    sessionPromptStateRef.current = initialPromptState;
    setSessionPromptState(initialPromptState);
    setSessionRemainingSeconds(remainingSeconds);

    const handleActivity = () => {
      if (sessionPromptStateRef.current !== null) {
        return;
      }

      markSessionActivity();
      syncPresenceIfDue();
    };

    const intervalId = window.setInterval(() => {
      const elapsedSeconds = Math.floor(
        (Date.now() - sessionLastActivityAtRef.current) / 1000,
      );
      const remainingSeconds = Math.max(0, sessionTimeoutSeconds - elapsedSeconds);

      setSessionRemainingSeconds(remainingSeconds);

      if (remainingSeconds === 0) {
        if (sessionPromptStateRef.current !== "expired") {
          sessionPromptStateRef.current = "expired";
          setSessionPromptState("expired");
          setIsUserMenuOpen(false);
          setIsAppMenuOpen(false);
        }

        return;
      }

      if (
        sessionWarningSeconds > 0 &&
        remainingSeconds <= sessionWarningSeconds &&
        sessionPromptStateRef.current === null
      ) {
        sessionPromptStateRef.current = "warning";
        setSessionPromptState("warning");
        setIsUserMenuOpen(false);
        setIsAppMenuOpen(false);
      }
    }, 1000);

    for (const eventName of SESSION_ACTIVITY_EVENTS) {
      window.addEventListener(eventName, handleActivity, { passive: true });
    }

    return () => {
      window.clearInterval(intervalId);
      for (const eventName of SESSION_ACTIVITY_EVENTS) {
        window.removeEventListener(eventName, handleActivity);
      }
    };
  }, [
    isAccountExpired,
    isAccountInactive,
    isAuthenticated,
    isLoading,
    markSessionActivity,
    sessionLastActivityStorageKey,
    sessionReauthPendingStorageKey,
    syncPresenceIfDue,
    sessionTimeoutSeconds,
    sessionWarningSeconds,
  ]);

  useEffect(() => {
    if (!isAuthenticated || isLoading || isAccountExpired || isAccountInactive) {
      return;
    }

    syncPresenceIfDue(true);
  }, [
    isAccountExpired,
    isAccountInactive,
    isAuthenticated,
    isLoading,
    syncPresenceIfDue,
  ]);

  useEffect(() => {
    if (!isSidebarResizing) {
      return;
    }

    const handlePointerMove = (event: MouseEvent) => {
      const nextWidth =
        sidebarResizeStartWidthRef.current +
        (event.clientX - sidebarResizeStartXRef.current);

      setSidebarWidth(clampSidebarWidth(nextWidth));
    };

    const handlePointerUp = () => {
      setIsSidebarResizing(false);
    };

    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";

    window.addEventListener("mousemove", handlePointerMove);
    window.addEventListener("mouseup", handlePointerUp);

    return () => {
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
      window.removeEventListener("mousemove", handlePointerMove);
      window.removeEventListener("mouseup", handlePointerUp);
    };
  }, [isSidebarResizing]);

  useEffect(() => {
    if (isLoading || !isAuthenticated) return;

    let cancelled = false;

    const bootstrap = async () => {
      setLoading(true);
      setError(null);
      setPermissionsLoaded(false);
      let parsedPermissions: string[] = [];

      try {
        const token = await getAccessTokenSilently({
          authorizationParams: API_AUTHORIZATION_PARAMS,
        });
        parsedPermissions = parsePermissionsFromToken(token);
        if (cancelled) return;
        setPermissions(parsedPermissions);
        const { fetchedCurrentUser, fetchedTickets } =
          await loadBootstrapData(token);

        if (cancelled) return;

        setCurrentUser(fetchedCurrentUser);
        setAllTickets(fetchedTickets);
        setNeedsConsent(false);
        setApiUnavailable(false);
        void loadTicketBoards(token);
        void loadSessionConfiguration(token);
        void loadNotifications(token, { silent: true });
      } catch (error) {
        console.error("Bootstrap failed", error);

        if (cancelled) return;

        setPermissions(parsedPermissions);

        if (isConsentRequiredError(error)) {
          setNeedsConsent(true);
          setApiUnavailable(false);
          setError("CORTEX API consent is required before the app can load.");
        } else if (isForbiddenError(error)) {
          setNeedsConsent(false);
          setApiUnavailable(false);
          setError("You do not have permission to view tickets.");
        } else if (isApiUnavailableError(error)) {
          setNeedsConsent(false);
          setApiUnavailable(true);
          setError(null);
        } else {
          setNeedsConsent(false);
          setApiUnavailable(false);
          setError("Failed to initialize the application.");
        }
      } finally {
        if (!cancelled) {
          setPermissionsLoaded(true);
          setLoading(false);
        }
      }
    };

    void bootstrap();

    return () => {
      cancelled = true;
    };
  }, [
    getAccessTokenSilently,
    isAuthenticated,
    isLoading,
    loadTicketBoards,
    loadNotifications,
    loadSessionConfiguration,
  ]);

  useEffect(() => {
    if (!isAuthenticated || ticketBoards.length > 0 || ticketBoardLoading) {
      return;
    }

    void loadTicketBoards();
  }, [isAuthenticated, loadTicketBoards, ticketBoardLoading, ticketBoards.length]);

  useEffect(() => {
    if (!isAuthenticated || ticketStatuses.length > 0 || ticketStatusLoading) {
      return;
    }

    void loadTicketStatuses();
  }, [
    isAuthenticated,
    loadTicketStatuses,
    ticketStatusLoading,
    ticketStatuses.length,
  ]);

  useEffect(() => {
    if (
      activeView !== "sla" ||
      !canManageConfiguration ||
      (slaConfigurations.length > 0 &&
        ticketStatuses.length > 0 &&
        ticketRoutingLoadedOnce &&
        sessionLoadedOnce &&
        notificationChannelsLoadedOnce &&
        archiveLoadedOnce &&
        customReportsLoadedOnce &&
        databaseViews.length > 0 &&
        databaseStoredProcedures.length > 0 &&
        storedProcedures.length > 0)
    ) {
      return;
    }

    if (slaConfigurations.length === 0) {
      void loadSlaConfigurations();
    }

    if (ticketStatuses.length === 0) {
      void loadTicketStatuses();
    }

    if (!ticketRoutingLoadedOnce) {
      void loadTicketRoutingRules();
    }

    if (!sessionLoadedOnce) {
      void loadSessionConfiguration();
    }

    if (!notificationChannelsLoadedOnce) {
      void loadNotificationChannelConfiguration();
    }

    if (!archiveLoadedOnce) {
      void loadArchiveConfigurations();
    }

    if (!customReportsLoadedOnce) {
      void loadCustomReports();
    }

    if (databaseViews.length === 0) {
      void loadDatabaseViews();
    }

    if (storedProcedures.length === 0) {
      void loadStoredProcedures();
    }

    if (databaseStoredProcedures.length === 0) {
      void loadDatabaseStoredProcedures();
    }
  }, [
    activeView,
    archiveLoadedOnce,
    canManageConfiguration,
    customReportsLoadedOnce,
    databaseStoredProcedures.length,
    databaseViews.length,
    loadNotificationChannelConfiguration,
    loadCustomReports,
    loadArchiveConfigurations,
    loadDatabaseStoredProcedures,
    loadDatabaseViews,
    loadSessionConfiguration,
    loadStoredProcedures,
    loadTicketRoutingRules,
    loadTicketStatuses,
    loadSlaConfigurations,
    notificationChannelsLoadedOnce,
    sessionLoadedOnce,
    slaConfigurations.length,
    storedProcedures.length,
    ticketRoutingLoadedOnce,
    ticketStatuses.length,
  ]);

  useEffect(() => {
    if (activeView !== "jobs" || !canManageJobs) {
      return;
    }

    if (!jobsLoaded) {
      void loadJobs();
    }

    if (storedProcedures.length === 0) {
      void loadStoredProcedures();
    }
  }, [
    activeView,
    canManageJobs,
    jobsLoaded,
    loadJobs,
    loadStoredProcedures,
    storedProcedures.length,
  ]);

  useEffect(() => {
    if (!canManageJobs || jobsLoaded || jobsLoading) {
      return;
    }

    void loadJobs();
  }, [canManageJobs, jobsLoaded, jobsLoading, loadJobs]);

  useEffect(() => {
    if (
      !isAuthenticated ||
      !permissionsLoaded ||
      needsConsent ||
      notificationsLoaded ||
      notificationsLoading
    ) {
      return;
    }

    void loadNotifications();
  }, [
    isAuthenticated,
    loadNotifications,
    needsConsent,
    notificationsLoaded,
    notificationsLoading,
    permissionsLoaded,
  ]);

  useEffect(() => {
    if (activeView !== "users" || !canViewUsers || users.length > 0) {
      return;
    }

    void loadUsers();
  }, [activeView, canViewUsers, loadUsers, users.length]);

  useEffect(() => {
    if (
      activeView !== "archived" ||
      !canViewArchived ||
      archivedTickets.length > 0
    ) {
      return;
    }

    void loadArchivedTickets();
  }, [activeView, archivedTickets.length, canViewArchived, loadArchivedTickets]);

  useEffect(() => {
    if (activeView !== "archived" && highlightedArchivedTicketId) {
      setHighlightedArchivedTicketId(null);
    }
  }, [activeView, highlightedArchivedTicketId]);

  useEffect(() => {
    if (canViewOnlineUsersReport || activeReportSection !== "online-users") {
      return;
    }

    setActiveReportSection("sla");
  }, [activeReportSection, canViewOnlineUsersReport]);

  useEffect(() => {
    if (
      activeView !== "reports" ||
      activeReportSection !== "online-users" ||
      !canViewOnlineUsersReport
    ) {
      return;
    }

    if (onlineUsers.length === 0) {
      void loadOnlineUsers();
    }
  }, [
    activeReportSection,
    activeView,
    canViewOnlineUsersReport,
    loadOnlineUsers,
    onlineUsers.length,
  ]);

  useEffect(() => {
    if (
      activeView !== "reports" ||
      !canManageCustomReports ||
      customReportsLoadedOnce
    ) {
      return;
    }

    void loadCustomReports();
  }, [
    activeView,
    canManageCustomReports,
    customReportsLoadedOnce,
    loadCustomReports,
  ]);

  useEffect(() => {
    if (activeReportSection !== "custom") {
      return;
    }

    if (!canManageCustomReports) {
      setActiveReportSection("sla");
      setSelectedCustomReportId(null);
      return;
    }

    const enabledReports = customReports.filter((report) => report.isEnabled);
    if (enabledReports.length === 0) {
      setSelectedCustomReportId(null);
      setCustomReportResult(null);
      return;
    }

    if (
      selectedCustomReportId === null ||
      !enabledReports.some((report) => report.id === selectedCustomReportId)
    ) {
      setSelectedCustomReportId(enabledReports[0].id);
    }
  }, [
    activeReportSection,
    canManageCustomReports,
    customReports,
    selectedCustomReportId,
  ]);

  useEffect(() => {
    if (
      activeView !== "reports" ||
      activeReportSection !== "custom" ||
      selectedCustomReportId === null
    ) {
      return;
    }

    void runCustomReport(selectedCustomReportId);
  }, [activeReportSection, activeView, runCustomReport, selectedCustomReportId]);

  useEffect(() => {
    if (
      activeView !== "reports" ||
      activeReportSection !== "online-users" ||
      !canViewOnlineUsersReport
    ) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void loadOnlineUsers();
    }, 60_000);

    return () => window.clearInterval(intervalId);
  }, [
    activeReportSection,
    activeView,
    canViewOnlineUsersReport,
    loadOnlineUsers,
  ]);

  const grantConsent = async () => {
    setLoading(true);
    setError(null);
    let parsedPermissions: string[] = [];

    try {
      const token = await getAccessTokenWithPopup({
        authorizationParams: API_AUTHORIZATION_PARAMS,
      });
      if (!token) {
        throw new Error("No access token was returned.");
      }
      parsedPermissions = parsePermissionsFromToken(token);
      const { fetchedCurrentUser, fetchedTickets } =
        await loadBootstrapData(token);

      setPermissions(parsedPermissions);
      setCurrentUser(fetchedCurrentUser);
      setAllTickets(fetchedTickets);
      setNeedsConsent(false);
      setPermissionsLoaded(true);
      setApiUnavailable(false);
      void loadTicketBoards(token);
      void loadNotifications(token, { silent: true });
    } catch (error) {
      console.error("Consent failed", error);
      setPermissions(parsedPermissions);

      if (isApiUnavailableError(error)) {
        setApiUnavailable(true);
      } else {
        setApiUnavailable(false);
        setError("Failed to grant CORTEX API access.");
        toast.error("Failed to grant CORTEX API access");
      }
    } finally {
      setLoading(false);
    }
  };

  const availableTicketBoards = useMemo(
    () =>
      ticketBoards.length > 0 ? ticketBoards : [...DEFAULT_TICKET_BOARDS],
    [ticketBoards],
  );

  const boardTabs = useMemo(() => {
    return availableTicketBoards.filter(
      (board) =>
        board.isEnabled || allTickets.some((ticket) => ticket.boardId === board.id),
    );
  }, [allTickets, availableTicketBoards]);

  const tickets = useMemo(() => {
    const filterInput = normalize(debouncedFilterValue);
    const searchInput = normalize(debouncedSearchQuery);
    let filteredTickets =
      selectedBoardId === "all"
        ? allTickets
        : allTickets.filter((ticket) => ticket.boardId === selectedBoardId);

    if (filter !== "all" && filterInput) {
      if (filter === "status") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.status ?? "").includes(filterInput),
        );
      } else if (filter === "sla") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.slaStatus ?? "").includes(filterInput),
        );
      } else {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.priority ?? "").includes(filterInput),
        );
      }
    }

    if (!searchInput) {
      return filteredTickets;
    }

    return filteredTickets.filter((ticket) =>
      ticketMatchesSearch(ticket, searchInput),
    );
  }, [
    allTickets,
    selectedBoardId,
    filter,
    debouncedFilterValue,
    debouncedSearchQuery,
  ]);

  const totalTickets = tickets.length;
  const totalPages =
    pageSize === "all" ? 1 : Math.max(1, Math.ceil(totalTickets / pageSize));
  const pagedTickets = useMemo(() => {
    if (pageSize === "all") {
      return tickets;
    }

    const startIndex = (currentPage - 1) * pageSize;
    return tickets.slice(startIndex, startIndex + pageSize);
  }, [currentPage, pageSize, tickets]);
  const showingStart =
    totalTickets === 0
      ? 0
      : (currentPage - 1) * (pageSize === "all" ? totalTickets : pageSize) + 1;
  const showingEnd =
    pageSize === "all"
      ? totalTickets
      : Math.min(totalTickets, currentPage * pageSize);

  useEffect(() => {
    setCurrentPage(1);
  }, [selectedBoardId, filter, debouncedFilterValue, debouncedSearchQuery, pageSize]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  useEffect(() => {
    if (selectedBoardId === "all") {
      return;
    }

    if (!boardTabs.some((board) => board.id === selectedBoardId)) {
      setSelectedBoardId("all");
    }
  }, [boardTabs, selectedBoardId]);

  const handleSaveTicket = async (
    updatedTicket: TicketMutationInput,
    attachments: File[],
  ) => {
    if (!selectedTicket) return;

    try {
      const token = await getApiToken();
      let savedTicket: Ticket;
      let successMessage = "";

      if (!selectedTicket.id) {
        savedTicket = await ticketService.create(
          updatedTicket as CreateTicketInput,
          token,
        );
        setAllTickets((prev) => [savedTicket, ...prev]);
        successMessage = "Ticket created";
      } else {
        savedTicket = await ticketService.update(
          selectedTicket.id,
          updatedTicket,
          token,
        );
        setAllTickets((prev) =>
          prev.map((ticket) => (ticket.id === savedTicket.id ? savedTicket : ticket)),
        );
        successMessage = "Ticket updated";
      }

      if (attachments.length > 0) {
        try {
          await attachmentService.upload(savedTicket.id, attachments, token);
          successMessage +=
            attachments.length === 1
              ? " with 1 attachment"
              : ` with ${attachments.length} attachments`;
        } catch (attachmentError) {
          console.error("Failed to upload attachments", attachmentError);
          toast.success(successMessage);
          toast.error("Ticket saved, but attachments failed to upload");
          setIsModalOpen(false);
          setSelectedTicket(null);
          return;
        }
      }

      toast.success(successMessage);
      setIsModalOpen(false);
      setSelectedTicket(null);
    } catch (error) {
      console.error("Failed to save ticket", error);
      toast.error("Failed to save ticket");
      throw error;
    }
  };

  const handleSlaConfigurationChange = (
    priority: string,
    field: "targetHours" | "warningHours",
    value: number,
  ) => {
    setSlaConfigurations((prev) =>
      prev.map((configuration) =>
        configuration.priority === priority
          ? {
              ...configuration,
              [field]: Number.isNaN(value) ? 0 : value,
            }
          : configuration,
      ),
    );
  };

  const saveSlaConfigurations = async () => {
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

      if (error instanceof ApiError) {
        setSlaError(error.message);
      } else {
        setSlaError("Failed to save SLA settings.");
      }

      toast.error("Failed to save SLA settings");
    } finally {
      setSlaSaving(false);
    }
  };

  const handleSessionConfigurationChange = <
    K extends keyof SessionConfiguration,
  >(
    field: K,
    value: SessionConfiguration[K],
  ) => {
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
  };

  const handleNotificationChannelConfigurationChange = <
    K extends keyof NotificationChannelConfiguration,
  >(
    field: K,
    value: NotificationChannelConfiguration[K],
  ) => {
    setNotificationChannelConfiguration((currentConfiguration) =>
      currentConfiguration
        ? {
            ...currentConfiguration,
            [field]: value,
          }
        : currentConfiguration,
    );
  };

  const saveSessionConfiguration = async () => {
    if (!sessionConfiguration) {
      return;
    }

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

      if (error instanceof ApiError) {
        setSessionError(error.message);
      } else {
        setSessionError("Failed to save session configuration.");
      }

      toast.error("Failed to save session policy");
    } finally {
      setSessionSaving(false);
    }
  };

  const saveNotificationChannelConfiguration = async () => {
    if (!notificationChannelConfiguration) {
      return;
    }

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

      if (error instanceof ApiError) {
        setNotificationChannelError(error.message);
      } else {
        setNotificationChannelError(
          "Failed to save notification channel settings.",
        );
      }

      toast.error("Failed to save notification channel settings");
    } finally {
      setNotificationChannelSaving(false);
    }
  };

  const createTicketBoard = async (
    definition: UpsertTicketBoardDefinitionInput,
  ) => {
    try {
      setTicketBoardSaving(true);
      setTicketBoardError(null);

      const token = await getApiToken();
      const createdDefinition = await ticketBoardService.create(definition, token);

      setTicketBoards((currentDefinitions) =>
        sortTicketBoards([...currentDefinitions, createdDefinition]),
      );
      toast.success("Ticket board created");
    } catch (error) {
      console.error("Failed to create ticket board", error);

      if (error instanceof ApiError) {
        setTicketBoardError(error.message);
      } else {
        setTicketBoardError("Failed to create ticket board.");
      }

      toast.error("Failed to create ticket board");
      throw error;
    } finally {
      setTicketBoardSaving(false);
    }
  };

  const updateTicketBoard = async (
    id: number,
    definition: UpsertTicketBoardDefinitionInput,
  ) => {
    try {
      setTicketBoardSaving(true);
      setTicketBoardError(null);

      const existingDefinition = ticketBoards.find(
        (currentDefinition) => currentDefinition.id === id,
      );
      const token = await getApiToken();
      const updatedDefinition = await ticketBoardService.update(id, definition, token);

      setTicketBoards((currentDefinitions) =>
        sortTicketBoards(
          currentDefinitions.map((currentDefinition) =>
            currentDefinition.id === updatedDefinition.id
              ? updatedDefinition
              : currentDefinition,
          ),
        ),
      );
      setAllTickets((currentTickets) =>
        currentTickets.map((ticket) =>
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
      setArchivedTickets((currentTickets) =>
        currentTickets.map((ticket) =>
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
        selectedBoardId !== "all" &&
        selectedBoardId === id &&
        !updatedDefinition.isEnabled &&
        existingDefinition?.isEnabled
      ) {
        setSelectedBoardId("all");
      }

      toast.success("Ticket board updated");
    } catch (error) {
      console.error("Failed to update ticket board", error);

      if (error instanceof ApiError) {
        setTicketBoardError(error.message);
      } else {
        setTicketBoardError("Failed to update ticket board.");
      }

      toast.error("Failed to update ticket board");
      throw error;
    } finally {
      setTicketBoardSaving(false);
    }
  };

  const deleteTicketBoard = async (id: number) => {
    try {
      setDeletingTicketBoardId(id);
      setTicketBoardError(null);

      const token = await getApiToken();
      await ticketBoardService.delete(id, token);

      setTicketBoards((currentDefinitions) =>
        currentDefinitions.filter((currentDefinition) => currentDefinition.id !== id),
      );
      if (selectedBoardId === id) {
        setSelectedBoardId("all");
      }

      toast.success("Ticket board deleted");
    } catch (error) {
      console.error("Failed to delete ticket board", error);

      if (error instanceof ApiError) {
        setTicketBoardError(error.message);
      } else {
        setTicketBoardError("Failed to delete ticket board.");
      }

      toast.error("Failed to delete ticket board");
      throw error;
    } finally {
      setDeletingTicketBoardId(null);
    }
  };

  const createTicketStatusDefinition = async (
    definition: UpsertTicketStatusDefinitionInput,
  ) => {
    try {
      setTicketStatusSaving(true);
      setTicketStatusError(null);

      const token = await getApiToken();
      const createdDefinition = await ticketStatusService.create(definition, token);

      setTicketStatuses((currentDefinitions) =>
        sortTicketStatuses([...currentDefinitions, createdDefinition]),
      );
      toast.success("Ticket status created");
    } catch (error) {
      console.error("Failed to create ticket status", error);

      if (error instanceof ApiError) {
        setTicketStatusError(error.message);
      } else {
        setTicketStatusError("Failed to create ticket status.");
      }

      toast.error("Failed to create ticket status");
      throw error;
    } finally {
      setTicketStatusSaving(false);
    }
  };

  const updateTicketStatusDefinition = async (
    id: number,
    definition: UpsertTicketStatusDefinitionInput,
  ) => {
    try {
      setTicketStatusSaving(true);
      setTicketStatusError(null);

      const existingDefinition = ticketStatuses.find(
        (currentDefinition) => currentDefinition.id === id,
      );
      const token = await getApiToken();
      const updatedDefinition = await ticketStatusService.update(id, definition, token);

      setTicketStatuses((currentDefinitions) =>
        sortTicketStatuses(
          currentDefinitions.map((currentDefinition) =>
            currentDefinition.id === updatedDefinition.id
              ? updatedDefinition
              : currentDefinition,
          ),
        ),
      );
      setArchiveConfigurations((currentConfigurations) =>
        currentConfigurations.map((configuration) => ({
          ...configuration,
          eligibleStatuses: configuration.eligibleStatuses.map((statusName) =>
            statusName === existingDefinition?.name ? updatedDefinition.name : statusName,
          ),
        })),
      );
      setArchiveConfiguration((currentConfiguration) =>
        currentConfiguration
          ? {
              ...currentConfiguration,
              eligibleStatuses: currentConfiguration.eligibleStatuses.map(
                (statusName) =>
                  statusName === existingDefinition?.name ? updatedDefinition.name : statusName,
              ),
            }
          : currentConfiguration,
      );
      setAllTickets((currentTickets) =>
        currentTickets.map((ticket) =>
          ticket.status === existingDefinition?.name
            ? { ...ticket, status: updatedDefinition.name }
            : ticket,
        ),
      );
      setArchivedTickets((currentTickets) =>
        currentTickets.map((ticket) =>
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

      if (error instanceof ApiError) {
        setTicketStatusError(error.message);
      } else {
        setTicketStatusError("Failed to update ticket status.");
      }

      toast.error("Failed to update ticket status");
      throw error;
    } finally {
      setTicketStatusSaving(false);
    }
  };

  const deleteTicketStatusDefinition = async (id: number) => {
    try {
      setDeletingTicketStatusId(id);
      setTicketStatusError(null);

      const token = await getApiToken();
      await ticketStatusService.delete(id, token);

      setTicketStatuses((currentDefinitions) =>
        currentDefinitions.filter((currentDefinition) => currentDefinition.id !== id),
      );
      toast.success("Ticket status deleted");
    } catch (error) {
      console.error("Failed to delete ticket status", error);

      if (error instanceof ApiError) {
        setTicketStatusError(error.message);
      } else {
        setTicketStatusError("Failed to delete ticket status.");
      }

      toast.error("Failed to delete ticket status");
      throw error;
    } finally {
      setDeletingTicketStatusId(null);
    }
  };

  const handleTicketRoutingRuleChange = <
    K extends keyof TicketRoutingRule,
  >(
    field: K,
    value: TicketRoutingRule[K],
  ) => {
    setSelectedTicketRoutingRule((currentRule) =>
      currentRule
        ? {
            ...currentRule,
            [field]: value,
          }
        : currentRule,
    );
  };

  const createTicketRoutingRule = () => {
    setTicketRoutingError(null);
    setSelectedTicketRoutingRule(createDraftTicketRoutingRule());
  };

  const selectTicketRoutingRule = (id: number) => {
    const selectedRule = ticketRoutingRules.find((rule) => rule.id === id);
    if (!selectedRule) {
      return;
    }

    setTicketRoutingError(null);
    setSelectedTicketRoutingRule(selectedRule);
  };

  const saveTicketRoutingRule = async () => {
    if (!selectedTicketRoutingRule) {
      return;
    }

    try {
      setTicketRoutingSaving(true);
      setTicketRoutingError(null);

      const payload: UpsertTicketRoutingRuleInput = {
        department: selectedTicketRoutingRule.department.trim() || undefined,
        titleContains: selectedTicketRoutingRule.titleContains.trim() || undefined,
        synitiOwner: selectedTicketRoutingRule.synitiOwner.trim() || undefined,
        businessOwner:
          selectedTicketRoutingRule.businessOwner.trim() || undefined,
        isEnabled: selectedTicketRoutingRule.isEnabled,
      };

      const token = await getApiToken();
      const isNewRule = selectedTicketRoutingRule.id === 0;
      const savedRule = isNewRule
        ? await ticketRoutingService.create(payload, token)
        : await ticketRoutingService.update(selectedTicketRoutingRule.id, payload, token);

      setTicketRoutingRules((currentRules) =>
        sortTicketRoutingRules(
          isNewRule
            ? [...currentRules, savedRule]
            : currentRules.map((rule) =>
                rule.id === savedRule.id ? savedRule : rule,
              ),
        ),
      );
      setSelectedTicketRoutingRule(savedRule);
      setTicketRoutingLoadedOnce(true);
      toast.success(isNewRule ? "Ticket routing rule created" : "Ticket routing rule saved");
    } catch (error) {
      console.error("Failed to save ticket routing rule", error);

      if (error instanceof ApiError) {
        setTicketRoutingError(error.message);
      } else {
        setTicketRoutingError("Failed to save ticket routing rule.");
      }

      toast.error("Failed to save ticket routing rule");
      throw error;
    } finally {
      setTicketRoutingSaving(false);
    }
  };

  const deleteTicketRoutingRule = async () => {
    if (!selectedTicketRoutingRule || selectedTicketRoutingRule.id === 0) {
      return;
    }

    const confirmed = window.confirm(
      `Delete the routing rule for ${selectedTicketRoutingRule.department}?`,
    );

    if (!confirmed) {
      return;
    }

    try {
      setDeletingTicketRoutingRuleId(selectedTicketRoutingRule.id);
      setTicketRoutingError(null);

      const token = await getApiToken();
      await ticketRoutingService.delete(selectedTicketRoutingRule.id, token);

      setTicketRoutingRules((currentRules) =>
        currentRules.filter((rule) => rule.id !== selectedTicketRoutingRule.id),
      );
      setSelectedTicketRoutingRule((currentRule) => {
        if (currentRule?.id !== selectedTicketRoutingRule.id) {
          return currentRule;
        }

        const remainingRules = ticketRoutingRules.filter(
          (rule) => rule.id !== selectedTicketRoutingRule.id,
        );
        return remainingRules[0] ?? null;
      });
      toast.success("Ticket routing rule deleted");
    } catch (error) {
      console.error("Failed to delete ticket routing rule", error);

      if (error instanceof ApiError) {
        setTicketRoutingError(error.message);
      } else {
        setTicketRoutingError("Failed to delete ticket routing rule.");
      }

      toast.error("Failed to delete ticket routing rule");
      throw error;
    } finally {
      setDeletingTicketRoutingRuleId(null);
    }
  };

  const createCustomReport = async (
    definition: UpsertCustomReportDefinitionInput,
  ) => {
    try {
      setCustomReportsSaving(true);
      setCustomReportsError(null);

      const token = await getApiToken();
      const createdDefinition = await customReportService.create(definition, token);

      setCustomReports((currentReports) =>
        [...currentReports, createdDefinition].sort((left, right) =>
          left.name.localeCompare(right.name),
        ),
      );

      if (createdDefinition.isEnabled) {
        setActiveReportSection("custom");
        setSelectedCustomReportId(createdDefinition.id);
      }

      await loadDatabaseViews(token);

      toast.success("Custom report created");
    } catch (error) {
      console.error("Failed to create custom report", error);

      if (error instanceof ApiError) {
        setCustomReportsError(error.message);
      } else {
        setCustomReportsError("Failed to create custom report.");
      }

      toast.error("Failed to create custom report");
      throw error;
    } finally {
      setCustomReportsSaving(false);
    }
  };

  const updateCustomReport = async (
    id: number,
    definition: UpsertCustomReportDefinitionInput,
  ) => {
    try {
      setCustomReportsSaving(true);
      setCustomReportsError(null);

      const token = await getApiToken();
      const updatedDefinition = await customReportService.update(id, definition, token);

      setCustomReports((currentReports) =>
        currentReports
          .map((report) => (report.id === updatedDefinition.id ? updatedDefinition : report))
          .sort((left, right) => left.name.localeCompare(right.name)),
      );

      if (!updatedDefinition.isEnabled && selectedCustomReportId === updatedDefinition.id) {
        setActiveReportSection("sla");
        setSelectedCustomReportId(null);
        setCustomReportResult(null);
      } else if (selectedCustomReportId === updatedDefinition.id) {
        await runCustomReport(updatedDefinition.id, token);
      }

      await loadDatabaseViews(token);

      toast.success("Custom report updated");
    } catch (error) {
      console.error("Failed to update custom report", error);

      if (error instanceof ApiError) {
        setCustomReportsError(error.message);
      } else {
        setCustomReportsError("Failed to update custom report.");
      }

      toast.error("Failed to update custom report");
      throw error;
    } finally {
      setCustomReportsSaving(false);
    }
  };

  const deleteCustomReport = async (id: number) => {
    try {
      setDeletingCustomReportId(id);
      setCustomReportsError(null);

      const token = await getApiToken();
      await customReportService.delete(id, token);

      const remainingReports = customReports.filter((report) => report.id !== id);
      const remainingEnabledReports = remainingReports.filter((report) => report.isEnabled);

      setCustomReports(remainingReports);

      if (selectedCustomReportId === id) {
        setCustomReportResult(null);

        if (activeReportSection === "custom" && remainingEnabledReports.length > 0) {
          setSelectedCustomReportId(remainingEnabledReports[0].id);
        } else {
          setSelectedCustomReportId(null);

          if (activeReportSection === "custom") {
            setActiveReportSection("sla");
          }
        }
      }

      await loadDatabaseViews(token);

      toast.success("Custom report deleted");
    } catch (error) {
      console.error("Failed to delete custom report", error);

      if (error instanceof ApiError) {
        setCustomReportsError(error.message);
      } else {
        setCustomReportsError("Failed to delete custom report.");
      }

      toast.error("Failed to delete custom report");
      throw error;
    } finally {
      setDeletingCustomReportId(null);
    }
  };

  const handleArchiveConfigurationChange = <
    K extends keyof ArchiveConfiguration,
  >(
    field: K,
    value: ArchiveConfiguration[K],
  ) => {
    setArchiveConfiguration((currentConfiguration) =>
      currentConfiguration
        ? {
            ...currentConfiguration,
            [field]:
              field === "archiveAfterDays" && typeof value === "number"
                ? Number.isNaN(value)
                  ? 0
                  : value
                : value,
          }
        : currentConfiguration,
    );
  };

  const createArchivePolicy = () => {
    setArchiveError(null);
    setArchiveConfiguration(createDraftArchiveConfiguration(ticketStatuses));
  };

  const selectArchivePolicy = (id: number) => {
    const selectedConfiguration = archiveConfigurations.find(
      (configuration) => configuration.id === id,
    );

    if (!selectedConfiguration) {
      return;
    }

    setArchiveError(null);
    setArchiveConfiguration(selectedConfiguration);
  };

  const saveArchiveConfiguration = async () => {
    if (!archiveConfiguration) {
      return;
    }

    try {
      setArchiveSaving(true);
      setArchiveError(null);

      const token = await getApiToken();
      const isNewConfiguration = archiveConfiguration.id === 0;
      const savedConfiguration = isNewConfiguration
        ? await archiveConfigurationService.create(archiveConfiguration, token)
        : await archiveConfigurationService.update(
            archiveConfiguration.id,
            archiveConfiguration,
            token,
          );

      setArchiveConfigurations((currentConfigurations) =>
        sortArchiveConfigurations(
          isNewConfiguration
            ? [...currentConfigurations, savedConfiguration]
            : currentConfigurations.map((configuration) =>
                configuration.id === savedConfiguration.id
                  ? savedConfiguration
                  : configuration,
              ),
        ),
      );
      setArchiveConfiguration(savedConfiguration);
      toast.success(
        isNewConfiguration
          ? "Archive policy created"
          : "Archive policy saved",
      );
    } catch (error) {
      console.error("Failed to save archive configuration", error);

      if (error instanceof ApiError) {
        setArchiveError(error.message);
      } else {
        setArchiveError("Failed to save archive configuration.");
      }

      toast.error("Failed to save archive configuration");
    } finally {
      setArchiveSaving(false);
    }
  };

  const deleteArchiveConfiguration = async () => {
    if (!archiveConfiguration || archiveConfiguration.id === 0) {
      return;
    }

    const confirmed = window.confirm(
      `Delete archive policy #${archiveConfiguration.id}?`,
    );

    if (!confirmed) {
      return;
    }

    try {
      setDeletingArchiveConfigurationId(archiveConfiguration.id);
      setArchiveError(null);

      const token = await getApiToken();
      await archiveConfigurationService.delete(archiveConfiguration.id, token);

      const remainingConfigurations = archiveConfigurations.filter(
        (configuration) => configuration.id !== archiveConfiguration.id,
      );

      setArchiveConfigurations(remainingConfigurations);
      setArchiveConfiguration(remainingConfigurations[0] ?? null);
      toast.success("Archive policy deleted");
    } catch (error) {
      console.error("Failed to delete archive configuration", error);

      if (error instanceof ApiError) {
        setArchiveError(error.message);
      } else {
        setArchiveError("Failed to delete archive configuration.");
      }

      toast.error("Failed to delete archive configuration");
    } finally {
      setDeletingArchiveConfigurationId(null);
    }
  };

  const runArchiveNow = async () => {
    try {
      setArchiveRunning(true);
      setArchiveError(null);

      const token = await getApiToken();
      const result = await archiveConfigurationService.runNow(token);

      await Promise.all([
        refreshTicketsSilently(token),
        loadArchivedTickets(token),
      ]);

      toast.success(
        result.archivedTicketCount === 1
          ? "Archived 1 ticket"
          : `Archived ${result.archivedTicketCount} tickets`,
      );
    } catch (error) {
      console.error("Failed to archive eligible tickets", error);

      if (error instanceof ApiError) {
        setArchiveError(error.message);
      } else {
        setArchiveError("Failed to archive eligible tickets.");
      }

      toast.error("Failed to archive eligible tickets");
    } finally {
      setArchiveRunning(false);
    }
  };

  const createStoredProcedureDefinition = async (
    definition: UpsertStoredProcedureDefinitionInput,
  ) => {
    try {
      setStoredProcedureSaving(true);
      setStoredProcedureError(null);

      const token = await getApiToken();
      const createdDefinition = await storedProcedureService.create(definition, token);

      setStoredProcedures((currentDefinitions) =>
        [...currentDefinitions, createdDefinition].sort((left, right) =>
          left.name.localeCompare(right.name),
        ),
      );
      await loadDatabaseStoredProcedures(token);
      toast.success("Stored procedure created");
    } catch (error) {
      console.error("Failed to create stored procedure", error);

      if (error instanceof ApiError) {
        setStoredProcedureError(error.message);
      } else {
        setStoredProcedureError("Failed to create stored procedure.");
      }

      toast.error("Failed to create stored procedure");
      throw error;
    } finally {
      setStoredProcedureSaving(false);
    }
  };

  const updateStoredProcedureDefinition = async (
    id: number,
    definition: UpsertStoredProcedureDefinitionInput,
  ) => {
    try {
      setStoredProcedureSaving(true);
      setStoredProcedureError(null);

      const token = await getApiToken();
      const updatedDefinition = await storedProcedureService.update(
        id,
        definition,
        token,
      );

      setStoredProcedures((currentDefinitions) =>
        currentDefinitions
          .map((currentDefinition) =>
            currentDefinition.id === updatedDefinition.id
              ? updatedDefinition
              : currentDefinition,
          )
          .sort((left, right) => left.name.localeCompare(right.name)),
      );
      setJobs((currentJobs) =>
        currentJobs.map((currentJob) =>
          currentJob.storedProcedureDefinitionId === updatedDefinition.id
            ? {
                ...currentJob,
                storedProcedureName: updatedDefinition.name,
              }
            : currentJob,
        ),
      );
      await loadDatabaseStoredProcedures(token);
      toast.success("Stored procedure updated");
    } catch (error) {
      console.error("Failed to update stored procedure", error);

      if (error instanceof ApiError) {
        setStoredProcedureError(error.message);
      } else {
        setStoredProcedureError("Failed to update stored procedure.");
      }

      toast.error("Failed to update stored procedure");
      throw error;
    } finally {
      setStoredProcedureSaving(false);
    }
  };

  const deleteStoredProcedureDefinition = async (id: number) => {
    try {
      setDeletingStoredProcedureId(id);
      setStoredProcedureError(null);

      const token = await getApiToken();
      await storedProcedureService.delete(id, token);

      setStoredProcedures((currentDefinitions) =>
        currentDefinitions.filter((currentDefinition) => currentDefinition.id !== id),
      );

      setJobs((currentJobs) =>
        currentJobs.map((currentJob) =>
          currentJob.storedProcedureDefinitionId === id
            ? {
                ...currentJob,
                storedProcedureDefinitionId: undefined,
                storedProcedureName: undefined,
                isEnabled: false,
                nextRunDateUtc: undefined,
                lastRunStatus: "Failed",
                lastRunMessage:
                  "Stored procedure was deleted. Select a replacement procedure before re-enabling this job.",
              }
            : currentJob,
        ),
      );

      void loadJobs(token);
      await loadDatabaseStoredProcedures(token);

      toast.success("Stored procedure deleted");
    } catch (error) {
      console.error("Failed to delete stored procedure", error);

      if (error instanceof ApiError) {
        setStoredProcedureError(error.message);
      } else {
        setStoredProcedureError("Failed to delete stored procedure.");
      }

      toast.error("Failed to delete stored procedure");
      throw error;
    } finally {
      setDeletingStoredProcedureId(null);
    }
  };

  const createScheduledJob = async (job: UpsertScheduledJobInput) => {
    try {
      setJobsSaving(true);
      setJobsError(null);

      const token = await getApiToken();
      const createdJob = await scheduledJobService.create(job, token);

      setJobs((currentJobs) =>
        [...currentJobs, createdJob].sort((left, right) =>
          left.name.localeCompare(right.name),
        ),
      );
      toast.success("Job created");
    } catch (error) {
      console.error("Failed to create job", error);

      if (error instanceof ApiError) {
        setJobsError(error.message);
      } else {
        setJobsError("Failed to create job.");
      }

      toast.error("Failed to create job");
      throw error;
    } finally {
      setJobsSaving(false);
    }
  };

  const updateScheduledJob = async (
    id: number,
    job: UpsertScheduledJobInput,
  ) => {
    try {
      setJobsSaving(true);
      setJobsError(null);

      const token = await getApiToken();
      const updatedJob = await scheduledJobService.update(id, job, token);

      setJobs((currentJobs) =>
        currentJobs
          .map((currentJob) =>
            currentJob.id === updatedJob.id ? updatedJob : currentJob,
          )
          .sort((left, right) => left.name.localeCompare(right.name)),
      );
      toast.success("Job updated");
    } catch (error) {
      console.error("Failed to update job", error);

      if (error instanceof ApiError) {
        setJobsError(error.message);
      } else {
        setJobsError("Failed to update job.");
      }

      toast.error("Failed to update job");
      throw error;
    } finally {
      setJobsSaving(false);
    }
  };

  const runScheduledJobNow = async (id: number) => {
    try {
      setRunningJobId(id);
      setJobsError(null);

      const token = await getApiToken();
      const updatedJob = await scheduledJobService.runNow(id, token);

      setJobs((currentJobs) =>
        currentJobs
          .map((currentJob) =>
            currentJob.id === updatedJob.id ? updatedJob : currentJob,
          )
          .sort((left, right) => left.name.localeCompare(right.name)),
      );

      await Promise.all([
        refreshTicketsSilently(token),
        loadArchivedTickets(token),
      ]);

      toast.success("Job ran successfully");
    } catch (error) {
      console.error("Failed to run job", error);

      if (error instanceof ApiError) {
        setJobsError(error.message);
      } else {
        setJobsError("Failed to run job.");
      }

      toast.error("Failed to run job");
      throw error;
    } finally {
      setRunningJobId(null);
    }
  };

  const requestDeleteTicket = (ticket: Ticket) => {
    setTicketToDelete(ticket);
  };

  const confirmDeleteTicket = async () => {
    if (!ticketToDelete) return;

    try {
      setDeleting(true);
      const token = await getApiToken();
      await ticketService.delete(ticketToDelete.id, token);
      setAllTickets((prev) =>
        prev.filter((ticket) => ticket.id !== ticketToDelete.id),
      );
      toast.success("Ticket deleted");
    } catch (error) {
      console.error("Failed to delete ticket", error);
      toast.error("Failed to delete ticket");
    } finally {
      setDeleting(false);
      setTicketToDelete(null);
    }
  };

  const handleArchiveTicket = async (
    ticket: Ticket,
    changeReason?: string,
  ) => {
    if (!ticket.id) {
      return;
    }

    try {
      const token = await getApiToken();
      const archivedTicket = await ticketService.archiveWithReason(
        ticket.id,
        changeReason,
        token,
      );

      setAllTickets((currentTickets) =>
        currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
      );
      setArchivedTickets((currentTickets) => [
        archivedTicket,
        ...currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
      ]);

      setIsModalOpen(false);
      setSelectedTicket(null);
      toast.success("Ticket archived");
    } catch (error) {
      console.error("Failed to archive ticket", error);
      toast.error(getErrorMessage(error, "Failed to archive ticket"));
      throw error;
    }
  };

  const handleReactivateArchivedTicket = async (ticket: ArchivedTicket) => {
    if (!ticket.id) {
      return;
    }

    try {
      setReactivatingArchivedTicketId(ticket.id);
      const token = await getApiToken();
      const restoredTicket = await ticketService.reactivateArchived(ticket.id, token);

      setArchivedTickets((currentTickets) =>
        currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
      );
      setAllTickets((currentTickets) => [
        restoredTicket,
        ...currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
      ]);

      toast.success(
        restoredTicket.status !== ticket.status
          ? `Ticket reactivated and reopened as ${restoredTicket.status}`
          : "Ticket reactivated",
      );
    } catch (error) {
      console.error("Failed to reactivate archived ticket", error);
      toast.error(getErrorMessage(error, "Failed to reactivate archived ticket"));
    } finally {
      setReactivatingArchivedTicketId(null);
    }
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setTimeout(() => {
      setSelectedTicket(null);
    }, 0);
  };

  const openTicket = (ticket: Ticket) => {
    setSelectedTicket(ticket);
    setIsModalOpen(true);
  };

  const handleViewChange = (view: AppView) => {
    setActiveView(view);
    setIsAppMenuOpen(false);
    setIsNotificationPanelOpen(false);
  };

  const openFailedJobsQueue = () => {
    setActiveView("jobs");
    setIsAppMenuOpen(false);
    setIsUserMenuOpen(false);
    setIsNotificationPanelOpen(false);

    window.setTimeout(() => {
      document.getElementById("failed-jobs-queue")?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }, 50);
  };

  const openTicketById = async (ticketId: string, providedToken?: string) => {
    const existingTicket = allTickets.find((ticket) => ticket.id === ticketId);
    if (existingTicket) {
      openTicket(existingTicket);
      return;
    }

    const token = providedToken ?? (await getApiToken());
    const fetchedTicket = await ticketService.getById(ticketId, token);

    setAllTickets((currentTickets) => {
      if (currentTickets.some((ticket) => ticket.id === fetchedTicket.id)) {
        return currentTickets;
      }

      return [fetchedTicket, ...currentTickets];
    });

    openTicket(fetchedTicket);
  };

  const markNotificationAsRead = async (
    notification: UserNotification,
    providedToken?: string,
  ) => {
    if (notification.isRead) {
      return;
    }

    const token = providedToken ?? (await getApiToken());
    const updatedNotification = await notificationService.markRead(
      notification.id,
      token,
    );

    setNotifications((currentNotifications) =>
      currentNotifications.map((currentNotification) =>
        currentNotification.id === updatedNotification.id
          ? updatedNotification
          : currentNotification,
      ),
    );
    setNotificationUnreadCount((currentCount) =>
      Math.max(0, currentCount - 1),
    );
  };

  const markAllNotificationsRead = async () => {
    try {
      setMarkingAllNotificationsRead(true);
      setNotificationsError(null);

      const token = await getApiToken();
      await notificationService.markAllRead(token);

      setNotifications((currentNotifications) =>
        currentNotifications.map((notification) =>
          notification.isRead
            ? notification
            : {
                ...notification,
                isRead: true,
                readDateUtc: new Date().toISOString(),
              },
        ),
      );
      setNotificationUnreadCount(0);
    } catch (error) {
      console.error("Failed to mark notifications as read", error);
      setNotificationsError(
        getErrorMessage(error, "Failed to mark notifications as read."),
      );
      toast.error("Failed to mark notifications as read");
    } finally {
      setMarkingAllNotificationsRead(false);
    }
  };

  const openNotification = async (notification: UserNotification) => {
    try {
      setMarkingNotificationId(notification.id);
      setNotificationsError(null);
      setIsNotificationPanelOpen(false);
      setIsUserMenuOpen(false);

      const token = await getApiToken();
      await markNotificationAsRead(notification, token);

      if (!notification.ticketId) {
        return;
      }

      if (notification.ticketIsArchived) {
        setHighlightedArchivedTicketId(notification.ticketId);
        setActiveView("archived");
        await loadArchivedTickets(token);

        window.setTimeout(() => {
          document
            .getElementById(`archived-ticket-${notification.ticketId}`)
            ?.scrollIntoView({
              behavior: "smooth",
              block: "center",
            });
        }, 75);
        return;
      }

      setHighlightedArchivedTicketId(null);
      setActiveView("tickets");
      await openTicketById(notification.ticketId, token);
    } catch (error) {
      console.error("Failed to open notification", error);
      setNotificationsError(getErrorMessage(error, "Failed to open notification."));
      toast.error("Failed to open notification");
    } finally {
      setMarkingNotificationId(null);
    }
  };

  const beginSidebarResize = (event: ReactMouseEvent<HTMLButtonElement>) => {
    sidebarResizeStartXRef.current = event.clientX;
    sidebarResizeStartWidthRef.current = sidebarWidth;
    setIsSidebarResizing(true);
  };

  const handleFilterChange = (value: string) => {
    setSelectedSavedFilterId("");
    setFilter(isFilterOption(value) ? value : "all");
    setFilterValue("");
  };

  const handleFilterValueChange = (value: string) => {
    setSelectedSavedFilterId("");
    setFilterValue(value);
  };

  const handleSearchChange = (value: string) => {
    setSelectedSavedFilterId("");
    setSearchQuery(value);
  };

  const handlePageSizeChange = (value: string) => {
    setSelectedSavedFilterId("");
    if (value === "all") {
      setPageSize("all");
      return;
    }

    const nextPageSize = Number(value);
    if (
      nextPageSize === 10 ||
      nextPageSize === 25 ||
      nextPageSize === 50
    ) {
      setPageSize(nextPageSize);
    }
  };

  const openSaveFilterModal = () => {
    const existingFilter = savedFilters.find(
      (savedFilter) => savedFilter.id === selectedSavedFilterId,
    );

    setSavedFilterName(existingFilter?.name ?? "My Ticket View");
    setIsSaveFilterModalOpen(true);
  };

  const closeSaveFilterModal = () => {
    setIsSaveFilterModalOpen(false);
    setSavedFilterName("");
  };

  const saveCurrentFilter = () => {
    const trimmedName = savedFilterName.trim();
    if (!trimmedName) {
      return;
    }

    const existingFilter = savedFilters.find(
      (savedFilter) => normalize(savedFilter.name) === normalize(trimmedName),
    );
    const savedFilterId = existingFilter?.id ?? createSavedFilterId();
    const nextSavedFilter: SavedTicketFilter = {
      id: savedFilterId,
      name: trimmedName,
      filter,
      filterValue,
      searchQuery,
      pageSize,
    };

    setSavedFilters((currentFilters) => [
      nextSavedFilter,
      ...currentFilters.filter((savedFilter) => savedFilter.id !== savedFilterId),
    ]);
    setSelectedSavedFilterId(savedFilterId);
    closeSaveFilterModal();
    toast.success(existingFilter ? "Saved filter updated" : "Saved filter saved");
  };

  const applySavedFilter = (savedFilterId: string) => {
    setSelectedSavedFilterId(savedFilterId);

    if (!savedFilterId) {
      return;
    }

    const savedFilter = savedFilters.find(
      (filterEntry) => filterEntry.id === savedFilterId,
    );
    if (!savedFilter) {
      return;
    }

    setFilter(savedFilter.filter);
    setFilterValue(savedFilter.filterValue);
    setSearchQuery(savedFilter.searchQuery);
    setPageSize(savedFilter.pageSize);
  };

  const clearTicketFilters = () => {
    setSelectedSavedFilterId("");
    setFilter("all");
    setFilterValue("");
    setSearchQuery("");
    setPageSize(10);
  };

  const deleteSavedFilter = () => {
    if (!selectedSavedFilterId) {
      return;
    }

    const filterToDelete = savedFilters.find(
      (savedFilter) => savedFilter.id === selectedSavedFilterId,
    );

    setSavedFilters((currentFilters) =>
      currentFilters.filter(
        (savedFilter) => savedFilter.id !== selectedSavedFilterId,
      ),
    );
    setSelectedSavedFilterId("");
    toast.success(
      filterToDelete
        ? `Removed "${filterToDelete.name}"`
        : "Saved filter removed",
    );
  };

  const toggleTheme = () => {
    setTheme((currentTheme) => (currentTheme === "dark" ? "light" : "dark"));
  };

  const toggleThemeFromMenu = () => {
    toggleTheme();
    setIsUserMenuOpen(false);
  };

  const toggleAppMenu = () => {
    setIsAppMenuOpen((current) => {
      const next = !current;
      if (next) {
        setIsUserMenuOpen(false);
        setIsNotificationPanelOpen(false);
      }
      return next;
    });
  };

  const toggleUserMenu = () => {
    setIsUserMenuOpen((current) => {
      const next = !current;
      if (next) {
        setIsAppMenuOpen(false);
        setIsNotificationPanelOpen(false);
      }
      return next;
    });
  };

  const toggleNotificationPanel = () => {
    setIsNotificationPanelOpen((current) => {
      const next = !current;
      if (next) {
        setIsAppMenuOpen(false);
        setIsUserMenuOpen(false);
        if (!notificationsLoaded && !notificationsLoading) {
          void loadNotifications();
        }
      }
      return next;
    });
  };

  const openProfileModal = async () => {
    setIsUserMenuOpen(false);
    setIsNotificationPanelOpen(false);
    setProfileLoading(true);

    try {
      let profile = currentUser;

      if (!profile) {
        const token = await getApiToken();
        profile = await userService.getCurrentUser(token);
        setCurrentUser(profile);
      }

      setProfileDraft({
        nickName: profile.nickName ?? "",
        phoneNumber: profile.phoneNumber ?? "",
        department: profile.department ?? "",
        assignmentNotificationChannel:
          profile.assignmentNotificationChannel ?? "",
        slaRiskNotificationChannel: profile.slaRiskNotificationChannel ?? "",
      });
      setIsProfileModalOpen(true);
    } catch (error) {
      console.error("Failed to load profile", error);
      toast.error(getErrorMessage(error, "Failed to load profile"));
    } finally {
      setProfileLoading(false);
    }
  };

  const closeProfileModal = () => {
    setIsProfileModalOpen(false);
  };

  const openCreateUserModal = () => {
    setCreateUserDraft({
      displayName: "",
      nickName: "",
      email: "",
      password: "",
      phoneNumber: "",
      department: "",
      role: "User",
      isActive: true,
      expiryDate: "",
    });
    setIsCreateUserModalOpen(true);
  };

  const closeCreateUserModal = () => {
    setIsCreateUserModalOpen(false);
  };

  const openAdminUserModal = (selectedUser: UserRecord) => {
    setEditingAdminUser(selectedUser);
    setAdminUserDraft({
      nickName: selectedUser.nickName ?? "",
      phoneNumber: selectedUser.phoneNumber ?? "",
      department: selectedUser.department ?? "",
      assignmentNotificationChannel:
        selectedUser.assignmentNotificationChannel ?? "",
      slaRiskNotificationChannel: selectedUser.slaRiskNotificationChannel ?? "",
      role: selectedUser.role,
      isActive: selectedUser.isActive,
      expiryDate: selectedUser.expiryDate ?? "",
    });
  };

  const closeAdminUserModal = () => {
    setEditingAdminUser(null);
    setAdminUserDraft({});
  };

  const handleProfileDraftChange = (
    field: keyof UpdateUserProfileInput,
    value: string,
  ) => {
    setProfileDraft((currentDraft) => ({
      ...currentDraft,
      [field]: value,
    }));
  };

  const handleAdminUserDraftChange = (
    field: keyof AdminUpdateUserInput,
    value: string | boolean,
  ) => {
    setAdminUserDraft((currentDraft) => ({
      ...currentDraft,
      [field]: value,
    }));
  };

  const handleCreateUserDraftChange = (
    field: keyof CreateUserInput,
    value: string | boolean,
  ) => {
    setCreateUserDraft((currentDraft) => ({
      ...currentDraft,
      [field]: value,
    }));
  };

  const saveProfile = async () => {
    if (!currentUser) return;

    try {
      setProfileSaving(true);
      const token = await getApiToken();
      const updatedUser = await userService.updateProfile(profileDraft, token);

      setCurrentUser(updatedUser);
      setUsers((currentUsers) =>
        currentUsers.map((userRecord) =>
          userRecord.id === updatedUser.id
            ? { ...userRecord, ...updatedUser }
            : userRecord,
        ),
      );
      setIsProfileModalOpen(false);
      toast.success("Profile updated");
    } catch (error) {
      console.error("Failed to update profile", error);
      toast.error(getErrorMessage(error, "Failed to update profile"));
    } finally {
      setProfileSaving(false);
    }
  };

  const saveAdminUser = async () => {
    if (!editingAdminUser) return;

    try {
      setAdminUserSaving(true);
      const token = await getApiToken();
      const payload: AdminUpdateUserInput = {
        ...adminUserDraft,
        expiryDate: adminUserDraft.expiryDate?.trim()
          ? adminUserDraft.expiryDate
          : null,
      };
      const updatedUser = await userService.updateUser(
        editingAdminUser.id,
        payload,
        token,
      );

      setUsers((currentUsers) =>
        currentUsers.map((userRecord) =>
          userRecord.id === updatedUser.id ? updatedUser : userRecord,
        ),
      );
      setCurrentUser((existingUser) =>
        existingUser && existingUser.id === updatedUser.id
          ? { ...existingUser, ...updatedUser }
          : existingUser,
      );
      closeAdminUserModal();
      toast.success("User updated");
    } catch (error) {
      console.error("Failed to update user", error);
      toast.error(getErrorMessage(error, "Failed to update user"));
    } finally {
      setAdminUserSaving(false);
    }
  };

  const saveCreatedUser = async () => {
    try {
      setCreateUserSaving(true);
      const token = await getApiToken();
      const payload: CreateUserInput = {
        ...createUserDraft,
        displayName: createUserDraft.displayName.trim(),
        email: createUserDraft.email.trim(),
        password: createUserDraft.password,
        nickName: createUserDraft.nickName?.trim() || undefined,
        phoneNumber: createUserDraft.phoneNumber?.trim() || undefined,
        department: createUserDraft.department?.trim() || undefined,
        expiryDate: createUserDraft.expiryDate?.trim()
          ? createUserDraft.expiryDate
          : null,
      };

      const createdUser = await userService.createUser(payload, token);
      setUsers((currentUsers) =>
        [...currentUsers, createdUser].sort((left, right) =>
          (left.displayName ?? left.email).localeCompare(
            right.displayName ?? right.email,
          ),
        ),
      );
      closeCreateUserModal();
      toast.success("User created");
    } catch (error) {
      console.error("Failed to create user", error);
      toast.error(getErrorMessage(error, "Failed to create user"));
    } finally {
      setCreateUserSaving(false);
    }
  };

  const deleteUserRecord = async (selectedUser: UserRecord) => {
    const userLabel = selectedUser.displayName || selectedUser.email;
    const confirmed = window.confirm(
      `Delete "${userLabel}"? This removes the local user record, deletes the linked Auth0 account when configured, reassigns historical references to the legacy fallback user, and disables any jobs that ran as this user.`,
    );

    if (!confirmed) {
      return;
    }

    try {
      setDeletingUserId(selectedUser.id);
      const token = await getApiToken();
      await userService.deleteUser(selectedUser.id, token);

      setUsers((currentUsers) =>
        currentUsers.filter((userRecord) => userRecord.id !== selectedUser.id),
      );
      setOnlineUsers((currentUsers) =>
        currentUsers.filter((userRecord) => userRecord.id !== selectedUser.id),
      );

      if (editingAdminUser?.id === selectedUser.id) {
        closeAdminUserModal();
      }

      if (canManageJobs) {
        void loadJobs(token);
      }

      toast.success("User deleted");
    } catch (error) {
      console.error("Failed to delete user", error);
      toast.error(getErrorMessage(error, "Failed to delete user"));
    } finally {
      setDeletingUserId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-cortex-surface to-cortex-surface-alt dark:from-cortex-ink-dark dark:to-cortex-ink flex items-center justify-center text-gray-900 dark:text-slate-100">
        <div className="text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-cortex-blue mx-auto" />
          <p className="mt-4 text-gray-600 dark:text-slate-400">Loading...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-cortex-surface to-cortex-surface-alt dark:from-cortex-ink-dark dark:to-cortex-ink flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
        <div className="text-center bg-white/85 dark:bg-cortex-ink/85 border border-white/60 dark:border-slate-800 rounded-2xl shadow-xl px-8 py-10 backdrop-blur">
          <h1 className="text-4xl font-bold mb-4">🧠 CORTEX</h1>
          <p className="text-gray-600 dark:text-slate-400 mb-6">
            Central Operations & Routing Technology EXpert
          </p>
          <button
            onClick={() =>
              loginWithRedirect({
                authorizationParams: {
                  ...API_AUTHORIZATION_PARAMS,
                  scope: "openid profile email",
                },
              })
            }
            className="px-6 py-3 bg-cortex-blue text-white rounded-md hover:bg-cortex-blue-dark transition-colors"
          >
            Log In
          </button>
        </div>
      </div>
    );
  }

  if (isAccountExpired) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-cortex-surface to-cortex-surface-alt dark:from-cortex-ink-dark dark:to-cortex-ink flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
        <div className="max-w-xl rounded-2xl border border-red-200 bg-white/90 px-8 py-10 text-center shadow-xl backdrop-blur dark:border-red-900/40 dark:bg-slate-900/90">
          <h1 className="mb-4 text-3xl font-bold">Your account has been expired</h1>
          <p className="text-gray-600 dark:text-slate-400">
            Please contact an administrator if you believe this is a mistake.
          </p>
          {currentUser?.expiryDate && (
            <p className="mt-3 text-sm text-gray-500 dark:text-slate-500">
              Expired on {new Date(currentUser.expiryDate).toLocaleDateString()}
            </p>
          )}
          <div className="mt-6 flex justify-center">
            <button
              onClick={performLogout}
              className="rounded-md bg-red-600 px-4 py-2 text-white transition-colors hover:bg-red-700"
            >
              Log Out
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (isAccountInactive) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-cortex-surface to-cortex-surface-alt dark:from-cortex-ink-dark dark:to-cortex-ink flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
        <div className="max-w-xl rounded-2xl border border-amber-200 bg-white/90 px-8 py-10 text-center shadow-xl backdrop-blur dark:border-amber-900/40 dark:bg-slate-900/90">
          <h1 className="mb-4 text-3xl font-bold">Your account is inactive</h1>
          <p className="text-gray-600 dark:text-slate-400">
            Please contact an administrator if you believe this is a mistake.
          </p>
          <div className="mt-6 flex justify-center">
            <button
              onClick={performLogout}
              className="rounded-md bg-amber-600 px-4 py-2 text-white transition-colors hover:bg-amber-700"
            >
              Log Out
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col bg-gradient-to-br from-cortex-surface to-cortex-surface-alt text-gray-900 transition-colors dark:from-cortex-ink-dark dark:to-cortex-ink dark:text-slate-100">
      <header className="relative z-40 border-b border-gray-200 bg-white/92 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-cortex-ink-dark/92">
        <div className="mx-auto flex w-full max-w-[2200px] flex-col gap-6 px-6 py-6 2xl:px-8 xl:flex-row xl:items-center xl:justify-between">
          <div className="space-y-4">
            <div className="flex flex-col gap-2 md:flex-row md:items-baseline md:gap-4">
              <h1 className="text-3xl font-bold">🧠 CORTEX</h1>
              <h2 className="text-lg text-gray-600 dark:text-slate-400">
                Central Operations & Routing Technology Expert
              </h2>
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <div ref={appMenuRef} className="relative lg:hidden">
                <button
                  onClick={toggleAppMenu}
                  className="inline-flex items-center gap-3 rounded-md border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 20 20"
                    className="h-4 w-4"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                    strokeLinecap="round"
                  >
                    <path d="M3 5h14" />
                    <path d="M3 10h14" />
                    <path d="M3 15h14" />
                  </svg>
                  <span>Menu</span>
                  <span className="text-xs text-gray-400 dark:text-slate-500">
                    ▾
                  </span>
                </button>

                {isAppMenuOpen && (
                  <div className="absolute left-0 top-full z-20 mt-2 w-80 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
                    <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                        Navigate
                      </p>
                      <p className="mt-1 text-sm text-gray-600 dark:text-slate-300">
                        Current view:{" "}
                        <span className="font-medium text-gray-900 dark:text-slate-100">
                          {activeViewLabel}
                        </span>
                      </p>
                    </div>

                    {navigationItems.map((item) => {
                      const isActive = item.view === activeView;

                      return (
                        <button
                          key={item.view}
                          onClick={() => handleViewChange(item.view)}
                          className={`w-full px-4 py-3 text-left transition-colors ${
                            isActive
                              ? "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100"
                              : "text-gray-700 hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800"
                          }`}
                        >
                          <div className="flex items-center justify-between gap-4">
                            <span className="font-medium">{item.label}</span>
                            {isActive && (
                              <span className="text-xs font-semibold uppercase tracking-wide text-cortex-blue dark:text-cortex-cyan">
                                Active
                              </span>
                            )}
                          </div>
                          <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                            {item.description}
                          </p>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>

              <div className="inline-flex items-center rounded-full bg-gray-100 px-3 py-2 text-sm text-gray-600 dark:bg-slate-800 dark:text-slate-300">
                {activeViewLabel}
              </div>

              {activeNavigationItem && (
                <p className="hidden text-sm text-gray-500 dark:text-slate-400 lg:block">
                  {activeNavigationItem.description}
                </p>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-end gap-4">
            {activeView === "tickets" && (
              <>
                <button
                  onClick={() => void loadAllTickets()}
                  className="inline-flex items-center rounded-md bg-cortex-blue px-3 py-2 text-sm font-medium text-white shadow-sm transition-colors hover:bg-cortex-blue-dark"
                >
                  Refresh
                </button>

                {canCreateTickets && (
                  <button
                    onClick={() =>
                      openTicket(
                        createDraftTicket(
                          ticketStatuses,
                          ticketBoards.length > 0 ? ticketBoards : [...DEFAULT_TICKET_BOARDS],
                          currentUser?.displayName ?? user?.name ?? "",
                          currentUser?.department ?? "",
                        ),
                      )
                    }
                    className="inline-flex items-center rounded-md bg-cortex-cyan px-3.5 py-2 text-sm font-semibold text-cortex-ink shadow-sm ring-1 ring-cortex-cyan/70 transition-colors hover:bg-cortex-blue hover:text-white dark:bg-cortex-cyan dark:text-cortex-ink dark:ring-cortex-cyan/60 dark:hover:bg-cortex-blue dark:hover:text-white"
                  >
                    + New Ticket
                  </button>
                )}
              </>
            )}

            {permissionsLoaded && needsConsent && (
              <div className="flex items-center gap-2">
                <span className="text-sm text-yellow-700 dark:text-amber-300">
                  CORTEX API consent is required before permission-based UI can
                  load.
                </span>
                <button
                  onClick={() => void grantConsent()}
                  className="rounded-md bg-cortex-blue px-3 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Grant Access
                </button>
              </div>
            )}

            <div
              ref={userMenuRef}
              className="relative flex items-center gap-3 border-l border-gray-300 pl-4 dark:border-slate-700"
            >
              <div ref={notificationPanelRef} className="relative">
                <button
                  onClick={toggleNotificationPanel}
                  className="relative inline-flex h-10 w-10 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-cortex-blue-soft hover:text-cortex-blue dark:text-slate-300 dark:hover:bg-cortex-blue/20 dark:hover:text-cortex-cyan"
                  title={
                    notificationUnreadCount === 0
                      ? "Notifications"
                      : `${notificationUnreadCount} unread notification${notificationUnreadCount === 1 ? "" : "s"}`
                  }
                  aria-label={
                    notificationUnreadCount === 0
                      ? "Open notifications"
                      : `Open notifications with ${notificationUnreadCount} unread`
                  }
                >
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 20 20"
                    className="h-5 w-5"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M10 3.5a5 5 0 0 0-5 5v2.1c0 .7-.2 1.3-.6 1.9l-.8 1.2c-.4.6 0 1.3.7 1.3h11.4c.7 0 1.1-.7.7-1.3l-.8-1.2c-.4-.6-.6-1.2-.6-1.9V8.5a5 5 0 0 0-5-5Z" />
                    <path d="M8.5 16.5a1.5 1.5 0 0 0 3 0" />
                  </svg>
                  {notificationUnreadCount > 0 && (
                    <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-cortex-cyan px-1 text-[11px] font-semibold leading-none text-cortex-ink">
                      {notificationUnreadCount > 9 ? "9+" : notificationUnreadCount}
                    </span>
                  )}
                </button>

                {isNotificationPanelOpen && (
                  <NotificationPanel
                    notifications={notifications}
                    unreadCount={notificationUnreadCount}
                    loading={notificationsLoading}
                    error={notificationsError}
                    markingAllRead={markingAllNotificationsRead}
                    markingNotificationId={markingNotificationId}
                    onRefresh={() => void loadNotifications()}
                    onMarkAllRead={markAllNotificationsRead}
                    onOpenNotification={openNotification}
                  />
                )}
              </div>

              {canManageJobs && failedJobsCount > 0 && (
                <button
                  onClick={openFailedJobsQueue}
                  className="relative inline-flex h-10 w-10 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-red-50 hover:text-red-700 dark:text-slate-300 dark:hover:bg-red-950/30 dark:hover:text-red-200"
                  title={
                    failedJobsCount === 1
                      ? "1 failed job needs attention"
                      : `${failedJobsCount} failed jobs need attention`
                  }
                  aria-label={
                    failedJobsCount === 1
                      ? "Open failed jobs queue"
                      : `Open failed jobs queue with ${failedJobsCount} failed jobs`
                  }
                >
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 20 20"
                    className="h-5 w-5"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M10 3a4 4 0 0 0-4 4v2.6c0 .7-.2 1.4-.6 2l-.9 1.3A1 1 0 0 0 5.3 14h9.4a1 1 0 0 0 .8-1.6l-.9-1.3a3.6 3.6 0 0 1-.6-2V7a4 4 0 0 0-4-4Z" />
                    <path d="M8.5 16a1.5 1.5 0 0 0 3 0" />
                  </svg>
                  <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-600 px-1 text-[11px] font-semibold leading-none text-white">
                    {failedJobsCount > 9 ? "9+" : failedJobsCount}
                  </span>
                </button>
              )}

              <button
                onClick={toggleUserMenu}
                className="inline-flex items-center gap-2 rounded-md px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                <span>
                  {currentUser?.nickName ??
                    currentUser?.displayName ??
                    user?.name}
                </span>
                <span className="text-xs text-gray-500 dark:text-slate-400">
                  ▾
                </span>
              </button>

              {isUserMenuOpen && (
                <div className="absolute right-0 top-full z-20 mt-2 w-72 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
                  <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
                    <p className="font-medium text-gray-900 dark:text-slate-100">
                      {currentUser?.displayName ?? user?.name}
                    </p>
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      {currentUser?.email ?? user?.email}
                    </p>
                    {currentUser?.nickName && (
                      <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                        Nick name: {currentUser.nickName}
                      </p>
                    )}
                  </div>

                  <button
                    onClick={() => void openProfileModal()}
                    disabled={profileLoading}
                    className="w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    {profileLoading ? "Loading Profile..." : "Edit Profile"}
                  </button>
                  <button
                    onClick={toggleThemeFromMenu}
                    className="w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    {isDarkMode ? "Light Mode" : "Dark Mode"}
                  </button>
                  <button
                    onClick={performLogout}
                    className="w-full px-4 py-3 text-left text-sm text-red-600 transition-colors hover:bg-red-50 dark:text-red-300 dark:hover:bg-red-950/30"
                  >
                    Log Out
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-[2200px] flex-1 px-6 py-8 lg:gap-8 2xl:px-8">
        <aside
          className="relative hidden shrink-0 lg:block"
          style={{ width: `${sidebarWidth}px` }}
        >
          <div className="relative sticky top-8 flex h-[calc(100vh-8rem)] flex-col rounded-2xl border border-gray-200 bg-white/90 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-slate-950/85">
            <div className="border-b border-gray-100 px-5 py-5 dark:border-slate-800">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-gray-500 dark:text-slate-400">
                Navigation
              </p>
              <h3 className="mt-2 text-lg font-semibold text-gray-900 dark:text-slate-100">
                Workspace
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Move between ticket operations, reporting, and admin controls.
              </p>
            </div>

            <nav className="flex-1 overflow-y-auto px-3 py-4">
              <div className="space-y-2">
                {navigationItems.map((item) => {
                  const isActive = item.view === activeView;

                  return (
                    <button
                      key={item.view}
                      onClick={() => handleViewChange(item.view)}
                      className={`w-full rounded-xl border px-4 py-3 text-left transition-colors ${
                        isActive
                          ? "border-cortex-blue bg-cortex-blue-soft text-cortex-ink shadow-sm dark:border-cortex-blue dark:bg-cortex-blue/20 dark:text-slate-100"
                          : "border-transparent text-gray-700 hover:border-gray-200 hover:bg-gray-50 dark:text-slate-200 dark:hover:border-slate-700 dark:hover:bg-slate-900"
                      }`}
                    >
                      <div className="flex items-center justify-between gap-3">
                        <span className="font-medium">{item.label}</span>
                        {isActive && (
                          <span className="text-xs font-semibold uppercase tracking-wide text-cortex-blue dark:text-cortex-cyan">
                            Active
                          </span>
                        )}
                      </div>
                      <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                        {item.description}
                      </p>
                    </button>
                  );
                })}
              </div>
            </nav>

            <div className="border-t border-gray-100 px-5 py-4 dark:border-slate-800">
              <p className="text-xs text-gray-500 dark:text-slate-400">
                Drag the right edge to resize the menu.
              </p>
            </div>

            <button
              type="button"
              aria-label="Resize sidebar"
              onMouseDown={beginSidebarResize}
              className="absolute inset-y-0 -right-2 hidden w-4 cursor-col-resize items-center justify-center lg:flex"
            >
              <span
                className={`block h-20 w-1 rounded-full transition-colors ${
                  isSidebarResizing
                    ? "bg-cortex-blue"
                    : "bg-gray-300 dark:bg-slate-700"
                }`}
              />
            </button>
          </div>
        </aside>

        <main className="min-w-0 flex-1">
        {activeView === "dashboard" && canViewDashboard ? (
          <DashboardPage
            tickets={allTickets}
            loading={loading || apiUnavailable}
            error={apiUnavailable ? null : error}
            onRefresh={() => void loadAllTickets()}
            onOpenTicket={openTicket}
          />
        ) : activeView === "tickets" ? (
          <>
            <div className="mb-6 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
              <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
                    Ticket Filters
                  </h3>
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    Search, narrow, and save ticket views without crowding the header.
                  </p>
                </div>

                <div className="flex flex-wrap gap-2">
                  <button
                    onClick={openSaveFilterModal}
                    className="inline-flex items-center rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    Save Filter
                  </button>

                  {selectedSavedFilterId && (
                    <button
                      onClick={deleteSavedFilter}
                      className="inline-flex items-center rounded-md border border-red-200 px-3 py-2 text-sm font-medium text-red-600 shadow-sm transition-colors hover:bg-red-50 dark:border-red-900/50 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      Delete Saved
                    </button>
                  )}

                  <button
                    onClick={clearTicketFilters}
                    className="inline-flex items-center rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    Clear
                  </button>
                </div>
              </div>

              <div className="mt-4 flex flex-wrap gap-2">
                <button
                  onClick={() => setSelectedBoardId("all")}
                  className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                    selectedBoardId === "all"
                      ? "bg-cortex-blue text-white"
                      : "border border-gray-200 bg-gray-50 text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                  }`}
                >
                  All Boards
                  <span className="ml-2 text-xs opacity-80">{allTickets.length}</span>
                </button>
                {boardTabs.map((board) => {
                  const boardCount = allTickets.filter(
                    (ticket) => ticket.boardId === board.id,
                  ).length;
                  const isActive = selectedBoardId === board.id;

                  return (
                    <button
                      key={board.id}
                      onClick={() => setSelectedBoardId(board.id)}
                      className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                        isActive
                          ? "bg-cortex-blue text-white"
                          : "border border-gray-200 bg-gray-50 text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                      }`}
                    >
                      {board.name}
                      <span className="ml-2 text-xs opacity-80">{boardCount}</span>
                    </button>
                  );
                })}
              </div>

              <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <select
                  value={filter}
                  onChange={(event) => handleFilterChange(event.target.value)}
                  className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                >
                  <option value="all">All Tickets</option>
                  <option value="status">By Status</option>
                  <option value="priority">By Priority</option>
                  <option value="sla">By SLA</option>
                </select>

                <label className="flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-600 dark:border-slate-700 dark:text-slate-400">
                  <span>Show</span>
                  <select
                    value={pageSize}
                    onChange={(event) => handlePageSizeChange(event.target.value)}
                    style={{ colorScheme: theme === "dark" ? "dark" : "light" }}
                    className="min-w-0 flex-1 rounded-md border border-gray-300 bg-white px-2 py-1 text-gray-900 shadow-none focus:border-cortex-blue focus:ring-0 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                  >
                    {PAGE_SIZE_OPTIONS.map((option) => (
                      <option key={option} value={option}>
                        {option === "all" ? "All" : option}
                      </option>
                    ))}
                  </select>
                </label>

                {filter === "sla" ? (
                  <select
                    value={filterValue}
                    onChange={(event) => handleFilterValueChange(event.target.value)}
                    className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                  >
                    <option value="">Select SLA state</option>
                    {SLA_FILTER_OPTIONS.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                ) : filter !== "all" ? (
                  <input
                    type="text"
                    placeholder={`Enter ${filter}...`}
                    value={filterValue}
                    onChange={(event) => handleFilterValueChange(event.target.value)}
                    className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
                  />
                ) : (
                  <div className="hidden lg:block" />
                )}

                <select
                  value={selectedSavedFilterId}
                  onChange={(event) => applySavedFilter(event.target.value)}
                  className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                >
                  <option value="">Saved Filters</option>
                  {savedFilters.map((savedFilter) => (
                    <option key={savedFilter.id} value={savedFilter.id}>
                      {savedFilter.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="mt-3">
                <input
                  type="text"
                  placeholder="Search tickets..."
                  value={searchQuery}
                  onChange={(event) => handleSearchChange(event.target.value)}
                  className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
                />
              </div>
            </div>

            {(loading || apiUnavailable) && <TicketGridSkeleton />}

            {error && !apiUnavailable && (
              <div className="bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
                <p className="text-red-700 dark:text-red-300">{error}</p>
              </div>
            )}

            {!loading && !apiUnavailable && !error && tickets.length === 0 && (
              <p className="text-gray-600 dark:text-slate-400 text-center">
                No tickets found
              </p>
            )}

            {!loading && !apiUnavailable && !error && tickets.length > 0 && (
              <>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
                  {pagedTickets.map((ticket) => (
                    <TicketCard
                      key={ticket.id}
                      ticket={ticket}
                      onClick={() => openTicket(ticket)}
                    />
                  ))}
                </div>

                <div className="mt-6 flex flex-col gap-3 rounded-lg border border-gray-200 bg-white/80 px-4 py-3 shadow-sm dark:border-slate-800 dark:bg-slate-900/80 sm:flex-row sm:items-center sm:justify-between">
                  <p className="text-sm text-gray-600 dark:text-slate-400">
                    Showing {showingStart}-{showingEnd} of {totalTickets} tickets
                  </p>
                  {pageSize !== "all" && totalPages > 1 && (
                    <div className="flex items-center gap-3 sm:ml-auto">
                      <p className="text-sm text-gray-600 dark:text-slate-400">
                        Page {currentPage} of {totalPages}
                      </p>
                      <button
                        onClick={() =>
                          setCurrentPage((page) => Math.max(1, page - 1))
                        }
                        disabled={currentPage === 1}
                        className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        Previous
                      </button>
                      <button
                        onClick={() =>
                          setCurrentPage((page) => Math.min(totalPages, page + 1))
                        }
                        disabled={currentPage === totalPages}
                        className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        Next
                      </button>
                    </div>
                  )}
                </div>
              </>
            )}
          </>
        ) : activeView === "archived" && canViewArchived ? (
          <ArchivedTicketsPage
            tickets={archivedTickets}
            loading={archivedLoading || apiUnavailable}
            error={apiUnavailable ? null : archivedError}
            highlightedTicketId={highlightedArchivedTicketId}
            onRefresh={() => void loadArchivedTickets()}
            canReactivate={canUpdateTickets}
            reactivatingTicketId={reactivatingArchivedTicketId}
            onReactivate={handleReactivateArchivedTicket}
          />
        ) : activeView === "reports" && canViewReports ? (
          <ReportsPage
            tickets={allTickets}
            onlineUsers={onlineUsers}
            customReports={customReports.filter((report) => report.isEnabled)}
            customReportResult={customReportResult}
            loading={loading || apiUnavailable}
            onlineUsersLoading={onlineUsersLoading || apiUnavailable}
            customReportLoading={customReportResultLoading || apiUnavailable}
            error={apiUnavailable ? null : error}
            onlineUsersError={apiUnavailable ? null : onlineUsersError}
            customReportError={apiUnavailable ? null : customReportResultError}
            showSlaLegend={showReportSlaLegend}
            canViewOnlineUsers={canViewOnlineUsersReport}
            canViewCustomReports={canManageCustomReports}
            activeSection={activeReportSection}
            onChangeSection={setActiveReportSection}
            selectedCustomReportId={selectedCustomReportId}
            onSelectCustomReport={setSelectedCustomReportId}
            onToggleSlaLegend={() =>
              setShowReportSlaLegend((currentValue) => !currentValue)
            }
            onRefresh={() => void loadAllTickets()}
            onRefreshOnlineUsers={() => void loadOnlineUsers()}
            onRefreshCustomReport={() => {
              if (selectedCustomReportId !== null) {
                void runCustomReport(selectedCustomReportId);
              }
            }}
            onExportCsv={() => void exportReportCsv(false)}
            onExportGoogleSheets={() => void exportReportCsv(true)}
            onOpenTicket={openTicket}
          />
        ) : activeView === "jobs" && canManageJobs ? (
          <JobsPage
            jobs={jobs}
            storedProcedures={storedProcedures}
            loading={jobsLoading}
            error={jobsError}
            saving={jobsSaving}
            runningJobId={runningJobId}
            onRefresh={() => void loadJobs()}
            onCreate={createScheduledJob}
            onUpdate={updateScheduledJob}
            onRunNow={runScheduledJobNow}
          />
        ) : activeView === "sla" && canManageConfiguration ? (
          apiUnavailable ? (
            <ConfigurationSkeleton />
          ) : (
            <ConfigurationPage
              slaConfigurations={slaConfigurations}
              slaError={slaError}
              slaLoading={slaLoading}
              slaSaving={slaSaving}
              onSlaChange={handleSlaConfigurationChange}
              onRefreshSla={() => void loadSlaConfigurations()}
              onSaveSla={() => void saveSlaConfigurations()}
              sessionConfiguration={sessionConfiguration}
              sessionError={sessionError}
              sessionLoading={sessionLoading}
              sessionSaving={sessionSaving}
              onSessionChange={handleSessionConfigurationChange}
              onRefreshSession={() => void loadSessionConfiguration()}
              onSaveSession={() => void saveSessionConfiguration()}
              notificationChannelConfiguration={notificationChannelConfiguration}
              notificationChannelError={notificationChannelError}
              notificationChannelLoading={notificationChannelLoading}
              notificationChannelSaving={notificationChannelSaving}
              onNotificationChannelChange={
                handleNotificationChannelConfigurationChange
              }
              onRefreshNotificationChannels={() =>
                void loadNotificationChannelConfiguration()
              }
              onSaveNotificationChannels={() =>
                void saveNotificationChannelConfiguration()
              }
              ticketBoards={ticketBoards}
              ticketBoardError={ticketBoardError}
              ticketBoardLoading={ticketBoardLoading}
              ticketBoardSaving={ticketBoardSaving}
              ticketBoardDeletingId={deletingTicketBoardId}
              onRefreshTicketBoards={() => void loadTicketBoards()}
              onCreateTicketBoard={createTicketBoard}
              onUpdateTicketBoard={updateTicketBoard}
              onDeleteTicketBoard={deleteTicketBoard}
              ticketStatuses={ticketStatuses}
              ticketStatusError={ticketStatusError}
              ticketStatusLoading={ticketStatusLoading}
              ticketStatusSaving={ticketStatusSaving}
              ticketStatusDeletingId={deletingTicketStatusId}
              onRefreshTicketStatuses={() => void loadTicketStatuses()}
              onCreateTicketStatus={createTicketStatusDefinition}
              onUpdateTicketStatus={updateTicketStatusDefinition}
              onDeleteTicketStatus={deleteTicketStatusDefinition}
              ticketRoutingRules={ticketRoutingRules}
              selectedTicketRoutingRule={selectedTicketRoutingRule}
              ticketRoutingError={ticketRoutingError}
              ticketRoutingLoading={ticketRoutingLoading}
              ticketRoutingSaving={ticketRoutingSaving}
              ticketRoutingDeletingId={deletingTicketRoutingRuleId}
              onRefreshTicketRouting={() => void loadTicketRoutingRules()}
              onCreateTicketRoutingRule={createTicketRoutingRule}
              onSelectTicketRoutingRule={selectTicketRoutingRule}
              onTicketRoutingChange={handleTicketRoutingRuleChange}
              onSaveTicketRoutingRule={saveTicketRoutingRule}
              onDeleteTicketRoutingRule={deleteTicketRoutingRule}
              archiveConfigurations={archiveConfigurations}
              archiveConfiguration={archiveConfiguration}
              archiveError={archiveError}
              archiveLoading={archiveLoading}
              archiveSaving={archiveSaving}
              archiveDeletingId={deletingArchiveConfigurationId}
              archiveRunning={archiveRunning}
              onCreateArchivePolicy={createArchivePolicy}
              onSelectArchivePolicy={selectArchivePolicy}
              onArchiveChange={handleArchiveConfigurationChange}
              onRefreshArchive={() => void loadArchiveConfigurations()}
              onSaveArchive={() => void saveArchiveConfiguration()}
              onDeleteArchive={() => void deleteArchiveConfiguration()}
              onRunArchiveNow={() => void runArchiveNow()}
              customReports={customReports}
              databaseViews={databaseViews}
              databaseViewsLoading={databaseViewsLoading}
              customReportError={customReportsError}
              customReportLoading={customReportsLoading}
              customReportSaving={customReportsSaving}
              customReportDeletingId={deletingCustomReportId}
              onRefreshCustomReports={() => void loadCustomReports()}
              onCreateCustomReport={createCustomReport}
              onUpdateCustomReport={updateCustomReport}
              onDeleteCustomReport={deleteCustomReport}
              storedProcedures={storedProcedures}
              databaseStoredProcedures={databaseStoredProcedures}
              databaseStoredProceduresLoading={databaseStoredProceduresLoading}
              storedProcedureError={storedProcedureError}
              storedProcedureLoading={storedProcedureLoading}
              storedProcedureSaving={storedProcedureSaving}
              storedProcedureDeletingId={deletingStoredProcedureId}
              onRefreshStoredProcedures={() => void loadStoredProcedures()}
              onCreateStoredProcedure={createStoredProcedureDefinition}
              onUpdateStoredProcedure={updateStoredProcedureDefinition}
              onDeleteStoredProcedure={deleteStoredProcedureDefinition}
            />
          )
        ) : activeView === "users" && canViewUsers ? (
          <UsersPage
            users={users}
            loading={usersLoading || apiUnavailable}
            error={apiUnavailable ? null : usersError}
            canCreate={canCreateUsers}
            canEdit={canEditUsers}
            canDelete={canDeleteUsers}
            currentUserId={currentUser?.id}
            deletingUserId={deletingUserId}
            onRefresh={() => void loadUsers()}
            onCreate={openCreateUserModal}
            onEdit={openAdminUserModal}
            onDelete={(userRecord) => void deleteUserRecord(userRecord)}
          />
        ) : (
          <div className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-6">
            <p className="text-gray-600 dark:text-slate-400">
              You do not have permission to view this section.
            </p>
          </div>
        )}
        </main>
      </div>

      {selectedTicket && isModalOpen && (
        <TicketModal
          key={selectedTicket.id ?? "new"}
          ticket={selectedTicket}
          latestRealtimeEvent={latestRealtimeEvent}
          ticketBoards={availableTicketBoards}
          ticketStatuses={
            ticketStatuses.length > 0
              ? ticketStatuses
              : DEFAULT_TICKET_STATUS_NAMES.map((name, index) => ({
                  id: index + 1,
                  name,
                  isEnabled: true,
                  createdDateUtc: "",
                }))
          }
          isOpen
          onClose={closeModal}
          onSave={handleSaveTicket}
          onArchive={handleArchiveTicket}
          onDelete={requestDeleteTicket}
          currentUser={
            currentUser
              ? {
                  displayName: currentUser.displayName ?? "",
                  department: currentUser.department ?? "",
                  role: currentUser.role ?? "",
                }
              : null
          }
          createdByDisplayName={
            selectedTicket.createdByDisplayName ??
            (!selectedTicket.id ? currentUser?.displayName ?? user?.name ?? "" : "")
          }
        />
      )}

      <SaveTicketFilterModal
        isOpen={isSaveFilterModalOpen}
        name={savedFilterName}
        onNameChange={setSavedFilterName}
        onClose={closeSaveFilterModal}
        onSave={saveCurrentFilter}
      />

      <ConfirmDeleteModal
        isOpen={!!ticketToDelete}
        onCancel={() => setTicketToDelete(null)}
        onConfirm={() => void confirmDeleteTicket()}
        loading={deleting}
      />

      <UserProfileModal
        isOpen={isProfileModalOpen}
        user={currentUser}
        draft={profileDraft}
        saving={profileSaving}
        onChange={handleProfileDraftChange}
        onClose={closeProfileModal}
        onSave={() => void saveProfile()}
      />

      <AdminUserEditModal
        isOpen={!!editingAdminUser}
        user={editingAdminUser}
        draft={adminUserDraft}
        saving={adminUserSaving}
        onChange={handleAdminUserDraftChange}
        onClose={closeAdminUserModal}
        onSave={() => void saveAdminUser()}
      />

      <AdminUserCreateModal
        isOpen={isCreateUserModalOpen}
        draft={createUserDraft}
        saving={createUserSaving}
        canAssignAdminRole={isAdmin}
        onChange={handleCreateUserDraftChange}
        onClose={closeCreateUserModal}
        onSave={() => void saveCreatedUser()}
      />

      <SessionTimeoutModal
        state={sessionPromptState}
        remainingSeconds={sessionRemainingSeconds}
        inactivityTimeoutMinutes={effectiveSessionConfiguration.inactivityTimeoutMinutes}
        onContinue={continueSessionAfterWarning}
        onReauthenticate={reauthenticateDueToInactivity}
      />
    </div>
  );
}

export default App;
