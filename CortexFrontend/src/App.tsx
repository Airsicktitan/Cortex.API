import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type MouseEvent as ReactMouseEvent,
} from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { Ticket } from "./types/ticket";
import type { UserNotification } from "./types/notification";
import type { RealtimeEvent } from "./types/realtime";
import type { SessionConfiguration } from "./types/sessionConfiguration";
import type { TicketBoardDefinition } from "./types/ticketBoard";
import type { TicketStatusDefinition } from "./types/ticketStatus";
import type {
  UpdateUserProfileInput,
  UserProfile,
} from "./types/user";
import {
  API_USER_MESSAGES,
  ApiError,
  getUserFacingErrorMessage,
  isLikelyNetworkError,
  userService,
} from "./services/api";
import { notificationService } from "./services/notificationService";
import { realtimeService } from "./services/realtimeService";
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
import { useUsers } from "./hooks/useUsers";
import { useConfiguration } from "./hooks/useConfiguration";
import { useTickets } from "./hooks/useTickets";
import toast from "react-hot-toast";
import {
  normalizeRoles,
  isAdmin as checkIsAdmin,
  canManageUsers,
  canAccessConfig,
  canViewReports,
  canManageJobs,
  canManageReportDefinitions,
  canCreateTickets,
  canEditTickets,
} from "./utils/role";

const API_AUDIENCE = "https://cortex-api";
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
const TICKET_AUTO_REFRESH_INTERVAL_MS = 15000;
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

type TicketListSortOption =
  | "newest-first"
  | "oldest-first"
  | "priority-high-low"
  | "priority-low-high"
  | "due-soonest"
  | "most-overdue";

function isTicketListSortOption(value: string): value is TicketListSortOption {
  return (
    value === "newest-first" ||
    value === "oldest-first" ||
    value === "priority-high-low" ||
    value === "priority-low-high" ||
    value === "due-soonest" ||
    value === "most-overdue"
  );
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

async function loadBootstrapCurrentUser(token: string) {
  return await userService.getCurrentUser(token).catch(
    (error) => {
      console.warn("Current user profile could not be loaded", error);
      return null;
    },
  );
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
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [apiUnavailable, setApiUnavailable] = useState(false);
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);
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
  const [latestRealtimeEvent, setLatestRealtimeEvent] = useState<RealtimeEvent | null>(null);

  const [bootstrapComplete, setBootstrapComplete] = useState(false);
  const [needsConsent, setNeedsConsent] = useState(false);

  // Configuration domain state is owned by useConfiguration (wired below after loadArchivedTickets).

  const [sessionPromptState, setSessionPromptState] =
    useState<SessionPromptState>(null);
  const [sessionRemainingSeconds, setSessionRemainingSeconds] = useState(
    DEFAULT_SESSION_CONFIGURATION.inactivityTimeoutMinutes * 60,
  );
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
  // User management state is owned by useUsers (wired below after getApiToken).
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

  const authRoles = useMemo(
    () => normalizeRoles(currentUser?.roles, currentUser?.role),
    [currentUser?.roles, currentUser?.role],
  );

  const isAdmin = useMemo(() => checkIsAdmin(authRoles), [authRoles]);
  const isDarkMode = theme === "dark";
  const isAccountExpired = isUserExpired(currentUser);
  const isAccountInactive = isUserInactive(currentUser);
  // effectiveSessionConfiguration, sessionTimeoutSeconds, sessionWarningSeconds
  // are computed after useConfiguration (below) because they depend on sessionConfiguration.
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

  const sessionUnlocked =
    bootstrapComplete && !needsConsent && Boolean(currentUser);

  const canCreateTicketsCap = sessionUnlocked && canCreateTickets(authRoles);
  const canEditTicketsCap = sessionUnlocked && canEditTickets(authRoles);
  const canViewTicketSections = sessionUnlocked;
  const canViewDashboard = canViewTicketSections;
  const canViewReportsNav = sessionUnlocked && canViewReports(authRoles);
  const canViewOnlineUsersReport = canViewReportsNav;
  const canManageCustomReportDefinitions =
    sessionUnlocked && canManageReportDefinitions(authRoles);
  const canViewArchived = canViewTicketSections;
  const canManageConfiguration = sessionUnlocked && canAccessConfig(authRoles);
  const canManageJobsNav = sessionUnlocked && canManageJobs(authRoles);
  const canViewUsers = sessionUnlocked && canManageUsers(authRoles);
  const canCreateUsers = canViewUsers;
  const canEditUsers = sessionUnlocked && canManageUsers(authRoles);
  const canDeleteUsers = sessionUnlocked && canManageUsers(authRoles);
  // failedJobsCount is computed after useConfiguration (below) because it depends on jobs.
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
        enabled: canViewReportsNav,
      },
      {
        view: "jobs",
        label: "Jobs",
        description: "Create and manage background automation jobs.",
        enabled: canManageJobsNav,
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

    return items.filter((item) => item.enabled);
  }, [
    canManageJobsNav,
    canManageConfiguration,
    canViewArchived,
    canViewDashboard,
    canViewReportsNav,
    canViewTicketSections,
    canViewUsers,
  ]);
  const activeNavigationItem =
    navigationItems.find((item) => item.view === activeView) ?? null;

  const isViewAllowed = useCallback(
    (view: AppView) =>
      view === "dashboard" ||
      view === "tickets" ||
      view === "archived" ||
      (view === "reports" && canViewReportsNav) ||
      (view === "jobs" && canManageJobsNav) ||
      (view === "sla" && canManageConfiguration) ||
      (view === "users" && canViewUsers),
    [canManageConfiguration, canManageJobsNav, canViewReportsNav, canViewUsers],
  );

  const getFallbackView = useCallback((): AppView => {
    if (canViewTicketSections) {
      return "tickets";
    }
    if (canViewReportsNav) {
      return "reports";
    }
    if (canManageJobsNav) {
      return "jobs";
    }
    if (canManageConfiguration) {
      return "sla";
    }
    if (canViewUsers) {
      return "users";
    }

    return "dashboard";
  }, [
    canManageConfiguration,
    canManageJobsNav,
    canViewReportsNav,
    canViewTicketSections,
    canViewUsers,
  ]);

  const getApiToken = useCallback(async () => {
    return await getAccessTokenSilently({
      authorizationParams: API_AUTHORIZATION_PARAMS,
    });
  }, [getAccessTokenSilently]);

  const getFreshApiToken = useCallback(async () => {
    return await getAccessTokenSilently({
      authorizationParams: API_AUTHORIZATION_PARAMS,
      cacheMode: "off",
    } as Parameters<typeof getAccessTokenSilently>[0]);
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

  // markSessionActivity is defined after useConfiguration (below) because its dep array
  // references sessionTimeoutSeconds which depends on sessionConfiguration from the hook.

  const performLogout = useCallback(() => {
    sessionPromptStateRef.current = null;
    clearSessionTimeoutState();
    logout({
      logoutParams: { returnTo: window.location.origin },
    });
  }, [clearSessionTimeoutState, logout]);

  const {
    allTickets,
    setAllTickets,
    filter,
    setFilter,
    filterValue,
    setFilterValue,
    selectedBoardId,
    setSelectedBoardId,
    searchQuery,
    setSearchQuery,
    pageSize,
    setPageSize,
    ticketListSort,
    setTicketListSort,
    myTicketsOnly,
    setMyTicketsOnly,
    currentPage,
    setCurrentPage,
    selectedTicket,
    setSelectedTicket,
    isModalOpen,
    archivedTickets,
    setArchivedTickets,
    archivedLoading,
    archivedError,
    highlightedArchivedTicketId,
    setHighlightedArchivedTicketId,
    reactivatingArchivedTicketId,
    ticketToDelete,
    setTicketToDelete,
    deleting,
    refreshTicketsSilently,
    loadAllTickets,
    loadArchivedTickets,
    tickets,
    totalTickets,
    totalPages,
    pagedTickets,
    showingStart,
    showingEnd,
    handleSaveTicket,
    requestDeleteTicket,
    confirmDeleteTicket,
    handleArchiveTicket,
    handleReactivateArchivedTicket,
    closeModal,
    openTicket,
    openTicketById,
  } = useTickets({
    getApiToken,
    setApiUnavailable,
    setLoading,
    setError,
    setNeedsConsent,
    currentUser,
    auth0Name: user?.name,
    auth0Email: user?.email,
    isConsentRequiredError,
    isForbiddenError,
    isLikelyNetworkError,
  });

  // loadSlaConfigurations and other configuration loaders live in useConfiguration.

  // continueSessionAfterWarning is defined after useConfiguration (below) because its
  // dep array references markSessionActivity which depends on sessionTimeoutSeconds.

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

  // loadSessionConfiguration, loadNotificationChannelConfiguration, loadTicketBoards,
  // loadTicketStatuses, loadTicketRoutingRules, loadArchiveConfigurations,
  // loadStoredProcedures, loadDatabaseStoredProcedures, loadJobs — all live in useConfiguration.

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

        if (isLikelyNetworkError(error)) {
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

  // loadTicketStatuses, loadTicketRoutingRules, loadArchiveConfigurations,
  // loadStoredProcedures, loadDatabaseStoredProcedures, loadJobs — all in useConfiguration.

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

        if (isLikelyNetworkError(error)) {
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

  // ── Configuration ────────────────────────────────────────────────────────
  const {
    slaConfigurations,
    slaLoading,
    slaSaving,
    slaError,
    loadSlaConfigurations,
    handleSlaConfigurationChange,
    saveSlaConfigurations,
    sessionConfiguration,
    sessionLoadedOnce,
    sessionLoading,
    sessionSaving,
    sessionError,
    loadSessionConfiguration,
    handleSessionConfigurationChange,
    saveSessionConfiguration,
    notificationChannelConfiguration,
    notificationChannelsLoadedOnce,
    notificationChannelLoading,
    notificationChannelSaving,
    notificationChannelError,
    loadNotificationChannelConfiguration,
    handleNotificationChannelConfigurationChange,
    saveNotificationChannelConfiguration,
    ticketBoards,
    ticketBoardLoading,
    ticketBoardSaving,
    deletingTicketBoardId,
    ticketBoardError,
    loadTicketBoards,
    createTicketBoard,
    updateTicketBoard,
    deleteTicketBoard,
    ticketStatuses,
    ticketStatusLoading,
    ticketStatusSaving,
    deletingTicketStatusId,
    ticketStatusError,
    loadTicketStatuses,
    createTicketStatusDefinition,
    updateTicketStatusDefinition,
    deleteTicketStatusDefinition,
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
    clearCustomReportResult,
  } = useConfiguration({
    getApiToken,
    setApiUnavailable,
    setAllTickets,
    setArchivedTickets,
    setSelectedTicket,
    setSelectedBoardId,
    refreshTicketsSilently,
    loadArchivedTickets,
    onActiveReportSectionChange: setActiveReportSection,
    onSelectedCustomReportIdChange: setSelectedCustomReportId,
  });

  // These depend on values from useConfiguration and must come after it.
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
  const failedJobsCount = useMemo(
    () => jobs.filter((job) => job.lastRunStatus === "Failed").length,
    [jobs],
  );
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
  const continueSessionAfterWarning = useCallback(() => {
    markSessionActivity();
  }, [markSessionActivity]);

  // ── User management ───────────────────────────────────────────────────────
  // redirectToLogin is a stable wrapper passed into the hook so it doesn't
  // need to import Auth0 internals or API_AUTHORIZATION_PARAMS directly.
  const redirectToLogin = useCallback(() => {
    void loginWithRedirect({
      authorizationParams: {
        ...API_AUTHORIZATION_PARAMS,
        prompt: "login",
        max_age: 0,
      },
    });
  }, [loginWithRedirect]);

  const {
    users,
    usersLoading,
    usersError,
    onlineUsers,
    onlineUsersLoading,
    onlineUsersError,
    isCreateUserModalOpen,
    createUserDraft,
    createUserSaving,
    editingAdminUser,
    adminUserDraft,
    adminAuth0Roles,
    availableAuth0Roles,
    adminRolesLoading,
    roleMutationLoading,
    adminAccessFeedback,
    adminAccessError,
    adminUserSaving,
    sessionRefreshInProgress,
    sessionRefreshNotice,
    deletingUserId,
    loadUsers,
    loadOnlineUsers,
    openCreateUserModal,
    closeCreateUserModal,
    openAdminUserModal,
    closeAdminUserModal,
    handleAdminUserDraftChange,
    handleCreateUserDraftChange,
    saveAdminUser,
    saveCreatedUser,
    deleteUserRecord,
    handleAddAuth0Role,
    handleRemoveAuth0Role,
    forceSessionRefreshForAuthChanges,
    updateUserRecord,
  } = useUsers({
    getApiToken,
    getFreshApiToken,
    currentUser,
    auth0Sub: user?.sub,
    setCurrentUser,
    setApiUnavailable,
    canEditUsers,
    canManageJobsNav,
    redirectToLogin,
    loadJobs, // comes from useConfiguration
  });

  // loadCustomReports through runCustomReport — all in useConfiguration.

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
    if (
      !isAuthenticated ||
      !bootstrapComplete ||
      needsConsent ||
      !canViewTicketSections ||
      activeView !== "tickets"
    ) {
      return;
    }

    const isUserInteractingWithForm = () => {
      if (typeof document === "undefined") {
        return false;
      }

      const activeElement = document.activeElement as HTMLElement | null;
      if (!activeElement) {
        return false;
      }

      if (activeElement.isContentEditable) {
        return true;
      }

      return ["INPUT", "TEXTAREA", "SELECT"].includes(activeElement.tagName);
    };

    const intervalId = window.setInterval(() => {
      if (loading || isModalOpen || isUserInteractingWithForm()) {
        return;
      }

      void refreshTicketsSilently();
    }, TICKET_AUTO_REFRESH_INTERVAL_MS);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [
    activeView,
    canViewTicketSections,
    isAuthenticated,
    isModalOpen,
    loading,
    needsConsent,
    bootstrapComplete,
    refreshTicketsSilently,
  ]);

  useEffect(() => {
    if (!isAuthenticated || !bootstrapComplete || needsConsent || !canViewTicketSections) {
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
    bootstrapComplete,
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
      setBootstrapComplete(false);

      try {
        const token = await getAccessTokenSilently({
          authorizationParams: API_AUTHORIZATION_PARAMS,
        });
        if (cancelled) return;
        const fetchedCurrentUser = await loadBootstrapCurrentUser(token);

        if (cancelled) return;

        setCurrentUser(fetchedCurrentUser);
        await loadAllTickets(token);
        setNeedsConsent(false);
        setApiUnavailable(false);
        void loadTicketBoards(token);
        void loadSessionConfiguration(token);
        void loadNotifications(token, { silent: true });
      } catch (error) {
        console.error("Bootstrap failed", error);

        if (cancelled) return;

        if (isConsentRequiredError(error)) {
          setNeedsConsent(true);
          setApiUnavailable(false);
          setError("CORTEX API consent is required before the app can load.");
        } else if (isForbiddenError(error)) {
          setNeedsConsent(false);
          setApiUnavailable(false);
          setError("You do not have permission to view tickets.");
        } else if (isLikelyNetworkError(error)) {
          setNeedsConsent(false);
          setApiUnavailable(true);
          setError(null);
        } else {
          setNeedsConsent(false);
          setApiUnavailable(false);
          setError(API_USER_MESSAGES.generic);
        }
      } finally {
        if (!cancelled) {
          setBootstrapComplete(true);
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
    loadAllTickets,
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

    if (!customReportsLoadedOnce && canManageCustomReportDefinitions) {
      void loadCustomReportDefinitions();
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
    canManageCustomReportDefinitions,
    loadCustomReportDefinitions,
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
    if (activeView !== "jobs" || !canManageJobsNav) {
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
    canManageJobsNav,
    jobsLoaded,
    loadJobs,
    loadStoredProcedures,
    storedProcedures.length,
  ]);

  useEffect(() => {
    if (!canManageJobsNav || jobsLoaded || jobsLoading) {
      return;
    }

    void loadJobs();
  }, [canManageJobsNav, jobsLoaded, jobsLoading, loadJobs]);

  useEffect(() => {
    if (
      !isAuthenticated ||
      !bootstrapComplete ||
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
    bootstrapComplete,
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
  }, [activeView, highlightedArchivedTicketId, setHighlightedArchivedTicketId]);

  useEffect(() => {
    if (!sessionUnlocked || isViewAllowed(activeView)) {
      return;
    }

    setActiveView(getFallbackView());
    setIsAppMenuOpen(false);
    setIsNotificationPanelOpen(false);
  }, [activeView, getFallbackView, isViewAllowed, sessionUnlocked]);

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
      !canViewReportsNav ||
      customReportsLoadedOnce
    ) {
      return;
    }

    void loadCustomReports();
  }, [
    activeView,
    canViewReportsNav,
    customReportsLoadedOnce,
    loadCustomReports,
  ]);

  useEffect(() => {
    if (activeReportSection !== "custom") {
      return;
    }

    if (!canViewReportsNav) {
      setActiveReportSection("sla");
      setSelectedCustomReportId(null);
      return;
    }

    const enabledReports = customReports.filter((report) => report.isEnabled);
    if (enabledReports.length === 0) {
      setSelectedCustomReportId(null);
      clearCustomReportResult();
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
    canViewReportsNav,
    clearCustomReportResult,
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

    try {
      const token = await getAccessTokenWithPopup({
        authorizationParams: API_AUTHORIZATION_PARAMS,
      });
      if (!token) {
        throw new Error("No access token was returned.");
      }
      const fetchedCurrentUser = await loadBootstrapCurrentUser(token);

      setCurrentUser(fetchedCurrentUser);
      await loadAllTickets(token);
      setNeedsConsent(false);
      setBootstrapComplete(true);
      setApiUnavailable(false);
      void loadTicketBoards(token);
      void loadNotifications(token, { silent: true });
    } catch (error) {
      console.error("Consent failed", error);

      if (isLikelyNetworkError(error)) {
        setApiUnavailable(true);
      } else {
        setApiUnavailable(false);
        setError("Failed to grant CORTEX API access.");
        toast.error(
          getUserFacingErrorMessage(error, "Failed to grant CORTEX API access"),
        );
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

  useEffect(() => {
    if (selectedBoardId === "all") {
      return;
    }

    if (!boardTabs.some((board) => board.id === selectedBoardId)) {
      setSelectedBoardId("all");
    }
  }, [boardTabs, selectedBoardId, setSelectedBoardId]);

  const handleViewChange = (view: AppView) => {
    if (!isViewAllowed(view)) {
      toast.error("You do not have access to this section.");
      return;
    }

    setActiveView(view);
    setIsAppMenuOpen(false);
    setIsNotificationPanelOpen(false);
  };

  const openFailedJobsQueue = () => {
    if (!canManageJobsNav) {
      return;
    }

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
        getUserFacingErrorMessage(error, "Failed to mark notifications as read."),
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
      setNotificationsError(getUserFacingErrorMessage(error, "Failed to open notification."));
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
      toast.error(getUserFacingErrorMessage(error, "Failed to load profile"));
    } finally {
      setProfileLoading(false);
    }
  };

  const closeProfileModal = () => {
    setIsProfileModalOpen(false);
  };

  // openCreateUserModal, closeCreateUserModal, openAdminUserModal,
  // closeAdminUserModal — all live in useUsers, destructured above.

  const handleProfileDraftChange = (
    field: keyof UpdateUserProfileInput,
    value: string,
  ) => {
    setProfileDraft((currentDraft) => ({
      ...currentDraft,
      [field]: value,
    }));
  };

  // handleAdminUserDraftChange, refreshSessionAfterSelfRoleChange,
  // forceSessionRefreshForAuthChanges, handleAddAuth0Role, handleRemoveAuth0Role,
  // handleCreateUserDraftChange — all live in useUsers, destructured above.

  const saveProfile = async () => {
    if (!currentUser) return;

    try {
      setProfileSaving(true);
      const token = await getApiToken();
      const updatedUser = await userService.updateProfile(profileDraft, token);

      setCurrentUser(updatedUser);
      updateUserRecord(updatedUser);
      setIsProfileModalOpen(false);
      toast.success("Profile updated");
    } catch (error) {
      console.error("Failed to update profile", error);
      toast.error(getUserFacingErrorMessage(error, "Failed to update profile"));
    } finally {
      setProfileSaving(false);
    }
  };

  // saveAdminUser, saveCreatedUser, deleteUserRecord — all live in useUsers, destructured above.

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
        <div className="mx-auto flex w-full max-w-[2200px] flex-col gap-6 px-4 py-5 sm:px-6 sm:py-6 2xl:px-8 xl:flex-row xl:items-center xl:justify-between">
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

                {canCreateTicketsCap && (
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

            {bootstrapComplete && needsConsent && (
              <div className="flex items-center gap-2">
                <span className="text-sm text-yellow-700 dark:text-amber-300">
                  CORTEX API consent is required before the app can
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

            {sessionRefreshNotice && (
              <div className="flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 dark:border-amber-900/40 dark:bg-amber-950/30">
                <span className="text-sm text-amber-800 dark:text-amber-200">
                  {sessionRefreshNotice}
                </span>
                <button
                  onClick={forceSessionRefreshForAuthChanges}
                  disabled={sessionRefreshInProgress}
                  className="rounded-md bg-amber-600 px-3 py-1.5 text-sm text-white transition-colors hover:bg-amber-700 disabled:cursor-not-allowed disabled:opacity-70"
                >
                  {sessionRefreshInProgress ? "Refreshing..." : "Refresh Session"}
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

              {canManageJobsNav && failedJobsCount > 0 && (
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

      <div className="mx-auto flex w-full max-w-[2200px] flex-1 px-4 py-6 sm:px-6 sm:py-8 lg:gap-8 2xl:px-8">
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

              <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-end">
                <div
                  className="flex shrink-0 gap-1 rounded-full border border-gray-200 bg-gray-50 p-1 dark:border-slate-700 dark:bg-slate-800"
                  role="group"
                  aria-label="Ticket scope"
                >
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedSavedFilterId("");
                      setMyTicketsOnly(false);
                    }}
                    className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                      !myTicketsOnly
                        ? "bg-cortex-blue text-white"
                        : "text-gray-700 hover:bg-gray-100 dark:text-slate-200 dark:hover:bg-slate-700"
                    }`}
                  >
                    All Tickets
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedSavedFilterId("");
                      setMyTicketsOnly(true);
                    }}
                    className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                      myTicketsOnly
                        ? "bg-cortex-blue text-white"
                        : "text-gray-700 hover:bg-gray-100 dark:text-slate-200 dark:hover:bg-slate-700"
                    }`}
                  >
                    My Tickets
                  </button>
                </div>
                <input
                  type="text"
                  placeholder="Search tickets..."
                  value={searchQuery}
                  onChange={(event) => handleSearchChange(event.target.value)}
                  className="min-w-0 flex-1 rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
                />
                <select
                  aria-label="Sort tickets"
                  value={ticketListSort}
                  onChange={(event) => {
                    const value = event.target.value;
                    if (isTicketListSortOption(value)) {
                      setSelectedSavedFilterId("");
                      setTicketListSort(value);
                    }
                  }}
                  className="w-full shrink-0 rounded-md border-gray-300 bg-white text-gray-900 shadow-sm sm:w-52 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                >
                  <option value="newest-first">Newest first</option>
                  <option value="oldest-first">Oldest first</option>
                  <option value="priority-high-low">Priority (high → low)</option>
                  <option value="priority-low-high">Priority (low → high)</option>
                  <option value="due-soonest">Due soonest</option>
                  <option value="most-overdue">Most overdue</option>
                </select>
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
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
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
            canReactivate={canEditTicketsCap}
            reactivatingTicketId={reactivatingArchivedTicketId}
            onReactivate={handleReactivateArchivedTicket}
          />
        ) : activeView === "reports" && canViewReportsNav ? (
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
            canViewCustomReports={canViewReportsNav}
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
        ) : activeView === "jobs" && canManageJobsNav ? (
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
              onRefreshCustomReports={() => void loadCustomReportDefinitions()}
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
              canExportAdminLogs={isAdmin}
              onExportAdminLogs={exportAdminLogsCsv}
              canManageJobs={canManageJobsNav}
              canManageReportDefinitions={canManageCustomReportDefinitions}
              canViewUsers={canViewUsers}
              onOpenJobs={() => setActiveView("jobs")}
              onOpenUsers={() => setActiveView("users")}
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
                  roles: currentUser.roles,
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
        canManageAccess={canEditUsers}
        auth0AssignedRoles={adminAuth0Roles}
        availableAuth0Roles={availableAuth0Roles}
        rolesLoading={adminRolesLoading}
        roleMutationLoading={roleMutationLoading}
        accessFeedback={adminAccessFeedback}
        accessError={adminAccessError}
        onAddRole={(roleName) => void handleAddAuth0Role(roleName)}
        onRemoveRole={(roleName) => void handleRemoveAuth0Role(roleName)}
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
