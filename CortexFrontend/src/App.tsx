import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { Ticket } from "./types/ticket";
import type { UserNotification } from "./types/notification";
import type { RealtimeEvent } from "./types/realtime";
import type { SessionConfiguration } from "./types/sessionConfiguration";
import type { TicketBoardDefinition } from "./types/ticketBoard";
import type { TicketStatusDefinition } from "./types/ticketStatus";
import type { UpdateUserProfileInput, UserProfile } from "./types/user";
import {
  API_USER_MESSAGES,
  ApiError,
  getUserFacingErrorMessage,
  isLikelyNetworkError,
  userService,
} from "./services/api";
import { notificationService } from "./services/notificationService";
import { realtimeService } from "./services/realtimeService";
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
import { ConfigurationSkeleton } from "./components/LoadingSkeletons";
import AppHeader from "./components/AppHeader";
import AppSidebar from "./components/AppSidebar";
import TicketsContainer from "./components/TicketsContainer";
import { applyTheme, getPreferredTheme, type ThemeMode } from "./theme";
import { useUsers } from "./hooks/useUsers";
import { useConfiguration } from "./hooks/useConfiguration";
import { useTickets } from "./hooks/useTickets";
import { useSavedFilters } from "./hooks/useSavedFilters";
import toast from "react-hot-toast";
import {
  normalizeRoles,
  isAdmin as checkIsAdmin,
  canManageUsers,
  canAccessConfig,
  canViewReports,
  canManageJobs,
  canViewJobActivity,
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
const SESSION_REAUTH_PENDING_STORAGE_KEY_PREFIX =
  "cortex:session-reauth-pending";
const SIDEBAR_MIN_WIDTH = 232;
const SIDEBAR_MAX_WIDTH = 440;
const SIDEBAR_DEFAULT_WIDTH = 296;
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

function mergeNotificationsById(
  currentNotifications: UserNotification[],
  incomingNotifications: UserNotification[],
): UserNotification[] {
  if (incomingNotifications.length === 0) {
    return currentNotifications;
  }

  const notificationsById = new Map(
    currentNotifications.map((notification) => [notification.id, notification]),
  );

  for (const notification of incomingNotifications) {
    notificationsById.set(notification.id, notification);
  }

  return Array.from(notificationsById.values())
    .sort(
      (left, right) =>
        new Date(right.createdAt ?? right.createdDateUtc).getTime() -
        new Date(left.createdAt ?? left.createdDateUtc).getTime(),
    )
    .slice(0, 20);
}

const APP_VIEW_LABELS: Record<AppView, string> = {
  dashboard: "Dashboard",
  tickets: "Tickets",
  archived: "Archived Tickets",
  reports: "Reports",
  sla: "Configuration",
  jobs: "Jobs",
  users: "Users",
};

type NavigationGroup = "workspace" | "admin";

type AppView =
  | "dashboard"
  | "tickets"
  | "archived"
  | "reports"
  | "sla"
  | "jobs"
  | "users";
type ReportSection = "sla" | "online-users" | "custom";
type SessionPromptState = "warning" | "expired" | null;
type NavigationItem = {
  view: AppView;
  group: NavigationGroup;
  label: string;
  description: string;
};

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
const NAVIGATION_ITEM_DEFINITIONS: ReadonlyArray<NavigationItem> = [
  {
    view: "dashboard",
    group: "workspace",
    label: "Dashboard",
    description: "See queue health and quick operational summaries.",
  },
  {
    view: "tickets",
    group: "workspace",
    label: "Tickets",
    description: "Browse and manage the active ticket queue.",
  },
  {
    view: "archived",
    group: "workspace",
    label: "Archived Tickets",
    description: "Review tickets moved out of the active queue.",
  },
  {
    view: "reports",
    group: "workspace",
    label: "Reports",
    description: "Drill into SLA trends and detailed reporting.",
  },
  {
    view: "jobs",
    group: "workspace",
    label: "Job Activity",
    description: "Monitor system automation runs and latest outcomes.",
  },
  {
    view: "sla",
    group: "admin",
    label: "Configuration",
    description: "Configure system setup and policy definitions.",
  },
  {
    view: "users",
    group: "admin",
    label: "Users",
    description: "Manage the registered user directory.",
  },
];

function clampSidebarWidth(width: number) {
  const maxWidth =
    typeof window === "undefined"
      ? SIDEBAR_MAX_WIDTH
      : Math.min(
          SIDEBAR_MAX_WIDTH,
          Math.max(320, Math.floor(window.innerWidth * 0.24)),
        );

  return Math.min(maxWidth, Math.max(SIDEBAR_MIN_WIDTH, width));
}

function useStableCallback<Args extends unknown[], ReturnValue>(
  callback: (...args: Args) => ReturnValue,
) {
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  return useCallback((...args: Args) => callbackRef.current(...args), []);
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

  const storedValue = Number(
    window.localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY),
  );
  if (Number.isNaN(storedValue)) {
    return SIDEBAR_DEFAULT_WIDTH;
  }

  return clampSidebarWidth(storedValue);
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
  return await userService.getCurrentUser(token).catch((error) => {
    console.warn("Current user profile could not be loaded", error);
    return null;
  });
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
  const [selectedCustomReportId, setSelectedCustomReportId] = useState<
    number | null
  >(null);
  const [latestRealtimeEvent, setLatestRealtimeEvent] =
    useState<RealtimeEvent | null>(null);
  const [realtimeStatus, setRealtimeStatus] = useState<
    "connected" | "reconnecting" | "offline"
  >("offline");
  const [realtimeReconnectKey, setRealtimeReconnectKey] = useState(0);
  const [isStatusTooltipVisible, setIsStatusTooltipVisible] = useState(false);

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
  const [notificationsError, setNotificationsError] = useState<string | null>(
    null,
  );
  const [notificationUnreadCount, setNotificationUnreadCount] = useState(0);
  const [isNotificationPanelOpen, setIsNotificationPanelOpen] = useState(false);
  const [markingNotificationId, setMarkingNotificationId] = useState<
    number | null
  >(null);
  const [markingAllNotificationsRead, setMarkingAllNotificationsRead] =
    useState(false);
  // User management state is owned by useUsers (wired below after getApiToken).
  const [isAppMenuOpen, setIsAppMenuOpen] = useState(false);
  const [sidebarWidth, setSidebarWidth] = useState(getInitialSidebarWidth);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileDraft, setProfileDraft] = useState<UpdateUserProfileInput>({});
  const appMenuRef = useRef<HTMLDivElement | null>(null);
  const userMenuRef = useRef<HTMLDivElement | null>(null);
  const notificationPanelRef = useRef<HTMLDivElement | null>(null);
  const statusTooltipShowTimerRef = useRef<number | null>(null);
  const statusTooltipHideTimerRef = useRef<number | null>(null);
  const lastKnownAuthRolesRef = useRef<string[]>([]);
  const sessionPromptStateRef = useRef<SessionPromptState>(null);
  const sessionLastActivityAtRef = useRef(Date.now());
  const lastPresenceSyncAtRef = useRef(0);
  const presenceSyncInFlightRef = useRef(false);
  const recoveryProbeInFlightRef = useRef(false);

  const authRoles = useMemo(
    () => normalizeRoles(currentUser?.roles, currentUser?.role),
    [currentUser?.roles, currentUser?.role],
  );
  const effectiveAuthRoles =
    currentUser === null ? lastKnownAuthRolesRef.current : authRoles;

  useEffect(() => {
    if (authRoles.length > 0) {
      lastKnownAuthRolesRef.current = authRoles;
    }
  }, [authRoles]);

  const isAdmin = useMemo(
    () => checkIsAdmin(effectiveAuthRoles),
    [effectiveAuthRoles],
  );
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
    () =>
      `${SESSION_LAST_ACTIVITY_STORAGE_KEY_PREFIX}:${sessionStorageIdentity}`,
    [sessionStorageIdentity],
  );
  const sessionReauthPendingStorageKey = useMemo(
    () =>
      `${SESSION_REAUTH_PENDING_STORAGE_KEY_PREFIX}:${sessionStorageIdentity}`,
    [sessionStorageIdentity],
  );

  const sessionUnlocked = bootstrapComplete && !needsConsent && isAuthenticated;

  const canCreateTicketsCap =
    sessionUnlocked && canCreateTickets(effectiveAuthRoles);
  const canEditTicketsCap = sessionUnlocked && canEditTickets(effectiveAuthRoles);
  const canViewTicketSections = sessionUnlocked;
  const canViewDashboard = canViewTicketSections;
  const canViewReportsNav = sessionUnlocked && canViewReports(effectiveAuthRoles);
  const canViewOnlineUsersReport = canViewReportsNav;
  const canManageCustomReportDefinitions =
    sessionUnlocked && canManageReportDefinitions(effectiveAuthRoles);
  const canViewArchived = canViewTicketSections;
  const canManageConfiguration =
    sessionUnlocked && canAccessConfig(effectiveAuthRoles);
  const canManageJobsNav = sessionUnlocked && canManageJobs(effectiveAuthRoles);
  const canViewJobActivityNav =
    sessionUnlocked && canViewJobActivity(effectiveAuthRoles);
  const canViewUsers = sessionUnlocked && canManageUsers(effectiveAuthRoles);
  const canCreateUsers = canViewUsers;
  const canEditUsers = sessionUnlocked && canManageUsers(effectiveAuthRoles);
  const canDeleteUsers = sessionUnlocked && canManageUsers(effectiveAuthRoles);
  // failedJobsCount is computed after useConfiguration (below) because it depends on jobs.
  const activeViewLabel = APP_VIEW_LABELS[activeView];

  const navigationItems = useMemo(() => {
    const isEnabledByView: Record<AppView, boolean> = {
      dashboard: canViewDashboard,
      tickets: canViewTicketSections,
      archived: canViewArchived,
      reports: canViewReportsNav,
      jobs: canViewJobActivityNav,
      sla: canManageConfiguration,
      users: canViewUsers,
    };

    return NAVIGATION_ITEM_DEFINITIONS.filter(
      (item) => isEnabledByView[item.view],
    );
  }, [
    canViewDashboard,
    canViewTicketSections,
    canViewArchived,
    canViewReportsNav,
    canViewJobActivityNav,
    canManageConfiguration,
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
      (view === "jobs" && canViewJobActivityNav) ||
      (view === "sla" && canManageConfiguration) ||
      (view === "users" && canViewUsers),
    [canManageConfiguration, canViewJobActivityNav, canViewReportsNav, canViewUsers],
  );

  const getFallbackView = useCallback((): AppView => {
    if (canViewTicketSections) {
      return "tickets";
    }
    if (canViewReportsNav) {
      return "reports";
    }
    if (canViewJobActivityNav) {
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
    canViewJobActivityNav,
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
    boardCountsById,
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
    archivedSearchQuery,
    setArchivedSearchQuery,
    archivedTicketsForView,
    setArchivedTickets,
    archivedLoading,
    archivedLoadingMore,
    archivedHasMore,
    archivedError,
    highlightedArchivedTicketId,
    setHighlightedArchivedTicketId,
    reactivatingArchivedTicketId,
    ticketToDelete,
    setTicketToDelete,
    deleting,
    refreshTicketsSilently,
    syncTicketChangesSilently,
    reconcileTicketByIdSilently,
    upsertActiveTicketLocally,
    applyArchivedTicketLocally,
    removeTicketLocally,
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

  const ticketModalOpenRef = useRef(false);
  const selectedTicketIdRef = useRef<string | undefined>(undefined);
  const currentUserRef = useRef(currentUser);
  const loadNotificationsRef = useRef<
    (token?: string, options?: { silent?: boolean }) => Promise<void>
  >(async () => {});
  const loadAllTicketsRef = useRef(loadAllTickets);
  const upsertActiveTicketLocallyRef = useRef(upsertActiveTicketLocally);
  const applyArchivedTicketLocallyRef = useRef(applyArchivedTicketLocally);
  const removeTicketLocallyRef = useRef(removeTicketLocally);
  const reconcileTicketByIdSilentlyRef = useRef(reconcileTicketByIdSilently);
  ticketModalOpenRef.current = isModalOpen;
  selectedTicketIdRef.current = selectedTicket?.id;

  const {
    savedFilters,
    selectedSavedFilterId,
    setSelectedSavedFilterId,
    isSaveFilterModalOpen,
    savedFilterName,
    setSavedFilterName,
    handleFilterChange,
    handleFilterValueChange,
    handleSearchChange,
    handlePageSizeChange,
    openSaveFilterModal,
    closeSaveFilterModal,
    saveCurrentFilter,
    applySavedFilter,
    clearTicketFilters,
    deleteSavedFilter,
  } = useSavedFilters({
    storageKey: savedFilterStorageKey,
    filter,
    filterValue,
    searchQuery,
    pageSize,
    setFilter,
    setFilterValue,
    setSearchQuery,
    setPageSize,
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
          setNotificationsError(
            "You do not have permission to view notifications.",
          );
        } else {
          setApiUnavailable(false);
          setNotificationsError("Unable to load notifications.");
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
    roleDefinitions,
    selectedRoleDefinition,
    rolePermissionOptions,
    roleDefinitionLoading,
    roleDefinitionSaving,
    roleDefinitionDeletingId,
    roleDefinitionError,
    loadRoleDefinitions,
    handleRoleDefinitionChange,
    createRoleDefinition,
    selectRoleDefinition,
    saveRoleDefinition,
    deleteRoleDefinition,
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
    usersSearchQuery,
    setUsersSearchQuery,
    usersVisible,
    usersHasMore,
    loadMoreUsers,
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


  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    sessionPromptStateRef.current = sessionPromptState;
  }, [sessionPromptState]);

  useEffect(() => {
    currentUserRef.current = currentUser;
  }, [currentUser]);

  useEffect(() => {
    loadNotificationsRef.current = loadNotifications;
  }, [loadNotifications]);

  useEffect(() => {
    loadAllTicketsRef.current = loadAllTickets;
  }, [loadAllTickets]);

  useEffect(() => {
    upsertActiveTicketLocallyRef.current = upsertActiveTicketLocally;
  }, [upsertActiveTicketLocally]);

  useEffect(() => {
    applyArchivedTicketLocallyRef.current = applyArchivedTicketLocally;
  }, [applyArchivedTicketLocally]);

  useEffect(() => {
    removeTicketLocallyRef.current = removeTicketLocally;
  }, [removeTicketLocally]);

  useEffect(() => {
    reconcileTicketByIdSilentlyRef.current = reconcileTicketByIdSilently;
  }, [reconcileTicketByIdSilently]);

  useEffect(() => {
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
    if (
      !isAuthenticated ||
      !bootstrapComplete ||
      needsConsent ||
      !canViewTicketSections
    ) {
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
          if (typeof event.unreadCount === "number") {
            setNotificationUnreadCount(event.unreadCount);
          }

          if (event.notifications && event.notifications.length > 0) {
            const assignmentNotifications = event.notifications.filter(
              (notification) =>
                notification.type === "assignment" ||
                notification.eventType === "ticket.assignment",
            );
            const commentNotifications = event.notifications.filter(
              (notification) =>
                notification.type === "comment" ||
                notification.eventType === "ticket.comment",
            );
            for (const notification of assignmentNotifications) {
              toast.success(notification.title, {
                id: `assignment-notification-${notification.id}`,
              });
            }
            for (const notification of commentNotifications) {
              toast(notification.title, {
                id: `comment-notification-${notification.id}`,
              });
            }
            setNotifications((currentNotifications) =>
              mergeNotificationsById(currentNotifications, event.notifications ?? []),
            );
            setNotificationsLoaded(true);
            setNotificationsError(null);
          } else {
            void loadNotificationsRef.current(undefined, { silent: true });
          }
          return;
        }

        if (
          (event.eventType === "ticket.created" ||
            event.eventType === "ticket.updated" ||
            event.eventType === "ticket.reactivated") &&
          event.ticket
        ) {
          upsertActiveTicketLocallyRef.current(event.ticket);
          return;
        }

        if (event.eventType === "ticket.routed") {
          // Ticket payload is already applied via ticket.created / ticket.updated; suppress noisy routing toasts.
          return;
        }

        if (event.eventType === "ticket.archived" && event.archivedTicket) {
          applyArchivedTicketLocallyRef.current(event.archivedTicket);
          return;
        }

        if (
          event.eventType === "ticket.deleted" &&
          typeof event.ticketId === "string" &&
          event.ticketId.trim().length > 0
        ) {
          const deletedId = event.ticketId.trim();
          const hadTicketOpenInModal =
            ticketModalOpenRef.current &&
            selectedTicketIdRef.current === deletedId;
          removeTicketLocallyRef.current(deletedId);
          if (hadTicketOpenInModal) {
            toast("This ticket was deleted.", {
              id: `ticket-deleted-${deletedId}`,
            });
          }
          return;
        }

        if (
          event.eventType === "ticket.removed" &&
          typeof event.ticketId === "string" &&
          event.ticketId.trim().length > 0
        ) {
          removeTicketLocallyRef.current(event.ticketId);
          return;
        }

        if (
          event.eventType.startsWith("ticket.") &&
          typeof event.ticketId === "string" &&
          event.ticketId.trim().length > 0
        ) {
          void reconcileTicketByIdSilentlyRef.current(event.ticketId);
        }
      },
      onError: (error) => {
        console.error("Realtime connection issue", error);
      },
      onStatusChange: (status) => {
        setRealtimeStatus(status);
      },
    });

    return () => {
      connection.close();
    };
  }, [
    canViewTicketSections,
    getApiToken,
    isAuthenticated,
    needsConsent,
    bootstrapComplete,
    realtimeReconnectKey,
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

  const recoverAfterBackendAvailable = useCallback(
    async (token: string) => {
      if (canViewTicketSections) {
        await loadAllTicketsRef.current(token);
      }

      void loadTicketBoards(token);
      void loadSessionConfiguration(token);
      void loadNotifications(token, { silent: true });

      if (activeView === "archived") {
        await loadArchivedTickets(token, { fullCatalog: true });
      } else if (activeView === "users" && canViewUsers) {
        await loadUsers(token);
      } else if (activeView === "jobs" && canViewJobActivityNav) {
        await loadJobs(token);
      } else if (activeView === "reports" && canViewReportsNav) {
        void loadOnlineUsers(token);
        void loadCustomReports(token);
      }
    },
    [
      activeView,
      canViewJobActivityNav,
      canViewReportsNav,
      canViewTicketSections,
      canViewUsers,
      loadAllTickets,
      loadArchivedTickets,
      loadCustomReports,
      loadJobs,
      loadNotifications,
      loadOnlineUsers,
      loadSessionConfiguration,
      loadTicketBoards,
      loadUsers,
    ],
  );

  useEffect(() => {
    if (
      !apiUnavailable ||
      !isAuthenticated ||
      isLoading ||
      needsConsent ||
      !bootstrapComplete
    ) {
      return;
    }

    let stopped = false;
    let timerId: number | null = null;
    let attempts = 0;

    const scheduleNextProbe = (delayMs: number) => {
      if (stopped) {
        return;
      }
      timerId = window.setTimeout(() => {
        void probeRecovery();
      }, delayMs);
    };

    const probeRecovery = async () => {
      if (stopped || recoveryProbeInFlightRef.current) {
        return;
      }

      recoveryProbeInFlightRef.current = true;
      attempts += 1;
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: API_AUTHORIZATION_PARAMS,
        });
        await loadBootstrapCurrentUser(token);
        await recoverAfterBackendAvailable(token);

        if (stopped) {
          return;
        }

        setApiUnavailable(false);
        setError(null);
        if (realtimeStatus !== "connected") {
          setRealtimeReconnectKey((current) => current + 1);
        }
      } catch (recoveryError) {
        if (stopped) {
          return;
        }

        if (isForbiddenError(recoveryError)) {
          setApiUnavailable(false);
          setError("You do not have permission to view tickets.");
          return;
        }

        if (!isLikelyNetworkError(recoveryError)) {
          console.warn("Backend recovery probe failed", recoveryError);
        }

        const delayMs = Math.min(30000, 2000 * 2 ** Math.min(attempts, 4));
        scheduleNextProbe(delayMs);
      } finally {
        recoveryProbeInFlightRef.current = false;
      }
    };

    scheduleNextProbe(1500);

    return () => {
      stopped = true;
      if (timerId !== null) {
        window.clearTimeout(timerId);
      }
    };
  }, [
    apiUnavailable,
    bootstrapComplete,
    getAccessTokenSilently,
    isAuthenticated,
    isLoading,
    needsConsent,
    realtimeStatus,
    recoverAfterBackendAvailable,
  ]);

  useEffect(() => {
    return () => {
      if (statusTooltipShowTimerRef.current !== null) {
        window.clearTimeout(statusTooltipShowTimerRef.current);
      }
      if (statusTooltipHideTimerRef.current !== null) {
        window.clearTimeout(statusTooltipHideTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (
      !isAuthenticated ||
      isLoading ||
      isAccountExpired ||
      isAccountInactive
    ) {
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
    const remainingSeconds = Math.max(
      0,
      sessionTimeoutSeconds - elapsedSeconds,
    );
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
      const remainingSeconds = Math.max(
        0,
        sessionTimeoutSeconds - elapsedSeconds,
      );

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
    if (
      !isAuthenticated ||
      isLoading ||
      isAccountExpired ||
      isAccountInactive
    ) {
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
    loadTicketBoards,
    loadNotifications,
    loadSessionConfiguration,
  ]);

  useEffect(() => {
    if (!isAuthenticated || ticketBoards.length > 0 || ticketBoardLoading) {
      return;
    }

    void loadTicketBoards();
  }, [
    isAuthenticated,
    loadTicketBoards,
    ticketBoardLoading,
    ticketBoards.length,
  ]);

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
        roleDefinitions.length > 0 &&
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

    if (roleDefinitions.length === 0) {
      void loadRoleDefinitions();
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
    loadRoleDefinitions,
    loadTicketStatuses,
    loadSlaConfigurations,
    notificationChannelsLoadedOnce,
    sessionLoadedOnce,
    slaConfigurations.length,
    storedProcedures.length,
    roleDefinitions.length,
    ticketRoutingLoadedOnce,
    ticketStatuses.length,
  ]);

  useEffect(() => {
    if (activeView !== "jobs" || !canViewJobActivityNav) {
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
    canViewJobActivityNav,
    jobsLoaded,
    loadJobs,
    loadStoredProcedures,
    storedProcedures.length,
  ]);

  useEffect(() => {
    if (!canViewJobActivityNav || jobsLoaded || jobsLoading) {
      return;
    }

    void loadJobs();
  }, [canViewJobActivityNav, jobsLoaded, jobsLoading, loadJobs]);

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
  }, [
    activeView,
    archivedTickets.length,
    canViewArchived,
    loadArchivedTickets,
  ]);

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
  }, [
    activeReportSection,
    activeView,
    runCustomReport,
    selectedCustomReportId,
  ]);

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

  const grantConsent = useStableCallback(async () => {
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
        setError("Unable to grant CORTEX API access.");
        toast.error(
          getUserFacingErrorMessage(error, "Unable to grant CORTEX API access"),
        );
      }
    } finally {
      setLoading(false);
    }
  });

  const availableTicketBoards = useMemo(
    () => (ticketBoards.length > 0 ? ticketBoards : [...DEFAULT_TICKET_BOARDS]),
    [ticketBoards],
  );

  const boardTabs = useMemo(() => {
    return availableTicketBoards.filter((board) => board.isEnabled);
  }, [availableTicketBoards]);

  useEffect(() => {
    if (selectedBoardId === "all") {
      return;
    }

    if (!boardTabs.some((board) => board.id === selectedBoardId)) {
      setSelectedBoardId("all");
    }
  }, [boardTabs, selectedBoardId, setSelectedBoardId]);

  const handleViewChange = useStableCallback((view: AppView) => {
    if (!isViewAllowed(view)) {
      toast.error("You do not have access to this section.");
      return;
    }

    setActiveView(view);
    setIsAppMenuOpen(false);
    setIsNotificationPanelOpen(false);
  });

  const handleSidebarResize = useCallback((nextWidth: number) => {
    setSidebarWidth(clampSidebarWidth(nextWidth));
  }, []);

  const handleRefreshTickets = useStableCallback(() => {
    void loadAllTickets();
  });

  const handleCreateTicket = useStableCallback(() => {
    openTicket(
      createDraftTicket(
        ticketStatuses,
        ticketBoards.length > 0 ? ticketBoards : [...DEFAULT_TICKET_BOARDS],
        currentUser?.displayName ?? user?.name ?? "",
        currentUser?.department ?? "",
      ),
    );
  });

  const openFailedJobsQueue = useStableCallback(() => {
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
  });

  const markNotificationAsRead = useStableCallback(async (
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
    setNotificationUnreadCount((currentCount) => Math.max(0, currentCount - 1));
  });

  const markAllNotificationsRead = useStableCallback(async () => {
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
        getUserFacingErrorMessage(
          error,
          "Unable to mark notifications as read.",
        ),
      );
      toast.error("Unable to mark notifications as read");
    } finally {
      setMarkingAllNotificationsRead(false);
    }
  });

  const openNotification = useStableCallback(async (notification: UserNotification) => {
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
        setSelectedBoardId("all");
        setActiveView("archived");
        await loadArchivedTickets(token, { fullCatalog: true });

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
      setNotificationsError(
        getUserFacingErrorMessage(error, "Unable to open notification."),
      );
      toast.error("Unable to open notification");
    } finally {
      setMarkingNotificationId(null);
    }
  });

  const toggleThemeFromMenu = useStableCallback(() => {
    setTheme((currentTheme) => (currentTheme === "dark" ? "light" : "dark"));
    setIsUserMenuOpen(false);
  });

  const toggleAppMenu = useStableCallback(() => {
    setIsAppMenuOpen((current) => {
      const next = !current;
      if (next) {
        setIsUserMenuOpen(false);
        setIsNotificationPanelOpen(false);
      }
      return next;
    });
  });

  const toggleUserMenu = useStableCallback(() => {
    setIsUserMenuOpen((current) => {
      const next = !current;
      if (next) {
        setIsAppMenuOpen(false);
        setIsNotificationPanelOpen(false);
      }
      return next;
    });
  });

  const toggleNotificationPanel = useStableCallback(() => {
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
  });

  const openProfileModal = useStableCallback(async () => {
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
      toast.error(getUserFacingErrorMessage(error, "Unable to load profile"));
    } finally {
      setProfileLoading(false);
    }
  });

  const handleRefreshNotifications = useStableCallback(() => {
    void loadNotifications();
  });

  const handleRefreshSession = useStableCallback(() => {
    void forceSessionRefreshForAuthChanges();
  });

  const handleStatusMouseEnter = useStableCallback(() => {
    if (statusTooltipHideTimerRef.current !== null) {
      window.clearTimeout(statusTooltipHideTimerRef.current);
      statusTooltipHideTimerRef.current = null;
    }
    if (statusTooltipShowTimerRef.current !== null) {
      window.clearTimeout(statusTooltipShowTimerRef.current);
    }
    statusTooltipShowTimerRef.current = window.setTimeout(() => {
      setIsStatusTooltipVisible(true);
      statusTooltipShowTimerRef.current = null;
    }, 25);
  });

  const handleStatusMouseLeave = useStableCallback(() => {
    if (statusTooltipShowTimerRef.current !== null) {
      window.clearTimeout(statusTooltipShowTimerRef.current);
      statusTooltipShowTimerRef.current = null;
    }
    if (statusTooltipHideTimerRef.current !== null) {
      window.clearTimeout(statusTooltipHideTimerRef.current);
    }
    statusTooltipHideTimerRef.current = window.setTimeout(() => {
      setIsStatusTooltipVisible(false);
      statusTooltipHideTimerRef.current = null;
    }, 80);
  });

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
      userService.clearDirectoryCache();
      setIsProfileModalOpen(false);
      toast.success("Profile updated");
    } catch (error) {
      console.error("Failed to update profile", error);
      toast.error(getUserFacingErrorMessage(error, "Unable to update profile"));
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
          <h1 className="mb-4 text-3xl font-bold">
            Your account has been expired
          </h1>
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
      <AppHeader
        activeView={activeView}
        activeViewLabel={activeViewLabel}
        activeNavigationDescription={activeNavigationItem?.description ?? null}
        navigationItems={navigationItems}
        realtimeStatus={realtimeStatus}
        isStatusTooltipVisible={isStatusTooltipVisible}
        onStatusMouseEnter={handleStatusMouseEnter}
        onStatusMouseLeave={handleStatusMouseLeave}
        appMenuRef={appMenuRef}
        isAppMenuOpen={isAppMenuOpen}
        onToggleAppMenu={toggleAppMenu}
        onViewChange={handleViewChange}
        showTicketActions={activeView === "tickets"}
        canCreateTickets={canCreateTicketsCap}
        onRefreshTickets={handleRefreshTickets}
        onCreateTicket={handleCreateTicket}
        bootstrapComplete={bootstrapComplete}
        needsConsent={needsConsent}
        onGrantConsent={grantConsent}
        sessionRefreshNotice={sessionRefreshNotice}
        sessionRefreshInProgress={sessionRefreshInProgress}
        onRefreshSession={handleRefreshSession}
        userMenuRef={userMenuRef}
        notificationPanelRef={notificationPanelRef}
        isNotificationPanelOpen={isNotificationPanelOpen}
        notificationUnreadCount={notificationUnreadCount}
        notifications={notifications}
        notificationsLoading={notificationsLoading}
        notificationsError={notificationsError}
        markingAllNotificationsRead={markingAllNotificationsRead}
        markingNotificationId={markingNotificationId}
        onToggleNotificationPanel={toggleNotificationPanel}
        onRefreshNotifications={handleRefreshNotifications}
        onMarkAllNotificationsRead={markAllNotificationsRead}
        onOpenNotification={openNotification}
        canManageJobs={canManageJobsNav}
        failedJobsCount={failedJobsCount}
        onOpenFailedJobsQueue={openFailedJobsQueue}
        isUserMenuOpen={isUserMenuOpen}
        onToggleUserMenu={toggleUserMenu}
        currentUser={currentUser}
        authDisplayName={user?.name}
        authEmail={user?.email}
        profileLoading={profileLoading}
        onOpenProfileModal={openProfileModal}
        isDarkMode={isDarkMode}
        onToggleThemeFromMenu={toggleThemeFromMenu}
        onLogout={performLogout}
      />

      <div className="mx-auto flex w-full max-w-[2200px] flex-1 px-4 py-6 sm:px-6 sm:py-8 lg:gap-8 2xl:px-8">
        <AppSidebar
          width={sidebarWidth}
          activeView={activeView}
          navigationItems={navigationItems}
          onViewChange={handleViewChange}
          onResize={handleSidebarResize}
        />

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
            <TicketsContainer
              theme={theme}
              isAuthenticated={isAuthenticated}
              bootstrapComplete={bootstrapComplete}
              needsConsent={needsConsent}
              canViewTicketSections={canViewTicketSections}
              boardTabs={boardTabs}
              boardCountsById={boardCountsById}
              loading={loading}
              apiUnavailable={apiUnavailable}
              error={error}
              savedFilters={savedFilters}
              selectedSavedFilterId={selectedSavedFilterId}
              setSelectedSavedFilterId={setSelectedSavedFilterId}
              openSaveFilterModal={openSaveFilterModal}
              deleteSavedFilter={deleteSavedFilter}
              clearTicketFilters={clearTicketFilters}
              applySavedFilter={applySavedFilter}
              handleFilterChange={handleFilterChange}
              handleFilterValueChange={handleFilterValueChange}
              handleSearchChange={handleSearchChange}
              handlePageSizeChange={handlePageSizeChange}
              filter={filter}
              filterValue={filterValue}
              searchQuery={searchQuery}
              pageSize={pageSize}
              selectedBoardId={selectedBoardId}
              setSelectedBoardId={setSelectedBoardId}
              myTicketsOnly={myTicketsOnly}
              setMyTicketsOnly={setMyTicketsOnly}
              ticketListSort={ticketListSort}
              setTicketListSort={setTicketListSort}
              tickets={tickets}
              pagedTickets={pagedTickets}
              totalTickets={totalTickets}
              totalPages={totalPages}
              currentPage={currentPage}
              setCurrentPage={setCurrentPage}
              showingStart={showingStart}
              showingEnd={showingEnd}
              isModalOpen={isModalOpen}
              syncTicketChangesSilently={syncTicketChangesSilently}
              openTicket={openTicket}
            />
          ) : activeView === "archived" && canViewArchived ? (
            <ArchivedTicketsPage
              tickets={archivedTicketsForView}
              totalTickets={archivedTicketsForView.length}
              searchQuery={archivedSearchQuery}
              onSearchQueryChange={setArchivedSearchQuery}
              loading={archivedLoading || apiUnavailable}
              error={apiUnavailable ? null : archivedError}
              highlightedTicketId={highlightedArchivedTicketId}
              onRefresh={() => void loadArchivedTickets()}
              onLoadMore={() => void loadArchivedTickets(undefined, { append: true })}
              hasMore={archivedHasMore}
              loadingMore={archivedLoadingMore}
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
              customReportError={
                apiUnavailable ? null : customReportResultError
              }
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
          ) : activeView === "jobs" && canViewJobActivityNav ? (
            <JobsPage
              jobs={jobs}
              loading={jobsLoading}
              error={jobsError}
              runningJobId={runningJobId}
              canViewSensitiveDetails={canManageJobsNav}
              canRetryNow={canManageJobsNav}
              onRefresh={() => void loadJobs()}
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
                notificationChannelConfiguration={
                  notificationChannelConfiguration
                }
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
                roleDefinitions={roleDefinitions}
                selectedRoleDefinition={selectedRoleDefinition}
                rolePermissionOptions={rolePermissionOptions}
                roleDefinitionError={roleDefinitionError}
                roleDefinitionLoading={roleDefinitionLoading}
                roleDefinitionSaving={roleDefinitionSaving}
                roleDefinitionDeletingId={roleDefinitionDeletingId}
                onRefreshRoleDefinitions={() => void loadRoleDefinitions()}
                onCreateRoleDefinition={createRoleDefinition}
                onSelectRoleDefinition={selectRoleDefinition}
                onRoleDefinitionChange={handleRoleDefinitionChange}
                onSaveRoleDefinition={saveRoleDefinition}
                onDeleteRoleDefinition={deleteRoleDefinition}
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
                onRefreshCustomReports={() =>
                  void loadCustomReportDefinitions()
                }
                onCreateCustomReport={createCustomReport}
                onUpdateCustomReport={updateCustomReport}
                onDeleteCustomReport={deleteCustomReport}
                storedProcedures={storedProcedures}
                databaseStoredProcedures={databaseStoredProcedures}
                databaseStoredProceduresLoading={
                  databaseStoredProceduresLoading
                }
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
                jobs={jobs}
                jobsLoading={jobsLoading}
                jobsError={jobsError}
                jobsSaving={jobsSaving}
                runningJobId={runningJobId}
                onRefreshJobs={() => void loadJobs()}
                onCreateScheduledJob={createScheduledJob}
                onUpdateScheduledJob={updateScheduledJob}
                onRunScheduledJobNow={runScheduledJobNow}
                canManageReportDefinitions={canManageCustomReportDefinitions}
                onOpenJobs={() => setActiveView("jobs")}
                onOpenUsers={() => setActiveView("users")}
              />
            )
          ) : activeView === "users" && canViewUsers ? (
            <UsersPage
              users={usersVisible}
              totalUsers={users.length}
              searchQuery={usersSearchQuery}
              onSearchQueryChange={setUsersSearchQuery}
              hasMore={usersHasMore}
              loadingMore={false}
              onLoadMore={loadMoreUsers}
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
            (!selectedTicket.id
              ? (currentUser?.displayName ?? user?.name ?? "")
              : "")
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
        inactivityTimeoutMinutes={
          effectiveSessionConfiguration.inactivityTimeoutMinutes
        }
        onContinue={continueSessionAfterWarning}
        onReauthenticate={reauthenticateDueToInactivity}
      />
    </div>
  );
}

export default App;
