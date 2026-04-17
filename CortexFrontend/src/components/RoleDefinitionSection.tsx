import type { RoleDefinition } from "../types/roleDefinition";

interface RoleDefinitionSectionProps {
  roles: RoleDefinition[];
  selectedRole: RoleDefinition | null;
  permissions: string[];
  loading: boolean;
  saving: boolean;
  deletingId: number | null;
  error: string | null;
  onRefresh: () => void;
  onNew: () => void;
  onSelect: (id: number) => void;
  onChange: <K extends keyof RoleDefinition>(field: K, value: RoleDefinition[K]) => void;
  onSave: () => Promise<void>;
  onDelete: () => Promise<void>;
}

export default function RoleDefinitionSection({
  roles,
  selectedRole,
  permissions,
  loading,
  saving,
  deletingId,
  error,
  onRefresh,
  onNew,
  onSelect,
  onChange,
  onSave,
  onDelete,
}: RoleDefinitionSectionProps) {
  const togglePermission = (permission: string) => {
    if (!selectedRole) return;
    const nextPermissions = selectedRole.permissions.includes(permission)
      ? selectedRole.permissions.filter((value) => value !== permission)
      : [...selectedRole.permissions, permission];
    onChange("permissions", nextPermissions);
  };

  const canSave = Boolean(
    selectedRole &&
      selectedRole.name.trim().length > 0 &&
      selectedRole.permissions.length > 0 &&
      !saving,
  );

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">User roles</h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Define roles and permissions. Assign users in the Users workspace.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onRefresh}
            disabled={loading || saving}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Refresh
          </button>
          <button
            type="button"
            onClick={onNew}
            disabled={saving}
            className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white hover:bg-cortex-blue-dark disabled:opacity-60"
          >
            New role
          </button>
        </div>
      </div>

      {error && (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-800/60 dark:bg-red-900/20 dark:text-red-200">
          {error}
        </p>
      )}

      <div className="mt-5 grid gap-6 lg:grid-cols-[260px_minmax(0,1fr)]">
        <div className="space-y-2">
          {roles.map((role) => (
            <button
              key={role.id}
              type="button"
              onClick={() => onSelect(role.id)}
              className={`w-full rounded-md border px-3 py-2 text-left text-sm ${
                selectedRole?.id === role.id
                  ? "border-cortex-blue bg-blue-50 text-cortex-blue dark:border-cortex-blue dark:bg-slate-800"
                  : "border-gray-200 text-gray-700 hover:bg-gray-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              }`}
            >
              <div className="font-medium">{role.name}</div>
              <div className="text-xs text-gray-500 dark:text-slate-400">
                {role.isEnabled ? "Enabled" : "Disabled"} • {role.permissions.length} permission
                {role.permissions.length === 1 ? "" : "s"}
              </div>
            </button>
          ))}
          {!loading && roles.length === 0 && (
            <p className="text-sm text-gray-500 dark:text-slate-400">No roles have been created yet.</p>
          )}
        </div>

        <div className="space-y-4">
          {!selectedRole ? (
            <p className="text-sm text-gray-500 dark:text-slate-400">
              Select a role to edit, or create a new role.
            </p>
          ) : (
            <>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">Role name</label>
                <input
                  value={selectedRole.name}
                  onChange={(event) => onChange("name", event.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                  placeholder="e.g. Operations Manager"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">Description</label>
                <textarea
                  value={selectedRole.description ?? ""}
                  onChange={(event) => onChange("description", event.target.value)}
                  rows={3}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                  placeholder="Describe when this role should be used."
                />
              </div>

              <div>
                <p className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">Permissions</p>
                <div className="grid gap-2 sm:grid-cols-2">
                  {permissions.map((permission) => (
                    <label
                      key={permission}
                      className="flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-sm dark:border-slate-700"
                    >
                      <input
                        type="checkbox"
                        checked={selectedRole.permissions.includes(permission)}
                        onChange={() => togglePermission(permission)}
                      />
                      <span>{permission}</span>
                    </label>
                  ))}
                </div>
              </div>

              <label className="flex items-center gap-2 text-sm text-gray-700 dark:text-slate-300">
                <input
                  type="checkbox"
                  checked={selectedRole.isEnabled}
                  onChange={(event) => onChange("isEnabled", event.target.checked)}
                />
                Role is enabled
              </label>

              <p className="text-xs text-gray-500 dark:text-slate-400">
                Users are assigned roles in the Users workspace, not individual permissions.
              </p>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => void onSave()}
                  disabled={!canSave}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-sm text-white hover:bg-cortex-blue-dark disabled:opacity-60"
                >
                  {saving ? "Saving..." : "Save Role"}
                </button>
                {selectedRole.id > 0 && (
                  <button
                    type="button"
                    onClick={() => void onDelete()}
                    disabled={deletingId === selectedRole.id || saving}
                    className="rounded-md border border-red-300 px-4 py-2 text-sm text-red-700 hover:bg-red-50 disabled:opacity-60 dark:border-red-800 dark:text-red-300 dark:hover:bg-red-900/20"
                  >
                    {deletingId === selectedRole.id ? "Deleting..." : "Delete Role"}
                  </button>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </section>
  );
}
