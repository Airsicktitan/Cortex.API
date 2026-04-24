import type { UserRecord } from "../types/user";
import { useRef, type UIEvent } from "react";
import { UsersSkeleton } from "./LoadingSkeletons";
import { CortexTooltip } from "./ui/Tooltip";
import { ScrollableViewport } from "./ui/ScrollableViewport";
import { formatStoredPhoneNumber } from "../utils/phoneNumber";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  humanizeEnumLabel,
} from "../utils/presentation";

interface UsersPageProps {
  users: UserRecord[];
  totalUsers: number;
  searchQuery: string;
  onSearchQueryChange: (value: string) => void;
  hasMore: boolean;
  loadingMore: boolean;
  onLoadMore: () => void;
  loading: boolean;
  syncingFromAuth0: boolean;
  error: string | null;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  currentUserId?: number;
  deletingUserId: number | null;
  onRefresh: () => void;
  onSyncFromAuth0: () => void;
  onCreate: () => void;
  onEdit: (user: UserRecord) => void;
  onDelete: (user: UserRecord) => void;
}

function formatRolesDisplay(user: UserRecord) {
  const list =
    user.roles && user.roles.length > 0
      ? user.roles
      : user.role
        ? [user.role]
        : [];
  if (list.length === 0) {
    return (
      <span className="text-gray-400 dark:text-slate-500">No roles assigned</span>
    );
  }
  return (
    <div className="flex flex-wrap gap-1">
      {list.map((r) => (
        <span
          key={r}
          className="inline-flex rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700 dark:bg-slate-800 dark:text-slate-300"
        >
          {humanizeEnumLabel(r)}
        </span>
      ))}
    </div>
  );
}

function TrashIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 20 20"
      className="h-4 w-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M3.5 5.5h13" />
      <path d="M8 5.5V4a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v1.5" />
      <path d="M15 5.5 14.4 16a1.5 1.5 0 0 1-1.5 1.4H7.1A1.5 1.5 0 0 1 5.6 16L5 5.5" />
      <path d="M8.5 8.5v5" />
      <path d="M11.5 8.5v5" />
    </svg>
  );
}

export default function UsersPage({
  users,
  totalUsers,
  searchQuery,
  onSearchQueryChange,
  hasMore,
  loadingMore,
  onLoadMore,
  loading,
  syncingFromAuth0,
  error,
  canCreate,
  canEdit,
  canDelete,
  currentUserId,
  deletingUserId,
  onRefresh,
  onSyncFromAuth0,
  onCreate,
  onEdit,
  onDelete,
}: UsersPageProps) {
  const tableScrollRef = useRef<HTMLDivElement | null>(null);

  const handleContainerScroll = (event: UIEvent<HTMLDivElement>) => {
    if (!hasMore || loadingMore || loading) return;
    const target = event.currentTarget;
    const remaining = target.scrollHeight - target.scrollTop - target.clientHeight;
    if (remaining < 220) {
      onLoadMore();
    }
  };

  if (loading) {
    return <UsersSkeleton />;
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-6 overflow-hidden">
      <section className="shrink-0 bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-6">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Registered Users
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              View the local Cortex directory (projection from Auth0). Import missing
              Auth0 users to populate owner pickers and assignments.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm text-gray-500 dark:text-slate-400">
              {totalUsers} user{totalUsers === 1 ? "" : "s"}
            </span>
            <button
              type="button"
              onClick={onSyncFromAuth0}
              disabled={loading || syncingFromAuth0}
              className="rounded-md border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-50 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              {syncingFromAuth0 ? "Importing…" : "Import users from Auth0"}
            </button>
            {canCreate && (
              <button
                onClick={onCreate}
                className="px-4 py-2 rounded-md bg-cortex-blue text-white hover:bg-cortex-blue-dark transition-colors"
              >
                Add User
              </button>
            )}
            <button
              onClick={onRefresh}
              className="px-4 py-2 rounded-md bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 transition-colors"
            >
              Refresh
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="shrink-0 bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <section className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
          <input
            type="text"
            value={searchQuery}
            onChange={(event) => onSearchQueryChange(event.target.value)}
            placeholder="Search users"
            className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
          />
        </div>
        {users.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No users found.
          </div>
        ) : (
          <ScrollableViewport
            viewportRef={tableScrollRef}
            outerClassName="min-h-0 flex-1"
            viewportClassName="min-h-0 h-full overflow-auto"
            affordanceAriaLabel="Scroll users to bottom"
            viewportProps={{ onScroll: handleContainerScroll }}
          >
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 dark:bg-slate-800/80 text-left text-gray-600 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Display Name</th>
                  <th className="px-4 py-3 font-medium">Nick Name</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium">Phone</th>
                  <th className="px-4 py-3 font-medium">Department</th>
                  <th className="px-4 py-3 font-medium">Role</th>
                  <th className="px-4 py-3 font-medium">Active</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                  <th className="px-4 py-3 font-medium">Last Login</th>
                  <th className="px-4 py-3 font-medium">Expiry</th>
                  <th className="px-4 py-3 font-medium">Last Modified</th>
                  {(canEdit || canDelete) && (
                    <th className="px-4 py-3 font-medium">Actions</th>
                  )}
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr
                    key={user.id}
                    className="border-t border-gray-100 dark:border-slate-800 text-gray-700 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top font-medium text-gray-900 dark:text-slate-100">
                      {formatDisplayValue(user.displayName)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(user.nickName)}
                    </td>
                    <td className="px-4 py-3 align-top">{formatDisplayValue(user.email)}</td>
                    <td className="px-4 py-3 align-top">
                      {formatStoredPhoneNumber(user.phoneNumber)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(user.department)}
                    </td>
                    <td className="px-4 py-3 align-top">{formatRolesDisplay(user)}</td>
                    <td className="px-4 py-3 align-top">
                      <span
                        className={`inline-flex px-2 py-1 rounded-full text-xs font-medium ${
                          user.isActive
                            ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300"
                            : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {user.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.createdDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastLoginDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.expiryDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastModifiedDate)}
                    </td>
                    {(canEdit || canDelete) && (
                      <td className="px-4 py-3 align-top">
                        <div className="flex items-center gap-2 whitespace-nowrap">
                          <button
                            type="button"
                            onClick={() => onEdit(user)}
                            disabled={!canEdit}
                            className="inline-flex h-9 items-center justify-center rounded-md bg-gray-100 px-3 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                          >
                            Edit
                          </button>
                          {canDelete && user.id !== currentUserId && (
                            <CortexTooltip
                              content={
                                deletingUserId === user.id
                                  ? "Deleting this user…"
                                  : "Permanently delete this user from Auth0 and Cortex."
                              }
                            >
                              <button
                                type="button"
                                onClick={() => onDelete(user)}
                                disabled={deletingUserId === user.id}
                                aria-label={`Delete ${
                                  user.displayName || user.email || "user"
                                }`}
                                aria-busy={deletingUserId === user.id}
                                className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-red-200 text-sm font-medium text-red-700 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                              >
                                {deletingUserId === user.id ? (
                                  <span
                                    aria-hidden="true"
                                    className="h-4 w-4 animate-spin rounded-full border-2 border-red-200 border-t-red-700 dark:border-red-900/60 dark:border-t-red-300"
                                  />
                                ) : (
                                  <TrashIcon />
                                )}
                              </button>
                            </CortexTooltip>
                          )}
                          {canDelete && user.id === currentUserId && (
                            <span className="inline-flex h-9 items-center rounded-md px-2 text-xs font-medium text-gray-500 dark:text-slate-400">
                              Current User
                            </span>
                          )}
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
              {loadingMore && (
                <div className="sticky bottom-0 border-t border-gray-200 bg-white/95 px-4 py-3 text-center text-sm text-gray-500 backdrop-blur dark:border-slate-800 dark:bg-slate-900/95 dark:text-slate-300">
                  Loading more users...
                </div>
              )}
          </ScrollableViewport>
        )}
      </section>
    </div>
  );
}
