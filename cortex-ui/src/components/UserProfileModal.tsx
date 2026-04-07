import type { UpdateUserProfileInput, UserProfile } from "../types/user";
import PhoneNumberInput from "./PhoneNumberInput";

interface UserProfileModalProps {
  isOpen: boolean;
  user: UserProfile | null;
  draft: UpdateUserProfileInput;
  saving: boolean;
  onChange: (field: keyof UpdateUserProfileInput, value: string) => void;
  onClose: () => void;
  onSave: () => void;
}

export default function UserProfileModal({
  isOpen,
  user,
  draft,
  saving,
  onChange,
  onClose,
  onSave,
}: UserProfileModalProps) {
  if (!isOpen || !user) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      <div className="flex min-h-full items-center justify-center p-4">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            onSave();
          }}
          className="relative bg-white dark:bg-slate-900 text-gray-900 dark:text-slate-100 rounded-lg shadow-xl border border-gray-200 dark:border-slate-800 max-w-2xl w-full p-6"
        >
          <div className="flex items-start justify-between mb-6">
            <div>
              <h2 className="text-2xl font-semibold">Edit Profile</h2>
              <p className="text-sm text-gray-500 dark:text-slate-400 mt-1">
                Update the local profile details used in CORTEX.
              </p>
            </div>
            <button
              type="button"
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300 text-2xl font-bold"
            >
              ×
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                Display Name
              </label>
              <input
                type="text"
                value={user.displayName ?? ""}
                readOnly
                className="w-full rounded-md border-gray-300 bg-gray-100 text-gray-600 shadow-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Display name is currently sourced from Auth0.
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
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
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                Nick Name
              </label>
              <input
                type="text"
                value={draft.nickName ?? ""}
                onChange={(event) => onChange("nickName", event.target.value)}
                placeholder="How should we refer to you?"
                className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                Phone Number
              </label>
              <PhoneNumberInput
                id="profile-phone-number"
                value={draft.phoneNumber ?? ""}
                onChange={(value) => onChange("phoneNumber", value)}
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Country code is added automatically and the local number format
                adjusts to the selected country.
              </p>
            </div>

            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
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
          </div>

          <div className="flex justify-end gap-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-md bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={saving}
              className="px-4 py-2 rounded-md bg-cortex-blue text-white hover:bg-blue-700 transition-colors disabled:opacity-60"
            >
              {saving ? "Saving..." : "Save Profile"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
