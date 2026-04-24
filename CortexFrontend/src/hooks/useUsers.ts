/**
 * useUsers — manages the user directory, admin CRUD, role mutation, and the
 * session-refresh notice that appears when an admin changes their own role.
 *
 * Profile modal state (isProfileModalOpen, profileDraft, etc.) is intentionally
 * kept in App.tsx because openProfileModal needs to close the user-menu and
 * notification panel, which are layout state owned by App.tsx.  App.tsx calls
 * updateUserRecord() from this hook after a successful profile save so the
 * users list stays consistent.
 */

import { useState, useCallback, useMemo, useEffect } from "react";
import type { Dispatch, SetStateAction } from "react";
import type {
  AdminUpdateUserInput,
  Auth0RoleOption,
  CreateUserInput,
  OnlineUser,
  UserProfile,
  UserRecord,
} from "../types/user";
import {
  userService,
  getUserFacingErrorMessage,
  isLikelyNetworkError,
  ApiError,
} from "../services/api";
import { AUTH0_ROLES, normalizeRoles } from "../utils/role";
import toast from "react-hot-toast";

function isForbiddenError(error: unknown): boolean {
  return error instanceof ApiError && error.status === 403;
}

export interface UseUsersParams {
  getApiToken: () => Promise<string>;
  getFreshApiToken: () => Promise<string>;
  /** Current logged-in user profile (null until bootstrap completes). */
  currentUser: UserProfile | null;
  /** Auth0 user.sub — used to detect when a role change affects the current session. */
  auth0Sub: string | undefined;
  /** Called when a user management action updates the currently logged-in user. */
  setCurrentUser: Dispatch<SetStateAction<UserProfile | null>>;
  setApiUnavailable: Dispatch<SetStateAction<boolean>>;
  canEditUsers: boolean;
  canManageJobsNav: boolean;
  /** Pre-wrapped loginWithRedirect with the correct force-login options. */
  redirectToLogin: () => void;
  loadJobs: (token?: string) => Promise<void>;
}

export function useUsers({
  getApiToken,
  getFreshApiToken,
  currentUser,
  auth0Sub,
  setCurrentUser,
  setApiUnavailable,
  canEditUsers,
  canManageJobsNav,
  redirectToLogin,
  loadJobs,
}: UseUsersParams) {
  const USER_RENDER_PAGE_SIZE = 50;
  // ── User directory list ───────────────────────────────────────────────────
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [usersSearchQuery, setUsersSearchQuery] = useState("");
  const [debouncedUsersSearchQuery, setDebouncedUsersSearchQuery] = useState("");
  const [usersRenderedCount, setUsersRenderedCount] = useState(USER_RENDER_PAGE_SIZE);
  const [usersLoading, setUsersLoading] = useState(false);
  const [usersSyncingFromAuth0, setUsersSyncingFromAuth0] = useState(false);
  const [usersError, setUsersError] = useState<string | null>(null);

  // ── Online users (reports view) ───────────────────────────────────────────
  const [onlineUsers, setOnlineUsers] = useState<OnlineUser[]>([]);
  const [onlineUsersLoading, setOnlineUsersLoading] = useState(false);
  const [onlineUsersError, setOnlineUsersError] = useState<string | null>(null);

  // ── Create-user modal ─────────────────────────────────────────────────────
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
    isSynitiOwnerEligible: false,
    isBusinessOwnerEligible: false,
    expiryDate: "",
  });
  const [createUserSaving, setCreateUserSaving] = useState(false);

  // ── Admin edit modal ──────────────────────────────────────────────────────
  const [editingAdminUser, setEditingAdminUser] = useState<UserRecord | null>(null);
  const [adminUserDraft, setAdminUserDraft] = useState<AdminUpdateUserInput>({});
  const [adminAuth0Roles, setAdminAuth0Roles] = useState<Auth0RoleOption[]>([]);
  const [availableAuth0Roles, setAvailableAuth0Roles] = useState<Auth0RoleOption[]>([]);
  const [adminRolesLoading, setAdminRolesLoading] = useState(false);
  const [roleMutationLoading, setRoleMutationLoading] = useState(false);
  const [adminAccessFeedback, setAdminAccessFeedback] = useState<string | null>(null);
  const [adminAccessError, setAdminAccessError] = useState<string | null>(null);
  const [adminUserSaving, setAdminUserSaving] = useState(false);

  // ── Session refresh notice (shown when admin changes their own role) ───────
  const [sessionRefreshInProgress, setSessionRefreshInProgress] = useState(false);
  const [sessionRefreshNotice, setSessionRefreshNotice] = useState<string | null>(null);

  // ── Deletion in-flight tracker ────────────────────────────────────────────
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);

  // ── Data loaders ──────────────────────────────────────────────────────────

  const loadUsers = useCallback(
    async (
      providedToken?: string,
      options?: { skipLoadingSpinner?: boolean },
    ) => {
      const skipSpinner = options?.skipLoadingSpinner === true;
      if (!skipSpinner) {
        setUsersLoading(true);
      }
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
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setUsersError("Failed to load users.");
        }
      } finally {
        if (!skipSpinner) {
          setUsersLoading(false);
        }
      }
    },
    [getApiToken, setApiUnavailable],
  );

  const syncUsersFromAuth0 = useCallback(async () => {
    setUsersSyncingFromAuth0(true);
    setUsersError(null);
    try {
      const token = await getApiToken();
      const result = await userService.syncFromAuth0(token);
      await loadUsers(token, { skipLoadingSpinner: true });
      const parts = [
        result.created > 0 ? `${result.created} created` : null,
        result.linkedByEmail > 0 ? `${result.linkedByEmail} linked by email` : null,
        result.updated > 0 ? `${result.updated} updated` : null,
        result.unchanged > 0 ? `${result.unchanged} unchanged` : null,
      ].filter(Boolean);
      let detail = parts.length > 0 ? parts.join(", ") : "Directory already matched.";
      if (result.skippedNoEmail > 0 || result.skippedEmailConflict > 0) {
        detail += ` (${result.skippedNoEmail} without email, ${result.skippedEmailConflict} email conflict)`;
      }
      toast.success(`Auth0: ${result.totalFromAuth0} user(s). ${detail}`);
    } catch (error) {
      console.error("Failed to sync users from Auth0", error);
      if (isForbiddenError(error)) {
        setApiUnavailable(false);
        setUsersError("You do not have permission to import users from Auth0.");
      } else if (isLikelyNetworkError(error)) {
        setApiUnavailable(true);
      } else {
        setApiUnavailable(false);
        setUsersError(
          getUserFacingErrorMessage(error, "Failed to import users from Auth0."),
        );
      }
    } finally {
      setUsersSyncingFromAuth0(false);
    }
  }, [getApiToken, loadUsers, setApiUnavailable]);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedUsersSearchQuery(usersSearchQuery.trim().toLowerCase());
    }, 250);
    return () => window.clearTimeout(handle);
  }, [usersSearchQuery]);

  useEffect(() => {
    setUsersRenderedCount(USER_RENDER_PAGE_SIZE);
  }, [debouncedUsersSearchQuery, users]);

  const usersFiltered = useMemo(() => {
    if (!debouncedUsersSearchQuery) {
      return users;
    }

    return users.filter((user) => {
      const haystack = [
        user.displayName,
        user.nickName,
        user.email,
        user.phoneNumber,
        user.department,
        user.role,
        ...(user.roles ?? []),
      ]
        .map((value) => String(value ?? "").toLowerCase())
        .join(" ");
      return haystack.includes(debouncedUsersSearchQuery);
    });
  }, [debouncedUsersSearchQuery, users]);

  const usersVisible = useMemo(
    () => usersFiltered.slice(0, usersRenderedCount),
    [usersFiltered, usersRenderedCount],
  );

  const usersHasMore = usersRenderedCount < usersFiltered.length;

  const loadMoreUsers = useCallback(() => {
    setUsersRenderedCount((current) =>
      Math.min(current + USER_RENDER_PAGE_SIZE, usersFiltered.length),
    );
  }, [usersFiltered.length]);

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
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setOnlineUsersError("Failed to load online users.");
        }
      } finally {
        setOnlineUsersLoading(false);
      }
    },
    [getApiToken, setApiUnavailable],
  );

  // ── Modal open/close ──────────────────────────────────────────────────────

  const openCreateUserModal = useCallback(() => {
    setCreateUserDraft({
      displayName: "",
      nickName: "",
      email: "",
      password: "",
      phoneNumber: "",
      department: "",
      role: "User",
      isActive: true,
      isSynitiOwnerEligible: false,
      isBusinessOwnerEligible: false,
      expiryDate: "",
    });
    setIsCreateUserModalOpen(true);
  }, []);

  const closeCreateUserModal = useCallback(() => {
    setIsCreateUserModalOpen(false);
  }, []);

  const openAdminUserModal = useCallback(
    (selectedUser: UserRecord) => {
      setEditingAdminUser(selectedUser);
      setAdminUserDraft({
        nickName: selectedUser.nickName ?? "",
        phoneNumber: selectedUser.phoneNumber ?? "",
        department: selectedUser.department ?? "",
        assignmentNotificationChannel:
          selectedUser.assignmentNotificationChannel ?? "",
        slaRiskNotificationChannel: selectedUser.slaRiskNotificationChannel ?? "",
        isActive: selectedUser.isActive,
        isSynitiOwnerEligible: selectedUser.isSynitiOwnerEligible,
        isBusinessOwnerEligible: selectedUser.isBusinessOwnerEligible,
        expiryDate: selectedUser.expiryDate ?? "",
      });
      setAdminAccessFeedback(null);
      setAdminAccessError(null);
      setAdminAuth0Roles([]);
      setAvailableAuth0Roles([]);
      setAdminRolesLoading(true);

      void (async () => {
        try {
          const token = await getApiToken();
          const [detail, catalog] = await Promise.all([
            selectedUser.auth0Id
              ? userService.getUserAuth0Roles(selectedUser.id, token)
              : Promise.resolve({ roles: [] as Auth0RoleOption[] }),
            userService.getAvailableAuth0Roles(token),
          ]);
          setAdminAuth0Roles(detail.roles);
          setAvailableAuth0Roles(catalog);
        } catch (error) {
          console.error("Failed to load Auth0 roles", error);
          setAdminAccessError(
            getUserFacingErrorMessage(error, "Could not load Auth0 roles."),
          );
        } finally {
          setAdminRolesLoading(false);
        }
      })();
    },
    [getApiToken],
  );

  const closeAdminUserModal = useCallback(() => {
    setEditingAdminUser(null);
    setAdminUserDraft({});
    setAdminAuth0Roles([]);
    setAvailableAuth0Roles([]);
    setAdminAccessFeedback(null);
    setAdminAccessError(null);
  }, []);

  // ── Draft change handlers ─────────────────────────────────────────────────

  const handleAdminUserDraftChange = useCallback(
    (field: keyof AdminUpdateUserInput, value: string | boolean) => {
      setAdminUserDraft((currentDraft) => ({
        ...currentDraft,
        [field]: value,
      }));
    },
    [],
  );

  const handleCreateUserDraftChange = useCallback(
    (field: keyof CreateUserInput, value: string | boolean) => {
      setCreateUserDraft((currentDraft) => {
        const next: CreateUserInput = { ...currentDraft, [field]: value };
        if (field === "role" && typeof value === "string") {
          const isDeveloper =
            value.trim().toLowerCase() ===
            AUTH0_ROLES.Developer.toLowerCase();
          const deptEmpty = !currentDraft.department?.trim();
          if (isDeveloper && deptEmpty) {
            next.department = "Syniti";
          }
        }
        return next;
      });
    },
    [],
  );

  // ── Session refresh after own role change ─────────────────────────────────

  const refreshSessionAfterSelfRoleChange = useCallback(
    async (updatedUser: UserRecord) => {
      const isCurrentUser =
        (currentUser?.id != null && updatedUser.id === currentUser.id) ||
        (Boolean(auth0Sub) &&
          Boolean(updatedUser.auth0Id) &&
          auth0Sub === updatedUser.auth0Id);

      if (!isCurrentUser) {
        return;
      }

      setSessionRefreshInProgress(true);
      setSessionRefreshNotice(null);

      try {
        const freshToken = await getFreshApiToken();
        const refreshedUser = await userService.getCurrentUser(freshToken);
        setCurrentUser(refreshedUser);

        const expectedRoles = normalizeRoles(updatedUser.roles, updatedUser.role);
        const refreshedRoles = normalizeRoles(refreshedUser.roles, refreshedUser.role);
        const claimsAreFresh =
          expectedRoles.length === refreshedRoles.length &&
          expectedRoles.every((value, index) => value === refreshedRoles[index]);

        if (!claimsAreFresh) {
          setSessionRefreshNotice(
            "Your access changed. Refresh your session to apply updated navigation and permissions.",
          );
          return;
        }

        setSessionRefreshNotice(null);
      } catch (error) {
        console.warn("Failed to refresh session after role update", error);
        setSessionRefreshNotice(
          "Your access changed. Refresh your session to apply updated navigation and permissions.",
        );
      } finally {
        setSessionRefreshInProgress(false);
      }
    },
    [auth0Sub, currentUser?.id, getFreshApiToken, setCurrentUser],
  );

  const forceSessionRefreshForAuthChanges = useCallback(() => {
    setSessionRefreshNotice(null);
    redirectToLogin();
  }, [redirectToLogin]);

  // ── Role mutation ─────────────────────────────────────────────────────────

  const handleAddAuth0Role = useCallback(
    async (roleName: string) => {
      if (!editingAdminUser?.auth0Id || !canEditUsers) return;

      setRoleMutationLoading(true);
      setAdminAccessError(null);
      try {
        const token = await getApiToken();
        const updated = await userService.mutateUserAuth0Role(
          editingAdminUser.id,
          { action: "add", roleName },
          token,
        );
        setUsers((list) =>
          list.map((u) => (u.id === updated.id ? { ...u, ...updated } : u)),
        );
        setEditingAdminUser((prev) =>
          prev && prev.id === updated.id ? { ...prev, ...updated } : prev,
        );
        setCurrentUser((existingUser) =>
          existingUser && existingUser.id === updated.id
            ? { ...existingUser, ...updated }
            : existingUser,
        );
        const detail = await userService.getUserAuth0Roles(editingAdminUser.id, token);
        setAdminAuth0Roles(detail.roles);
        await refreshSessionAfterSelfRoleChange(updated);
        toast.success(`Role "${roleName}" added`);
      } catch (error) {
        console.error("Failed to add role", error);
        const message = getUserFacingErrorMessage(error, "Failed to add role");
        setAdminAccessError(message);
        toast.error(message);
      } finally {
        setRoleMutationLoading(false);
      }
    },
    [
      canEditUsers,
      editingAdminUser,
      getApiToken,
      refreshSessionAfterSelfRoleChange,
      setCurrentUser,
    ],
  );

  const handleRemoveAuth0Role = useCallback(
    async (roleName: string) => {
      if (!editingAdminUser?.auth0Id || !canEditUsers) return;

      setRoleMutationLoading(true);
      setAdminAccessError(null);
      try {
        const token = await getApiToken();
        const updated = await userService.mutateUserAuth0Role(
          editingAdminUser.id,
          { action: "remove", roleName },
          token,
        );
        setUsers((list) =>
          list.map((u) => (u.id === updated.id ? { ...u, ...updated } : u)),
        );
        setEditingAdminUser((prev) =>
          prev && prev.id === updated.id ? { ...prev, ...updated } : prev,
        );
        setCurrentUser((existingUser) =>
          existingUser && existingUser.id === updated.id
            ? { ...existingUser, ...updated }
            : existingUser,
        );
        const detail = await userService.getUserAuth0Roles(editingAdminUser.id, token);
        setAdminAuth0Roles(detail.roles);
        await refreshSessionAfterSelfRoleChange(updated);
        toast.success(`Role "${roleName}" removed`);
      } catch (error) {
        console.error("Failed to remove role", error);
        const message = getUserFacingErrorMessage(error, "Failed to remove role");
        setAdminAccessError(message);
        toast.error(message);
      } finally {
        setRoleMutationLoading(false);
      }
    },
    [
      canEditUsers,
      editingAdminUser,
      getApiToken,
      refreshSessionAfterSelfRoleChange,
      setCurrentUser,
    ],
  );

  // ── Save / create / delete ────────────────────────────────────────────────

  const saveAdminUser = useCallback(async () => {
    if (!editingAdminUser) return;

    try {
      setAdminUserSaving(true);
      setAdminAccessFeedback(null);
      setAdminAccessError(null);
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
          userRecord.id === updatedUser.id
            ? { ...userRecord, ...updatedUser }
            : userRecord,
        ),
      );
      setCurrentUser((existingUser) =>
        existingUser && existingUser.id === updatedUser.id
          ? { ...existingUser, ...updatedUser }
          : existingUser,
      );
      userService.clearDirectoryCache();

      setAdminAccessFeedback("User saved.");
      closeAdminUserModal();
      toast.success("User updated");
    } catch (error) {
      console.error("Failed to update user", error);
      const message = getUserFacingErrorMessage(
        error,
        "Failed to update user access",
      );
      setAdminAccessError(message);
      toast.error(message);
    } finally {
      setAdminUserSaving(false);
    }
  }, [
    adminUserDraft,
    closeAdminUserModal,
    editingAdminUser,
    getApiToken,
    setCurrentUser,
  ]);

  const saveCreatedUser = useCallback(async () => {
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
      userService.clearDirectoryCache();
      closeCreateUserModal();
      toast.success("User created");
    } catch (error) {
      console.error("Failed to create user", error);
      toast.error(getUserFacingErrorMessage(error, "Failed to create user"));
    } finally {
      setCreateUserSaving(false);
    }
  }, [closeCreateUserModal, createUserDraft, getApiToken]);

  const deleteUserRecord = useCallback(
    async (selectedUser: UserRecord) => {
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
          currentUsers.filter(
            (userRecord) => userRecord.id !== selectedUser.id,
          ),
        );
        setOnlineUsers((currentUsers) =>
          currentUsers.filter(
            (userRecord) => userRecord.id !== selectedUser.id,
          ),
        );
        userService.clearDirectoryCache();

        if (editingAdminUser?.id === selectedUser.id) {
          closeAdminUserModal();
        }

        if (canManageJobsNav) {
          void loadJobs(token);
        }

        toast.success("User deleted");
      } catch (error) {
        console.error("Failed to delete user", error);
        toast.error(getUserFacingErrorMessage(error, "Failed to delete user"));
      } finally {
        setDeletingUserId(null);
      }
    },
    [
      canManageJobsNav,
      closeAdminUserModal,
      editingAdminUser?.id,
      getApiToken,
      loadJobs,
    ],
  );

  // ── Cross-cutting helper (called by saveProfile in App.tsx) ───────────────

  /** Updates a single record in the users list — call this after saving the current user's own profile. */
  const updateUserRecord = useCallback((updatedUser: UserProfile) => {
    setUsers((currentUsers) =>
      currentUsers.map((userRecord) =>
        userRecord.id === updatedUser.id
          ? { ...userRecord, ...updatedUser }
          : userRecord,
      ),
    );
  }, []);

  // ── Return ────────────────────────────────────────────────────────────────

  return {
    // State
    users,
    usersSearchQuery,
    setUsersSearchQuery,
    usersVisible,
    usersHasMore,
    loadMoreUsers,
    usersLoading,
    usersSyncingFromAuth0,
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
    // Handlers
    loadUsers,
    syncUsersFromAuth0,
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
  };
}
