import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { RoleDefinition } from "../types/roleDefinition";
import type { TicketRoutingRule } from "../types/ticketRouting";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { UserDirectoryEntry } from "../types/user";
import UserCombobox from "./UserCombobox";
import { CortexTooltip } from "./ui/Tooltip";
import {
  ConfigDetailCard,
  ConfigErrorBanner,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  ConfigTwoColumnWideCatalog,
  configCatalogItemClass,
} from "./configurationAdminUi";
import {
  getUserFacingErrorMessage,
  USER_DIRECTORY_INVALIDATED_EVENT,
  userService,
} from "../services/api";
import { ownerDisplayLabel } from "../utils/ownerIdentity";
import { roleDefinitionService } from "../services/roleDefinitionService";

const API_AUDIENCE = "https://cortex-api";

const PRIORITY_OPTIONS = ["Critical", "High", "Medium", "Low"] as const;

interface TicketRoutingSectionProps {
  rules: TicketRoutingRule[];
  boards: TicketBoardDefinition[];
  roleDefinitions: RoleDefinition[];
  /** After a successful config load, dropdown options come only from these definitions (plus legacy stored values). */
  roleDefinitionsLoadedOnce: boolean;
  selectedRule: TicketRoutingRule | null;
  loading: boolean;
  saving: boolean;
  deletingId: number | null;
  error: string | null;
  onRefresh: () => void;
  onNew: () => void;
  onSelect: (id: number) => void;
  onChange: <K extends keyof TicketRoutingRule>(
    field: K,
    value: TicketRoutingRule[K],
  ) => void;
  onSave: () => void;
  onDelete: () => void;
}

function describeRule(
  rule: TicketRoutingRule,
  boardNameById: Map<string, string>,
  ownerDirectory: UserDirectoryEntry[],
) {
  const criteria: string[] = [];
  const assignments: string[] = [];

  if (rule.titleContains.trim()) {
    criteria.push(`Title contains "${rule.titleContains}"`);
  }

  if (rule.boardId.trim()) {
    const boardName = boardNameById.get(rule.boardId.trim()) ?? `Board #${rule.boardId}`;
    criteria.push(`Board: ${boardName}`);
  }

  if (rule.priority.trim()) {
    criteria.push(`Priority: ${rule.priority}`);
  }

  if (rule.requesterDepartment.trim()) {
    criteria.push(`Requester dept: ${rule.requesterDepartment}`);
  }

  if (rule.requesterRole.trim()) {
    criteria.push(`Requester role: ${rule.requesterRole}`);
  }

  if (rule.synitiOwner.trim()) {
    const label =
      ownerDisplayLabel(rule.synitiOwner, ownerDirectory).trim() ||
      rule.synitiOwner.trim();
    assignments.push(`Syniti: ${label}`);
  }

  if (rule.businessOwner.trim()) {
    const label =
      ownerDisplayLabel(rule.businessOwner, ownerDirectory).trim() ||
      rule.businessOwner.trim();
    assignments.push(`Business: ${label}`);
  }

  return `P${rule.rulePriority}/W${rule.weight} :: ${criteria.join(" + ") || "No match criteria"} -> ${assignments.join(" | ") || "No assignments"}`;
}

export default function TicketRoutingSection({
  rules,
  boards,
  roleDefinitions,
  roleDefinitionsLoadedOnce,
  selectedRule,
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
}: TicketRoutingSectionProps) {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const isBusy = saving || deletingId !== null;
  const isNewRule = selectedRule?.id === 0;
  const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);
  const [ownerDirectory, setOwnerDirectory] = useState<UserDirectoryEntry[]>([]);
  const [ownerDirectoryLoading, setOwnerDirectoryLoading] = useState(false);
  const [ownerDirectoryError, setOwnerDirectoryError] = useState<string | null>(
    null,
  );
  const [routingRoleDefinitions, setRoutingRoleDefinitions] = useState<RoleDefinition[]>(
    [],
  );
  const boardNameById = new Map(
    boards.map((board) => [String(board.id), board.name]),
  );
  const requesterRoleOptions = useMemo(() => {
    const sourceRoles = roleDefinitionsLoadedOnce
      ? roleDefinitions
      : roleDefinitions.length > 0
        ? roleDefinitions
        : routingRoleDefinitions;
    const options = sourceRoles
      .filter((role) => role.isEnabled && role.name.trim())
      .map((role) => role.name.trim())
      .sort((left, right) => left.localeCompare(right));

    const selectedRole = selectedRule?.requesterRole.trim();
    if (selectedRole && !options.some((role) => role === selectedRole)) {
      options.unshift(selectedRole);
    }

    return options;
  }, [
    roleDefinitions,
    roleDefinitionsLoadedOnce,
    routingRoleDefinitions,
    selectedRule?.requesterRole,
  ]);

  const loadRoutingOwnerDirectory = useCallback(async () => {
    setOwnerDirectoryLoading(true);
    setOwnerDirectoryError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      const directoryEntries = await userService.getDirectory(token);
      setOwnerDirectory(directoryEntries);
    } catch (error) {
      console.error("Failed to load user directory for routing rules", error);
      setOwnerDirectoryError(
        getUserFacingErrorMessage(error, "Unable to load users."),
      );
    } finally {
      setOwnerDirectoryLoading(false);
    }
  }, [getAccessTokenSilently]);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }
    void loadRoutingOwnerDirectory();
  }, [isAuthenticated, loadRoutingOwnerDirectory]);

  useEffect(() => {
    const onDirectoryInvalidated = () => {
      if (!isAuthenticated) {
        return;
      }
      void loadRoutingOwnerDirectory();
    };
    window.addEventListener(USER_DIRECTORY_INVALIDATED_EVENT, onDirectoryInvalidated);
    return () =>
      window.removeEventListener(USER_DIRECTORY_INVALIDATED_EVENT, onDirectoryInvalidated);
  }, [isAuthenticated, loadRoutingOwnerDirectory]);

  useEffect(() => {
    if (!isAuthenticated || roleDefinitionsLoadedOnce) {
      return;
    }

    if (roleDefinitions.length > 0) {
      setRoutingRoleDefinitions(roleDefinitions);
      return;
    }

    let cancelled = false;

    (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const roles = await roleDefinitionService.getAll(token);
        if (!cancelled) {
          setRoutingRoleDefinitions(roles);
        }
      } catch (error) {
        console.error("Failed to load role definitions for routing rules", error);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    getAccessTokenSilently,
    isAuthenticated,
    roleDefinitions,
    roleDefinitionsLoadedOnce,
  ]);

  const ownerPickerDisabled =
    isBusy ||
    (Boolean(ownerDirectoryError) && ownerDirectory.length === 0);
  const synitiOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : "Optional. Leave blank if this rule should only set the business owner.";
  const businessOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : "Optional. Leave blank to keep the requester as the business owner.";

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Routing rules"
        description="Match new tickets and assign Syniti or business owners. Higher rule priority wins; ties use weight."
        actions={
          <>
            <ConfigPrimaryButton onClick={onNew} disabled={isBusy}>
              New rule
            </ConfigPrimaryButton>
            <ConfigGhostButton onClick={onRefresh} disabled={isBusy}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      {loading ? (
        <div className="px-6 py-10 text-center text-sm text-gray-500 dark:text-slate-400">
          Loading routing rules…
        </div>
      ) : (
        <ConfigPageBody>
          <ConfigTwoColumnWideCatalog
            left={
              <div className="space-y-4">
            {rules.length === 0 ? (
              <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No rules yet</p>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Create a rule to route work by board, priority, department, or role.
                </p>
                <div className="mt-4 flex justify-center">
                  <ConfigPrimaryButton onClick={onNew} disabled={isBusy}>
                    New rule
                  </ConfigPrimaryButton>
                </div>
              </div>
            ) : (
              <ul className="max-h-[min(480px,55vh)] space-y-1 overflow-y-auto pr-0.5">
              {rules.map((rule) => {
                const isSelected = selectedRule?.id === rule.id && selectedRule.id !== 0;

                return (
                  <li key={rule.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(rule.id)}
                    disabled={isBusy}
                    className={`w-full rounded-lg border px-3 py-3 text-left text-sm transition disabled:opacity-60 ${configCatalogItemClass(isSelected)}`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          Rule #{rule.id} (P{rule.rulePriority}, W{rule.weight})
                        </p>
                        <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
                          {describeRule(rule, boardNameById, ownerDirectory)}
                        </p>
                      </div>
                      <span
                        className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                          rule.isEnabled
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                            : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {rule.isEnabled ? "On" : "Off"}
                      </span>
                    </div>
                  </button>
                  </li>
                );
              })}
              </ul>
            )}
              </div>
            }
            right={
              <div className="min-w-0 space-y-4">
            {selectedRule ? (
              <>
                <ConfigDetailCard
                  title={isNewRule ? "New rule" : `Rule #${selectedRule.id}`}
                  subtitle="Match criteria"
                >
                    <div className="grid gap-4 md:grid-cols-2">
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                          Board
                        </label>
                        <select
                          value={selectedRule.boardId}
                          onChange={(event) => onChange("boardId", event.target.value)}
                          className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                        >
                          <option value="">Any board</option>
                          {boards.map((board) => (
                            <option key={board.id} value={String(board.id)}>
                              {board.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                          Priority
                        </label>
                        <select
                          value={selectedRule.priority}
                          onChange={(event) => onChange("priority", event.target.value)}
                          className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                        >
                          <option value="">Any priority</option>
                          {PRIORITY_OPTIONS.map((priority) => (
                            <option key={priority} value={priority}>
                              {priority}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>

                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                          Requester department
                        </label>
                        <input
                          type="text"
                          value={selectedRule.requesterDepartment}
                          onChange={(event) =>
                            onChange("requesterDepartment", event.target.value)
                          }
                          className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                          placeholder="Finance"
                        />
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                          Requester role
                        </label>
                        <select
                          value={selectedRule.requesterRole}
                          onChange={(event) => onChange("requesterRole", event.target.value)}
                          className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                        >
                          <option value="">Any role</option>
                          {requesterRoleOptions.map((roleName) => (
                            <option key={roleName} value={roleName}>
                              {roleName}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                </ConfigDetailCard>

                <ConfigDetailCard title="Assign to" subtitle="Syniti and business owners">
                    <div className="space-y-4">
                      <UserCombobox
                        label="Syniti owner"
                        value={selectedRule.synitiOwner}
                        users={ownerDirectory}
                        onChange={(value) => onChange("synitiOwner", value)}
                        placeholder="Search users..."
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled}
                        helperText={synitiOwnerHelperText}
                      />

                      <UserCombobox
                        label="Business owner"
                        value={selectedRule.businessOwner}
                        users={ownerDirectory}
                        onChange={(value) => onChange("businessOwner", value)}
                        placeholder="Search users..."
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled}
                        helperText={businessOwnerHelperText}
                      />
                    </div>
                </ConfigDetailCard>

                  <div className="rounded-xl border border-gray-200 bg-gray-50/50 dark:border-slate-700 dark:bg-slate-800/40">
                    <button
                      type="button"
                      onClick={() => setIsAdvancedOpen((current) => !current)}
                      className="flex w-full items-center justify-between rounded-t-xl px-4 py-3 text-left"
                    >
                      <h5 className="text-sm font-semibold text-gray-800 dark:text-slate-200">
                        Advanced: priority &amp; weight
                      </h5>
                      <span className="text-xs text-gray-500 dark:text-slate-400">
                        {isAdvancedOpen ? "Hide" : "Show"}
                      </span>
                    </button>
                    {isAdvancedOpen && (
                      <div className="border-t border-gray-200 px-4 py-3 dark:border-slate-700">
                        <div className="grid gap-4 md:grid-cols-2">
                          <div>
                            <label className="block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Rule Priority (higher runs first)
                              <CortexTooltip content="Highest Rule Priority wins when multiple rules match a ticket.">
                                <span
                                  className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                  aria-label="About Rule Priority"
                                  tabIndex={0}
                                >
                                  ?
                                </span>
                              </CortexTooltip>
                            </label>
                            <input
                              type="number"
                              value={selectedRule.rulePriority}
                              onChange={(event) =>
                                onChange("rulePriority", Number(event.target.value) || 0)
                              }
                              className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                            />
                          </div>
                          <div>
                            <label className="block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Weight
                              <CortexTooltip content="Tie-breaker when two rules share the same Rule Priority. Higher weight wins.">
                                <span
                                  className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                  aria-label="About Weight"
                                  tabIndex={0}
                                >
                                  ?
                                </span>
                              </CortexTooltip>
                            </label>
                            <input
                              type="number"
                              value={selectedRule.weight}
                              onChange={(event) =>
                                onChange("weight", Number(event.target.value) || 0)
                              }
                              className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                            />
                          </div>
                        </div>
                      </div>
                    )}
                  </div>

                <ConfigDetailCard title="Status">
                  <label className="flex cursor-pointer items-start gap-3">
                    <input
                      type="checkbox"
                      checked={selectedRule.isEnabled}
                      onChange={(event) => onChange("isEnabled", event.target.checked)}
                      className="mt-1 h-4 w-4 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    />
                    <span className="text-sm text-gray-800 dark:text-slate-200">
                      Rule is enabled (disabled rules are skipped)
                    </span>
                  </label>
                </ConfigDetailCard>

                <ConfigDetailCard title="Preview">
                    <p className="text-sm text-gray-800 dark:text-slate-200">
                      {(selectedRule.boardId.trim() ||
                        selectedRule.priority.trim() ||
                        selectedRule.requesterDepartment.trim() ||
                        selectedRule.requesterRole.trim()) &&
                      (selectedRule.synitiOwner.trim() || selectedRule.businessOwner.trim())
                        ? describeRule(selectedRule, boardNameById, ownerDirectory)
                        : "Add at least one match and one owner."}
                    </p>
                </ConfigDetailCard>

                <ConfigDetailCard title="Actions">
                  <div className="flex flex-wrap gap-2">
                    <ConfigPrimaryButton
                      onClick={onSave}
                      disabled={
                        isBusy ||
                        selectedRule.rulePriority < 0 ||
                        selectedRule.weight < 0 ||
                        (!selectedRule.boardId.trim() &&
                          !selectedRule.priority.trim() &&
                          !selectedRule.requesterDepartment.trim() &&
                          !selectedRule.requesterRole.trim()) ||
                        (!selectedRule.synitiOwner.trim() &&
                          !selectedRule.businessOwner.trim())
                      }
                    >
                      {saving ? "Saving…" : isNewRule ? "Create rule" : "Save changes"}
                    </ConfigPrimaryButton>
                    <ConfigSecondaryButton
                      onClick={onDelete}
                      disabled={isBusy || isNewRule}
                      className="border-red-200 text-red-700 hover:bg-red-50 dark:border-red-800/60 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      {deletingId === selectedRule.id ? "Deleting…" : "Delete"}
                    </ConfigSecondaryButton>
                  </div>
                </ConfigDetailCard>
              </>
            ) : (
              <div className="flex min-h-56 flex-col items-center justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/50 px-4 py-10 text-center dark:border-slate-700 dark:bg-slate-800/30">
                <p className="text-sm font-medium text-gray-700 dark:text-slate-300">Select a rule</p>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Pick a rule from the list or create a new one.
                </p>
              </div>
            )}
              </div>
            }
          />
        </ConfigPageBody>
      )}
    </ConfigPageShell>
  );
}
