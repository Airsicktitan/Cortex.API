import { useMemo, useState } from "react";
import type { RoleDefinition } from "../types/roleDefinition";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configCatalogItemClass,
  configFieldClass,
} from "./configurationAdminUi";

interface RoleDefinitionSectionProps {
  roles: RoleDefinition[];
  selectedRole: RoleDefinition | null;
  permissions: string[];
  loading: boolean;
  saving: boolean;
  deletingId: number | null;
  syncingFromAuth0: boolean;
  error: string | null;
  onRefresh: () => void;
  /** Import new role definitions from Auth0 (additive only; does not reconcile existing rows). */
  onSyncFromAuth0: () => void;
  onNew: () => void;
  onSelect: (id: number) => void;
  onChange: <K extends keyof RoleDefinition>(field: K, value: RoleDefinition[K]) => void;
  onSave: () => Promise<void>;
  onDelete: () => Promise<void>;
}

/** Group catalog permissions for clearer scanning (order preserved within group). */
const PERMISSION_GROUPS: { title: string; keys: string[] }[] = [
  { title: "Tickets", keys: ["View Tickets", "Edit Tickets", "Assign Tickets"] },
  { title: "Routing", keys: ["Manage Routing"] },
  { title: "Platform", keys: ["Admin Access"] },
];

export default function RoleDefinitionSection({
  roles,
  selectedRole,
  permissions,
  loading,
  saving,
  deletingId,
  syncingFromAuth0,
  error,
  onRefresh,
  onSyncFromAuth0,
  onNew,
  onSelect,
  onChange,
  onSave,
  onDelete,
}: RoleDefinitionSectionProps) {
  const [searchQuery, setSearchQuery] = useState("");

  const filteredRoles = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return roles;
    return roles.filter((role) => role.name.toLowerCase().includes(q));
  }, [roles, searchQuery]);

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
      !saving &&
      !loading &&
      !syncingFromAuth0,
  );

  const busy = loading || saving || syncingFromAuth0;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Role definitions"
        description="Auth0 controls who can sign in and their roles. Cortex role definitions configure routing, UI, and in-app behavior—they do not replace Auth0 access."
        meta={<p className="text-xs text-gray-400 dark:text-slate-500">Last synced: —</p>}
        actions={
          <>
            <ConfigPrimaryButton onClick={onNew} disabled={saving || syncingFromAuth0}>
              New role
            </ConfigPrimaryButton>
            <ConfigSecondaryButton onClick={onSyncFromAuth0} disabled={busy}>
              {syncingFromAuth0 ? "Importing…" : "Import new roles from Auth0"}
            </ConfigSecondaryButton>
            <ConfigGhostButton onClick={onRefresh} disabled={busy}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      <ConfigPageBody>
        <div className="grid gap-6 lg:grid-cols-[minmax(240px,280px)_minmax(0,1fr)]">
          {/* Role list */}
          <div className="flex min-h-[200px] flex-col gap-3">
            <label className="sr-only" htmlFor="role-def-search">
              Search roles
            </label>
            <input
              id="role-def-search"
              type="search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search roles…"
              disabled={loading || roles.length === 0}
              className={configFieldClass}
            />

            {loading ? (
              <p className="py-8 text-center text-sm text-gray-500 dark:text-slate-400">Loading roles…</p>
            ) : roles.length === 0 ? (
              <div className="flex flex-1 flex-col justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No roles in Cortex yet</p>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Import new roles from Auth0, or add one manually.
                </p>
                <div className="mt-4 flex flex-col items-stretch gap-2 sm:flex-row sm:justify-center">
                  <ConfigSecondaryButton onClick={onSyncFromAuth0} disabled={syncingFromAuth0}>
                    {syncingFromAuth0 ? "Importing…" : "Import new roles from Auth0"}
                  </ConfigSecondaryButton>
                  <ConfigPrimaryButton onClick={onNew} disabled={saving}>
                    New role
                  </ConfigPrimaryButton>
                </div>
              </div>
            ) : filteredRoles.length === 0 ? (
              <p className="py-6 text-center text-sm text-gray-500 dark:text-slate-400">No roles match your search.</p>
            ) : (
              <ul className="max-h-[min(420px,50vh)] space-y-1 overflow-y-auto pr-0.5">
                {filteredRoles.map((role) => {
                  const selected = selectedRole?.id === role.id;
                  return (
                    <li key={role.id}>
                      <button
                        type="button"
                        onClick={() => onSelect(role.id)}
                        className={`group w-full rounded-lg border px-3 py-2.5 text-left text-sm transition ${configCatalogItemClass(selected)}`}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <span
                            className={`truncate font-medium ${
                              selected
                                ? "text-cortex-blue dark:text-cortex-cyan"
                                : "text-gray-900 group-hover:text-gray-950 dark:text-slate-100"
                            }`}
                          >
                            {role.name}
                          </span>
                          <span
                            className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                              role.isEnabled
                                ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                            }`}
                          >
                            {role.isEnabled ? "On" : "Off"}
                          </span>
                        </div>
                        <div className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                          {role.permissions.length} permission{role.permissions.length === 1 ? "" : "s"}
                        </div>
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          {/* Detail panel */}
          <div className="min-w-0 space-y-4">
            {!selectedRole ? (
              <div className="flex min-h-[200px] flex-col items-center justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/50 px-6 py-10 text-center dark:border-slate-700 dark:bg-slate-800/40">
                <p className="text-sm font-medium text-gray-700 dark:text-slate-300">Select a role</p>
                <p className="mt-1 max-w-sm text-sm text-gray-500 dark:text-slate-400">
                  Choose a role from the list to edit details and permissions.
                </p>
              </div>
            ) : (
              <>
                <ConfigDetailCard title="Role details">
                  <div className="space-y-3">
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Name
                      </label>
                      <input
                        value={selectedRole.name}
                        onChange={(event) => onChange("name", event.target.value)}
                        className={configFieldClass}
                        placeholder="Role name"
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Description
                      </label>
                      <textarea
                        value={selectedRole.description ?? ""}
                        onChange={(event) => onChange("description", event.target.value)}
                        rows={3}
                        className={configFieldClass}
                        placeholder="Optional. Who should have this role?"
                      />
                    </div>
                  </div>
                </ConfigDetailCard>

                <ConfigDetailCard
                  title="Cortex permissions"
                  subtitle="Optional behavior flags for this role in Cortex. They do not grant or revoke Auth0 access."
                >
                  <p className="rounded-lg border border-amber-200/80 bg-amber-50/90 px-3 py-2 text-xs leading-relaxed text-amber-950 dark:border-amber-900/50 dark:bg-amber-950/35 dark:text-amber-100/95">
                    Auth0 still controls authentication and authorization. These checkboxes tune Cortex features (for example routing and admin surfaces), not login.
                  </p>
                  <div className="space-y-5">
                    {PERMISSION_GROUPS.map((group) => {
                      const inCatalog = group.keys.filter((k) => permissions.includes(k));
                      if (inCatalog.length === 0) return null;
                      return (
                        <div key={group.title}>
                          <p className="mb-2 text-xs font-medium text-gray-600 dark:text-slate-400">{group.title}</p>
                          <div className="grid gap-2 sm:grid-cols-2">
                            {inCatalog.map((permission) => (
                              <label
                                key={permission}
                                className="flex cursor-pointer items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm transition hover:border-gray-300 dark:border-slate-600 dark:bg-slate-900 dark:hover:border-slate-500"
                              >
                                <input
                                  type="checkbox"
                                  className="rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                                  checked={selectedRole.permissions.includes(permission)}
                                  onChange={() => togglePermission(permission)}
                                />
                                <span className="text-gray-800 dark:text-slate-200">{permission}</span>
                              </label>
                            ))}
                          </div>
                        </div>
                      );
                    })}
                    {(() => {
                      const grouped = new Set(PERMISSION_GROUPS.flatMap((g) => g.keys));
                      const extra = permissions.filter((p) => !grouped.has(p));
                      if (extra.length === 0) return null;
                      return (
                        <div>
                          <p className="mb-2 text-xs font-medium text-gray-600 dark:text-slate-400">Other</p>
                          <div className="grid gap-2 sm:grid-cols-2">
                            {extra.map((permission) => (
                              <label
                                key={permission}
                                className="flex cursor-pointer items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm transition hover:border-gray-300 dark:border-slate-600 dark:bg-slate-900 dark:hover:border-slate-500"
                              >
                                <input
                                  type="checkbox"
                                  className="rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                                  checked={selectedRole.permissions.includes(permission)}
                                  onChange={() => togglePermission(permission)}
                                />
                                <span className="text-gray-800 dark:text-slate-200">{permission}</span>
                              </label>
                            ))}
                          </div>
                        </div>
                      );
                    })()}
                  </div>
                  {selectedRole.permissions.length === 0 ? (
                    <p className="mt-3 text-xs text-amber-800 dark:text-amber-200/90">
                      This role has no Cortex permissions configured. You can save it and add permissions later.
                    </p>
                  ) : null}
                </ConfigDetailCard>

                <ConfigDetailCard title="Status">
                  <p className="mb-3 rounded-lg border border-slate-200/90 bg-white/80 px-3 py-2 text-xs leading-relaxed text-slate-700 dark:border-slate-600 dark:bg-slate-900/50 dark:text-slate-300">
                    Turning a role off here does not remove the Auth0 role or block sign-in. It only affects how Cortex uses this definition (for example routing and enabled features).
                  </p>
                  <label className="flex cursor-pointer items-center gap-3">
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                      checked={selectedRole.isEnabled}
                      onChange={(event) => onChange("isEnabled", event.target.checked)}
                    />
                    <span className="text-sm text-gray-800 dark:text-slate-200">Role definition enabled in Cortex</span>
                  </label>
                </ConfigDetailCard>

                <ConfigDetailCard title="Actions">
                  <div className="flex flex-wrap items-center gap-2">
                    <ConfigPrimaryButton onClick={() => void onSave()} disabled={!canSave}>
                      {saving ? "Saving…" : "Save changes"}
                    </ConfigPrimaryButton>
                    {selectedRole.id > 0 ? (
                      <button
                        type="button"
                        onClick={() => void onDelete()}
                        disabled={deletingId === selectedRole.id || saving}
                        className="rounded-lg border border-red-200 px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50 dark:border-red-800/60 dark:text-red-300 dark:hover:bg-red-950/30"
                      >
                        {deletingId === selectedRole.id ? "Deleting…" : "Delete role"}
                      </button>
                    ) : null}
                  </div>
                </ConfigDetailCard>
              </>
            )}
          </div>
        </div>
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
