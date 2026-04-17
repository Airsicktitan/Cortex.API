import { useMemo, useState } from "react";
import type { AdminUpdateUserInput, Auth0RoleOption, UserRecord } from "../types/user";
import PhoneNumberInput from "./PhoneNumberInput";

interface AdminUserEditModalProps {
  isOpen: boolean;
  user: UserRecord | null;
  draft: AdminUpdateUserInput;
  saving: boolean;
  onChange: (field: keyof AdminUpdateUserInput, value: string | boolean) => void;
  onClose: () => void;
  onSave: () => void;
  canManageAccess: boolean;
  /** Roles currently assigned in Auth0 (from Management API). */
  auth0AssignedRoles: Auth0RoleOption[];
  /** All roles defined in Auth0 (for add dropdown). */
  availableAuth0Roles: Auth0RoleOption[];
  rolesLoading: boolean;
  roleMutationLoading: boolean;
  accessFeedback: string | null;
  accessError: string | null;
  onAddRole: (roleName: string) => void;
  onRemoveRole: (roleName: string) => void;
}

const NOTIFICATION_CHANNEL_OPTIONS = [
  { value: "", label: "Use system default" },
  { value: "Neither", label: "Neither" },
  { value: "Email", label: "Email" },
  { value: "Teams", label: "Teams" },
  { value: "Both", label: "Both" },
] as const;

function toDateInputValue(value?: string | null) {
  if (!value) return "";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function normalizeRoleKey(name: string) {
  return name.trim().toLowerCase();
}

export default function AdminUserEditModal({
  isOpen,
  user,
  draft,
  saving,
  onChange,
  onClose,
  onSave,
  canManageAccess,
  auth0AssignedRoles,
  availableAuth0Roles,
  rolesLoading,
  roleMutationLoading,
  accessFeedback,
  accessError,
  onAddRole,
  onRemoveRole,
}: AdminUserEditModalProps) {
  const [addSelection, setAddSelection] = useState("");

  const assignedNameSet = useMemo(() => {
    return new Set(auth0AssignedRoles.map((r) => normalizeRoleKey(r.name)));
  }, [auth0AssignedRoles]);

  const rolesAvailableToAdd = useMemo(() => {
    return availableAuth0Roles.filter(
      (r) => !assignedNameSet.has(normalizeRoleKey(r.name)),
    );
  }, [availableAuth0Roles, assignedNameSet]);

  if (!isOpen || !user) return null;

  const noAuth0 = !user.auth0Id;

  return (
    <div className="scroll-surface fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      <div className="flex min-h-full items-center justify-center p-4">
        <div className="relative w-full max-w-4xl rounded-lg border border-gray-200 bg-white p-6 text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100">
          <div className="mb-6 flex items-start justify-between">
            <div>
              <h2 className="text-2xl font-semibold">Edit User</h2>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Update local CORTEX user settings. Display name and email still
                sync from Auth0 when available. Auth0 roles are managed below.
              </p>
            </div>
            <button
              onClick={onClose}
              className="text-2xl font-bold text-gray-400 hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300"
            >
              ×
            </button>
          </div>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Display Name
              </label>
              <input
                type="text"
                value={user.displayName ?? ""}
                readOnly
                className="w-full rounded-md border-gray-300 bg-gray-100 text-gray-600 shadow-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Email
              </label>
              <input
                type="email"
                value={user.email ?? ""}
                readOnly
                className="w-full rounded-md border-gray-300 bg-gray-100 text-gray-600 shadow-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Nick Name
              </label>
              <input
                type="text"
                value={draft.nickName ?? ""}
                onChange={(event) => onChange("nickName", event.target.value)}
                placeholder="Nick name"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Phone Number
              </label>
              <PhoneNumberInput
                id="admin-user-phone-number"
                value={draft.phoneNumber ?? ""}
                onChange={(value) => onChange("phoneNumber", value)}
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                The selected country controls the calling code and number
                layout.
              </p>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Department
              </label>
              <input
                type="text"
                value={draft.department ?? ""}
                onChange={(event) => onChange("department", event.target.value)}
                placeholder="Department"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Assignment Notifications
              </label>
              <select
                value={draft.assignmentNotificationChannel ?? ""}
                onChange={(event) =>
                  onChange("assignmentNotificationChannel", event.target.value)
                }
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                {NOTIFICATION_CHANNEL_OPTIONS.map((option) => (
                  <option key={option.value || "default"} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                SLA Risk Notifications
              </label>
              <select
                value={draft.slaRiskNotificationChannel ?? ""}
                onChange={(event) =>
                  onChange("slaRiskNotificationChannel", event.target.value)
                }
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                {NOTIFICATION_CHANNEL_OPTIONS.map((option) => (
                  <option key={option.value || "default"} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Expiry Date
              </label>
              <input
                type="date"
                value={toDateInputValue(draft.expiryDate)}
                onChange={(event) => onChange("expiryDate", event.target.value)}
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              />
              <button
                type="button"
                onClick={() => onChange("expiryDate", "")}
                className="mt-2 text-sm text-cortex-blue hover:text-cortex-blue-dark"
              >
                Clear expiry
              </button>
            </div>

            <div className="flex items-center rounded-md border border-gray-200 px-4 py-3 dark:border-slate-700">
              <input
                id="isActive"
                type="checkbox"
                checked={draft.isActive ?? false}
                onChange={(event) => onChange("isActive", event.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
              />
              <label
                htmlFor="isActive"
                className="ml-3 text-sm font-medium text-gray-700 dark:text-slate-300"
              >
                User is active
              </label>
            </div>
          </div>

          {canManageAccess && (
            <section className="mt-6 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-slate-700 dark:bg-slate-950/40">
              <h3 className="text-sm font-semibold uppercase tracking-wide text-gray-700 dark:text-slate-300">
                Auth0 roles
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Source of truth is Auth0 RBAC. Changes apply immediately in Auth0;
                users may need to refresh tokens to see authorization updates.
              </p>

              {noAuth0 ? (
                <p className="mt-3 text-sm text-amber-800 dark:text-amber-200">
                  This user has no Auth0 account linked. Role management is unavailable.
                </p>
              ) : rolesLoading ? (
                <p className="mt-3 text-sm text-gray-500 dark:text-slate-400">
                  Loading roles…
                </p>
              ) : (
                <>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {auth0AssignedRoles.length === 0 ? (
                      <span className="text-sm italic text-gray-500 dark:text-slate-400">
                        No roles assigned
                      </span>
                    ) : (
                      auth0AssignedRoles.map((role) => (
                        <span
                          key={role.id}
                          className="inline-flex items-center gap-1 rounded-full border border-gray-300 bg-white px-3 py-1 text-sm text-gray-800 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100"
                        >
                          {role.name}
                          <button
                            type="button"
                            disabled={roleMutationLoading}
                            onClick={() => onRemoveRole(role.name)}
                            className="ml-1 rounded-full px-1 text-lg leading-none text-red-600 hover:bg-red-50 disabled:opacity-50 dark:text-red-400 dark:hover:bg-red-950/40"
                            title={`Remove ${role.name}`}
                          >
                            ×
                          </button>
                        </span>
                      ))
                    )}
                  </div>

                  <div className="mt-4 flex flex-wrap items-end gap-2">
                    <div className="min-w-[200px] flex-1">
                      <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                        Add role
                      </label>
                      <select
                        value={addSelection}
                        onChange={(e) => setAddSelection(e.target.value)}
                        disabled={roleMutationLoading || rolesAvailableToAdd.length === 0}
                        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                      >
                        <option value="">
                          {rolesAvailableToAdd.length === 0
                            ? "All roles already assigned"
                            : "Select a role…"}
                        </option>
                        {rolesAvailableToAdd.map((r) => (
                          <option key={r.id} value={r.name}>
                            {r.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <button
                      type="button"
                      disabled={
                        roleMutationLoading ||
                        !addSelection ||
                        rolesAvailableToAdd.length === 0
                      }
                      onClick={() => {
                        if (!addSelection) return;
                        onAddRole(addSelection);
                        setAddSelection("");
                      }}
                      className="rounded-md bg-cortex-blue px-4 py-2 text-sm text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-50"
                    >
                      {roleMutationLoading ? "Applying…" : "Add role"}
                    </button>
                  </div>
                </>
              )}
            </section>
          )}

          {(accessError || accessFeedback) && (
            <div className="mt-4 space-y-2">
              {accessError && (
                <p className="text-sm text-red-700 dark:text-red-300">{accessError}</p>
              )}
              {accessFeedback && (
                <p className="text-sm text-green-700 dark:text-green-300">{accessFeedback}</p>
              )}
            </div>
          )}

          <div className="mt-6 flex justify-end gap-3">
            <button
              onClick={onClose}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Cancel
            </button>
            <button
              onClick={onSave}
              disabled={saving || roleMutationLoading}
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {saving ? "Saving…" : "Save User"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
