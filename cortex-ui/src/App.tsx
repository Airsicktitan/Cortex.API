import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { Ticket } from "./types/ticket";
import type { SlaConfiguration } from "./types/sla";
import type {
  AdminUpdateUserInput,
  UpdateUserProfileInput,
  UserProfile,
  UserRecord,
} from "./types/user";
import { ApiError, ticketService, userService } from "./services/api";
import { slaService } from "./services/slaService";
import TicketCard from "./components/TicketCard";
import TicketModal from "./components/TicketModal";
import ConfirmDeleteModal from "./components/ConfirmDeleteModal";
import SlaLegend from "./components/SlaLegend";
import SlaSettingsPage from "./components/SlaSettingsPage";
import UsersPage from "./components/UsersPage";
import UserProfileModal from "./components/UserProfileModal";
import AdminUserEditModal from "./components/AdminUserEditModal";
import { applyTheme, getPreferredTheme, type ThemeMode } from "./theme";
import toast from "react-hot-toast";

const API_AUDIENCE = "https://cortex-api";
const ADMIN_PERMISSION = "admin:system";
const TICKETS_CREATE_PERMISSION = "tickets:create";
const API_AUTHORIZATION_PARAMS = {
  audience: API_AUDIENCE,
} as const;

type Permission =
  | typeof ADMIN_PERMISSION
  | "tickets:read"
  | typeof TICKETS_CREATE_PERMISSION;
type FilterOption = "all" | "status" | "priority" | "sla";
type AppView = "tickets" | "sla" | "users";
type PageSizeOption = 10 | 25 | 50 | "all";

const SLA_FILTER_OPTIONS = ["Breached", "At Risk", "Met"] as const;
const PAGE_SIZE_OPTIONS: ReadonlyArray<PageSizeOption> = [10, 25, 50, "all"];

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

function createDraftTicket(): Ticket {
  const createdDate = new Date().toISOString();
  const targetDate = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();

  return {
    id: "",
    title: "",
    description: "",
    priority: "Medium",
    status: "New",
    createdDate,
    slaTargetDate: targetDate,
    slaStatus: "On Track",
    slaRemainingMinutes: 24 * 60,
    isSlaBreached: false,
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
  const [allTickets, setAllTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);

  const [filter, setFilter] = useState<FilterOption>("all");
  const [filterValue, setFilterValue] = useState("");
  const debouncedFilterValue = useDebouncedValue(filterValue, 300);
  const [pageSize, setPageSize] = useState<PageSizeOption>(10);
  const [currentPage, setCurrentPage] = useState(1);

  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

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
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [editingAdminUser, setEditingAdminUser] = useState<UserRecord | null>(
    null,
  );
  const [adminUserDraft, setAdminUserDraft] = useState<AdminUpdateUserInput>({});
  const [adminUserSaving, setAdminUserSaving] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileDraft, setProfileDraft] = useState<UpdateUserProfileInput>({});
  const userMenuRef = useRef<HTMLDivElement | null>(null);

  const permissionSet = useMemo(() => new Set(permissions), [permissions]);
  const isAdmin = permissionSet.has(ADMIN_PERMISSION);
  const isDarkMode = theme === "dark";
  const isAccountExpired = isUserExpired(currentUser);
  const isAccountInactive = isUserInactive(currentUser);

  const hasPermission = (permission: Permission) => {
    return isAdmin || permissionSet.has(permission);
  };

  const canCreateTickets =
    permissionsLoaded &&
    !needsConsent &&
    hasPermission(TICKETS_CREATE_PERMISSION);
  const canManageSla = permissionsLoaded && !needsConsent && isAdmin;
  const canViewUsers = permissionsLoaded && !needsConsent && isAdmin;

  const getApiToken = useCallback(async () => {
    return await getAccessTokenSilently({
      authorizationParams: API_AUTHORIZATION_PARAMS,
    });
  }, [getAccessTokenSilently]);

  const refreshTicketsSilently = async (providedToken?: string) => {
    try {
      const token = providedToken ?? (await getApiToken());
      const data = await ticketService.getAll(token);
      setAllTickets(data);
    } catch (error) {
      console.error("Failed to refresh tickets silently", error);
    }
  };

  const loadAllTickets = async (providedToken?: string) => {
    setLoading(true);
    setError(null);

    try {
      const token = providedToken ?? (await getApiToken());
      const data = await ticketService.getAll(token);
      setAllTickets(data);
      setNeedsConsent(false);
    } catch (error) {
      console.error("Failed to load tickets", error);

      if (isConsentRequiredError(error)) {
        setNeedsConsent(true);
        setError("CORTEX API consent is required before tickets can load.");
      } else if (isForbiddenError(error)) {
        setError("You do not have permission to view tickets.");
      } else {
        setError("Failed to load tickets. Make sure the API is running.");
      }
    } finally {
      setLoading(false);
    }
  };

  const loadSlaConfigurations = useCallback(
    async (providedToken?: string) => {
      setSlaLoading(true);
      setSlaError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await slaService.getAll(token);
        setSlaConfigurations(data);
      } catch (error) {
        console.error("Failed to load SLA settings", error);

        if (isForbiddenError(error)) {
          setSlaError("You do not have permission to manage SLA settings.");
        } else {
          setSlaError("Failed to load SLA settings.");
        }
      } finally {
        setSlaLoading(false);
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
      } catch (error) {
        console.error("Failed to load users", error);

        if (isForbiddenError(error)) {
          setUsersError("You do not have permission to view users.");
        } else {
          setUsersError("Failed to load users.");
        }
      } finally {
        setUsersLoading(false);
      }
    },
    [getApiToken],
  );

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    if (!isUserMenuOpen) return;

    const handlePointerDown = (event: MouseEvent) => {
      if (!userMenuRef.current?.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handlePointerDown);
    return () => document.removeEventListener("mousedown", handlePointerDown);
  }, [isUserMenuOpen]);

  useEffect(() => {
    if (isLoading || !isAuthenticated) return;

    let cancelled = false;

    const bootstrap = async () => {
      setLoading(true);
      setError(null);
      setPermissionsLoaded(false);

      try {
        const token = await getAccessTokenSilently({
          authorizationParams: API_AUTHORIZATION_PARAMS,
        });
        const parsedPermissions = parsePermissionsFromToken(token);
        const { fetchedCurrentUser, fetchedTickets } =
          await loadBootstrapData(token);

        if (cancelled) return;

        setPermissions(parsedPermissions);
        setCurrentUser(fetchedCurrentUser);
        setAllTickets(fetchedTickets);
        setNeedsConsent(false);
      } catch (error) {
        console.error("Bootstrap failed", error);

        if (cancelled) return;

        setPermissions([]);

        if (isConsentRequiredError(error)) {
          setNeedsConsent(true);
          setError("CORTEX API consent is required before the app can load.");
        } else if (isForbiddenError(error)) {
          setNeedsConsent(false);
          setError("You do not have permission to view tickets.");
        } else {
          setNeedsConsent(false);
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
  }, [isLoading, isAuthenticated, getAccessTokenSilently]);

  useEffect(() => {
    if (activeView !== "sla" || !canManageSla || slaConfigurations.length > 0) {
      return;
    }

    void loadSlaConfigurations();
  }, [activeView, canManageSla, loadSlaConfigurations, slaConfigurations.length]);

  useEffect(() => {
    if (activeView !== "users" || !canViewUsers || users.length > 0) {
      return;
    }

    void loadUsers();
  }, [activeView, canViewUsers, loadUsers, users.length]);

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
      const parsedPermissions = parsePermissionsFromToken(token);
      const { fetchedCurrentUser, fetchedTickets } =
        await loadBootstrapData(token);

      setPermissions(parsedPermissions);
      setCurrentUser(fetchedCurrentUser);
      setAllTickets(fetchedTickets);
      setNeedsConsent(false);
      setPermissionsLoaded(true);
    } catch (error) {
      console.error("Consent failed", error);
      setError("Failed to grant CORTEX API access.");
      toast.error("Failed to grant CORTEX API access");
    } finally {
      setLoading(false);
    }
  };

  const tickets = useMemo(() => {
    const value = normalize(debouncedFilterValue);
    if (filter === "all" || !value) return allTickets;

    if (filter === "status") {
      return allTickets.filter((ticket) =>
        normalize(ticket.status ?? "").includes(value),
      );
    }

    if (filter === "sla") {
      return allTickets.filter((ticket) =>
        normalize(ticket.slaStatus ?? "").includes(value),
      );
    }

    return allTickets.filter((ticket) =>
      normalize(ticket.priority ?? "").includes(value),
    );
  }, [allTickets, filter, debouncedFilterValue]);

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
  const showingStart = totalTickets === 0 ? 0 : (currentPage - 1) * (pageSize === "all" ? totalTickets : pageSize) + 1;
  const showingEnd =
    pageSize === "all"
      ? totalTickets
      : Math.min(totalTickets, currentPage * pageSize);

  useEffect(() => {
    setCurrentPage(1);
  }, [filter, debouncedFilterValue, pageSize]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  const handleSaveTicket = async (updatedTicket: Partial<Ticket>) => {
    if (!selectedTicket) return;

    try {
      const token = await getApiToken();

      if (!selectedTicket.id) {
        const created = await ticketService.create(
          updatedTicket as Omit<Ticket, "id" | "createdDate" | "createdBy">,
          token,
        );
        setAllTickets((prev) => [created, ...prev]);
        toast.success("Ticket created");
      } else {
        const saved = await ticketService.update(
          selectedTicket.id,
          updatedTicket,
          token,
        );
        setAllTickets((prev) =>
          prev.map((ticket) => (ticket.id === saved.id ? saved : ticket)),
        );
        toast.success("Ticket updated");
      }

      setIsModalOpen(false);
      setSelectedTicket(null);
    } catch (error) {
      console.error("Failed to save ticket", error);
      toast.error("Failed to save ticket");
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

  const handleFilterChange = (value: string) => {
    setFilter(isFilterOption(value) ? value : "all");
    setFilterValue("");
  };

  const handlePageSizeChange = (value: string) => {
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

  const toggleTheme = () => {
    setTheme((currentTheme) => (currentTheme === "dark" ? "light" : "dark"));
  };

  const toggleThemeFromMenu = () => {
    toggleTheme();
    setIsUserMenuOpen(false);
  };

  const openProfileModal = async () => {
    setIsUserMenuOpen(false);
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

  const openAdminUserModal = (selectedUser: UserRecord) => {
    setEditingAdminUser(selectedUser);
    setAdminUserDraft({
      nickName: selectedUser.nickName ?? "",
      phoneNumber: selectedUser.phoneNumber ?? "",
      department: selectedUser.department ?? "",
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

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 dark:from-slate-950 dark:to-slate-900 flex items-center justify-center text-gray-900 dark:text-slate-100">
        <div className="text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-cortex-blue mx-auto" />
          <p className="mt-4 text-gray-600 dark:text-slate-400">Loading...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 dark:from-slate-950 dark:to-slate-900 flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
        <div className="text-center bg-white/80 dark:bg-slate-900/80 border border-white/60 dark:border-slate-800 rounded-2xl shadow-xl px-8 py-10 backdrop-blur">
          <h1 className="text-4xl font-bold mb-4">🧠 CORTEX</h1>
          <p className="text-gray-600 dark:text-slate-400 mb-6">
            Support Ticket System
          </p>
          <div className="flex flex-col items-center gap-3">
            <button
              onClick={() =>
                loginWithRedirect({
                  authorizationParams: {
                    ...API_AUTHORIZATION_PARAMS,
                    scope: "openid profile email",
                  },
                })
              }
              className="px-6 py-3 bg-cortex-blue text-white rounded-md hover:bg-blue-700 transition-colors"
            >
              Log In
            </button>
            <button
              onClick={toggleTheme}
              className="px-4 py-2 rounded-md bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 transition-colors"
            >
              {isDarkMode ? "Light Mode" : "Dark Mode"}
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (isAccountExpired) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 dark:from-slate-950 dark:to-slate-900 flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
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
              onClick={() =>
                logout({
                  logoutParams: { returnTo: window.location.origin },
                })
              }
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
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 dark:from-slate-950 dark:to-slate-900 flex items-center justify-center px-6 text-gray-900 dark:text-slate-100">
        <div className="max-w-xl rounded-2xl border border-amber-200 bg-white/90 px-8 py-10 text-center shadow-xl backdrop-blur dark:border-amber-900/40 dark:bg-slate-900/90">
          <h1 className="mb-4 text-3xl font-bold">Your account is inactive</h1>
          <p className="text-gray-600 dark:text-slate-400">
            Please contact an administrator if you believe this is a mistake.
          </p>
          <div className="mt-6 flex justify-center">
            <button
              onClick={() =>
                logout({
                  logoutParams: { returnTo: window.location.origin },
                })
              }
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
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 dark:from-slate-950 dark:to-slate-900 text-gray-900 dark:text-slate-100 transition-colors">
      <header className="bg-white/90 dark:bg-slate-950/90 shadow-sm border-b border-gray-200 dark:border-slate-800 backdrop-blur">
        <div className="max-w-7xl mx-auto px-6 py-6 flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-4">
            <div className="flex flex-col gap-2 md:flex-row md:items-baseline md:gap-4">
              <h1 className="text-3xl font-bold">🧠 CORTEX</h1>
              <h2 className="text-lg text-gray-600 dark:text-slate-400">
                Support Ticket System
              </h2>
            </div>

            <div className="flex flex-wrap gap-2">
              <button
                onClick={() => setActiveView("tickets")}
                className={`px-4 py-2 rounded-md transition-colors ${
                  activeView === "tickets"
                    ? "bg-cortex-blue text-white"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                Tickets
              </button>
              {canManageSla && (
                <button
                  onClick={() => setActiveView("sla")}
                  className={`px-4 py-2 rounded-md transition-colors ${
                    activeView === "sla"
                      ? "bg-cortex-blue text-white"
                      : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                  }`}
                >
                  SLA Settings
                </button>
              )}
              {canViewUsers && (
                <button
                  onClick={() => setActiveView("users")}
                  className={`px-4 py-2 rounded-md transition-colors ${
                    activeView === "users"
                      ? "bg-cortex-blue text-white"
                      : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                  }`}
                >
                  Users
                </button>
              )}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-4 justify-end">
            {activeView === "tickets" && (
              <>
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

                <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-slate-400">
                  <span>Show</span>
                  <select
                    value={pageSize}
                    onChange={(event) => handlePageSizeChange(event.target.value)}
                    className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
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
                    onChange={(event) => setFilterValue(event.target.value)}
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
                    onChange={(event) => setFilterValue(event.target.value)}
                    className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
                  />
                ) : null}

                <button
                  onClick={() => void loadAllTickets()}
                  className="inline-flex items-center rounded-md bg-cortex-blue px-3 py-2 text-sm font-medium text-white shadow-sm transition-colors hover:bg-blue-700"
                >
                  Refresh
                </button>

                {canCreateTickets && (
                  <button
                    onClick={() => openTicket(createDraftTicket())}
                    className="inline-flex items-center rounded-md bg-green-600 px-3.5 py-2 text-sm font-medium text-white shadow-sm transition-colors hover:bg-green-700"
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
                  className="px-3 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
                >
                  Grant Access
                </button>
              </div>
            )}

            <div
              ref={userMenuRef}
              className="relative flex items-center gap-3 pl-4 border-l border-gray-300 dark:border-slate-700"
            >
              <button
                onClick={() => setIsUserMenuOpen((current) => !current)}
                className="inline-flex items-center gap-2 text-sm text-gray-700 dark:text-slate-300 px-3 py-2 rounded-md hover:bg-gray-100 dark:hover:bg-slate-800 transition-colors"
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
                <div className="absolute right-0 top-full mt-2 w-72 rounded-lg border border-gray-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-lg z-20 overflow-hidden">
                  <div className="px-4 py-3 border-b border-gray-100 dark:border-slate-800">
                    <p className="font-medium text-gray-900 dark:text-slate-100">
                      {currentUser?.displayName ?? user?.name}
                    </p>
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      {currentUser?.email ?? user?.email}
                    </p>
                    {currentUser?.nickName && (
                      <p className="text-xs text-gray-500 dark:text-slate-400 mt-1">
                        Nick name: {currentUser.nickName}
                      </p>
                    )}
                  </div>

                  <button
                    onClick={() => void openProfileModal()}
                    disabled={profileLoading}
                    className="w-full px-4 py-3 text-left text-sm text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {profileLoading ? "Loading Profile..." : "Edit Profile"}
                  </button>
                  <button
                    onClick={toggleThemeFromMenu}
                    className="w-full px-4 py-3 text-left text-sm text-gray-700 dark:text-slate-200 hover:bg-gray-50 dark:hover:bg-slate-800 transition-colors"
                  >
                    {isDarkMode ? "Light Mode" : "Dark Mode"}
                  </button>
                  <button
                    onClick={() =>
                      logout({
                        logoutParams: { returnTo: window.location.origin },
                      })
                    }
                    className="w-full px-4 py-3 text-left text-sm text-red-600 dark:text-red-300 hover:bg-red-50 dark:hover:bg-red-950/30 transition-colors"
                  >
                    Log Out
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8">
        {activeView === "tickets" ? (
          <>
            <SlaLegend />

            {loading && (
              <div className="text-center py-12">
                <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-cortex-blue mx-auto" />
                <p className="mt-4 text-gray-600 dark:text-slate-400">
                  Loading tickets…
                </p>
              </div>
            )}

            {error && (
              <div className="bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
                <p className="text-red-700 dark:text-red-300">{error}</p>
              </div>
            )}

            {!loading && !error && tickets.length === 0 && (
              <p className="text-gray-600 dark:text-slate-400 text-center">
                No tickets found
              </p>
            )}

            {!loading && !error && tickets.length > 0 && (
              <>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
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
        ) : activeView === "sla" && canManageSla ? (
          <SlaSettingsPage
            configurations={slaConfigurations}
            error={slaError}
            loading={slaLoading}
            saving={slaSaving}
            onChange={handleSlaConfigurationChange}
            onRefresh={() => void loadSlaConfigurations()}
            onSave={() => void saveSlaConfigurations()}
          />
        ) : activeView === "users" && canViewUsers ? (
          <UsersPage
            users={users}
            loading={usersLoading}
            error={usersError}
            onRefresh={() => void loadUsers()}
            onEdit={openAdminUserModal}
          />
        ) : (
          <div className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-6">
            <p className="text-gray-600 dark:text-slate-400">
              You do not have permission to view this section.
            </p>
          </div>
        )}
      </main>

      {selectedTicket && isModalOpen && (
        <TicketModal
          key={selectedTicket.id ?? "new"}
          ticket={selectedTicket}
          isOpen
          onClose={closeModal}
          onSave={handleSaveTicket}
          onDelete={requestDeleteTicket}
          currentUser={
            currentUser
              ? { displayName: currentUser.displayName ?? "" }
              : null
          }
          createdByDisplayName=""
        />
      )}

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
    </div>
  );
}

export default App;
