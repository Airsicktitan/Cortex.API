import type { UserRecord } from "../types/user";
import { UsersSkeleton } from "./LoadingSkeletons";
import { formatStoredPhoneNumber } from "../utils/phoneNumber";

interface UsersPageProps {
  users: UserRecord[];
  loading: boolean;
  error: string | null;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  currentUserId?: number;
  deletingUserId: number | null;
  onRefresh: () => void;
  onCreate: () => void;
  onEdit: (user: UserRecord) => void;
  onDelete: (user: UserRecord) => void;
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

function formatDateOnly(value?: string) {
  return value ? new Date(value).toLocaleDateString() : "—";
}

function formatValue(value?: string) {
  return value && value.trim() ? value : "—";
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
          {r}
        </span>
      ))}
    </div>
  );
}

export default function UsersPage({
  users,
  loading,
  error,
  canCreate,
  canEdit,
  canDelete,
  currentUserId,
  deletingUserId,
  onRefresh,
  onCreate,
  onEdit,
  onDelete,
}: UsersPageProps) {
  if (loading) {
    return <UsersSkeleton />;
  }

  return (
    <div className="space-y-6">
      <section className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 p-6">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Registered Users
            </h2>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              View the user directory, provision new users, and manage local
              CORTEX user settings.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <span className="text-sm text-gray-500 dark:text-slate-400">
              {users.length} user{users.length === 1 ? "" : "s"}
            </span>
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
        <div className="bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <section className="bg-white dark:bg-slate-900 rounded-lg border border-gray-200 dark:border-slate-800 overflow-hidden">
        {users.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No users found.
          </div>
        ) : (
          <div className="overflow-x-auto">
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
                      {formatValue(user.displayName)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatValue(user.nickName)}
                    </td>
                    <td className="px-4 py-3 align-top">{formatValue(user.email)}</td>
                    <td className="px-4 py-3 align-top">
                      {formatStoredPhoneNumber(user.phoneNumber)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      {formatValue(user.department)}
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
                      {formatDateOnly(user.createdDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDateOnly(user.lastLoginDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDate(user.expiryDate)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDate(user.lastModifiedDate)}
                    </td>
                    {(canEdit || canDelete) && (
                      <td className="px-4 py-3 align-top">
                        <button
                          onClick={() => onEdit(user)}
                          disabled={!canEdit}
                          className="rounded-md bg-gray-100 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                        >
                          Edit
                        </button>
                        {canDelete && user.id !== currentUserId && (
                          <button
                            onClick={() => onDelete(user)}
                            disabled={deletingUserId === user.id}
                            className="ml-2 rounded-md border border-red-200 px-3 py-2 text-sm text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                          >
                            {deletingUserId === user.id ? "Deleting..." : "Delete"}
                          </button>
                        )}
                        {canDelete && user.id === currentUserId && (
                          <span className="ml-2 inline-flex px-2 py-2 text-xs text-gray-500 dark:text-slate-400">
                            Current User
                          </span>
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
