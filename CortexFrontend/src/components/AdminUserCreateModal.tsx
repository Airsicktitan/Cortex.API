import type { CreateUserInput } from "../types/user";
import { AUTH0_ROLES } from "../utils/role";
import PhoneNumberInput from "./PhoneNumberInput";
import { humanizeEnumLabel } from "../utils/presentation";

interface AdminUserCreateModalProps {
  isOpen: boolean;
  draft: CreateUserInput;
  saving: boolean;
  canAssignAdminRole: boolean;
  onChange: (field: keyof CreateUserInput, value: string | boolean) => void;
  onClose: () => void;
  onSave: () => void;
}

const BASE_ROLE_OPTIONS = [
  AUTH0_ROLES.User,
  AUTH0_ROLES.Guest,
  AUTH0_ROLES.BusinessManager,
  AUTH0_ROLES.Developer,
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

export default function AdminUserCreateModal({
  isOpen,
  draft,
  saving,
  canAssignAdminRole,
  onChange,
  onClose,
  onSave,
}: AdminUserCreateModalProps) {
  if (!isOpen) return null;

  const roleOptions = canAssignAdminRole
    ? ([
        AUTH0_ROLES.Admin,
        AUTH0_ROLES.Developer,
        AUTH0_ROLES.BusinessManager,
        AUTH0_ROLES.User,
        AUTH0_ROLES.Guest,
      ] as const)
    : BASE_ROLE_OPTIONS;

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
              <h2 className="text-2xl font-semibold">Add User</h2>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Create the Auth0 account and the local CORTEX user record in one step.
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
                value={draft.displayName}
                onChange={(event) => onChange("displayName", event.target.value)}
                placeholder="Maria Jones"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Email
              </label>
              <input
                type="email"
                value={draft.email}
                onChange={(event) => onChange("email", event.target.value)}
                placeholder="maria.jones@syniti.com"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Temporary Password
              </label>
              <input
                type="password"
                value={draft.password}
                onChange={(event) => onChange("password", event.target.value)}
                placeholder="At least 8 characters"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
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
                placeholder="MJ"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Phone Number
              </label>
              <PhoneNumberInput
                id="create-user-phone-number"
                value={draft.phoneNumber ?? ""}
                onChange={(value) => onChange("phoneNumber", value)}
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Department
              </label>
              <input
                type="text"
                value={draft.department ?? ""}
                onChange={(event) => onChange("department", event.target.value)}
                placeholder="Finance"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                Role
              </label>
              <select
                value={draft.role}
                onChange={(event) => onChange("role", event.target.value)}
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              >
                {roleOptions.map((role) => (
                  <option key={role} value={role}>
                    {humanizeEnumLabel(role)}
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
                id="create-user-is-active"
                type="checkbox"
                checked={draft.isActive}
                onChange={(event) => onChange("isActive", event.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
              />
              <label
                htmlFor="create-user-is-active"
                className="ml-3 text-sm font-medium text-gray-700 dark:text-slate-300"
              >
                User is active
              </label>
            </div>
          </div>

          <div className="mt-6 flex justify-end gap-3">
            <button
              onClick={onClose}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Cancel
            </button>
            <button
              onClick={onSave}
              disabled={
                saving ||
                !draft.displayName.trim() ||
                !draft.email.trim() ||
                !draft.password.trim()
              }
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {saving ? "Creating..." : "Create User"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
