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
import RoutingRuleHealthPanel from "./RoutingRuleHealthPanel";
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
  const finalAssignments: string[] = [];

  if (rule.titleContains.trim()) {
    criteria.push(`Title contains "${rule.titleContains}"`);
  }

  if (rule.boardId.trim()) {
    const boardName = boardNameById.get(rule.boardId.trim()) ?? "Selected board";
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
    finalAssignments.push(`Syniti: ${label}`);
  }

  if (rule.businessOwner.trim()) {
    const label =
      ownerDisplayLabel(rule.businessOwner, ownerDirectory).trim() ||
      rule.businessOwner.trim();
    finalAssignments.push(`Business: ${label}`);
  }

  const ifText = criteria.join(" AND ") || "Any ticket";
  const thenText = finalAssignments.join(" | ") || "No owner assignment";
  const summary = `${ifText} -> ${thenText}`;
  return { ifText, thenText, summary };
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
      console.error("Failed to load user directory for recommendation rules", error);
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
        console.error("Failed to load role definitions for recommendation rules", error);
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
  const synitiEligibleOwners = useMemo(
    () =>
      ownerDirectory.filter(
        (entry) => entry.isActive && entry.isSynitiOwnerEligible,
      ),
    [ownerDirectory],
  );
  const businessEligibleOwners = useMemo(
    () =>
      ownerDirectory.filter(
        (entry) => entry.isActive && entry.isBusinessOwnerEligible,
      ),
    [ownerDirectory],
  );
  const noEligibleSynitiOwners =
    !ownerDirectoryLoading &&
    !ownerDirectoryError &&
    synitiEligibleOwners.length === 0;
  const noEligibleBusinessOwners =
    !ownerDirectoryLoading &&
    !ownerDirectoryError &&
    businessEligibleOwners.length === 0;
  const synitiOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : noEligibleSynitiOwners
      ? "No eligible Syniti owners found. Mark a user as active, assign them to Syniti, and enable “Eligible for Syniti Owner assignment.”"
      : "Optional. Leave blank if this rule should only set the business owner.";
  const businessOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : noEligibleBusinessOwners
      ? "No eligible business owners found. Mark a user as active and enable “Eligible for Business Owner assignment.”"
      : "Optional. Leave blank to keep the requester as the business owner.";

  const applyStarterRule = useCallback(() => {
    onNew();
    onChange("requesterDepartment", "Syniti");
    onChange("titleContains", "login");
    onChange("rulePriority", 80);
    onChange("weight", 10);
  }, [onChange, onNew]);

  const [ruleHealthReloadKey, setRuleHealthReloadKey] = useState(0);

  const handleRefreshRulesAndHealth = () => {
    onRefresh();
    setRuleHealthReloadKey((previous) => previous + 1);
  };

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Cortex recommendation rules"
        description="Define routing signals for recommended Syniti and business owners. Higher decision priority runs first; ties use the tie breaker."
        actions={
          <>
            <ConfigPrimaryButton onClick={onNew} disabled={isBusy}>
              New rule
            </ConfigPrimaryButton>
            <ConfigGhostButton onClick={handleRefreshRulesAndHealth} disabled={isBusy}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      {loading ? (
        <div className="px-6 py-10 text-center text-sm text-gray-500 dark:text-slate-400">
          Loading recommendation rules…
        </div>
      ) : (
        <>
          <RoutingRuleHealthPanel reloadKey={ruleHealthReloadKey} />
        <ConfigPageBody>
          <ConfigTwoColumnWideCatalog
            left={
              <div className="space-y-4">
            {rules.length === 0 ? (
              <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                <p className="text-sm font-medium text-gray-800 dark:text-slate-200">
                  No routing rules configured yet
                </p>
                <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                  Add a rule so Cortex can recommend owners before autonomy
                  evaluates tickets. Start blank or use the login/authentication
                  starter as a template.
                </p>
                <div className="mt-4 flex flex-wrap justify-center gap-2">
                  <ConfigPrimaryButton onClick={onNew} disabled={isBusy}>
                    New rule
                  </ConfigPrimaryButton>
                  <ConfigSecondaryButton
                    onClick={applyStarterRule}
                    disabled={isBusy}
                  >
                    Use login/authentication starter
                  </ConfigSecondaryButton>
                </div>
                <p className="mt-3 text-xs text-gray-500 dark:text-slate-400">
                  Starter pre-fills requester department “Syniti”, title
                  keyword “login”, and priority 80 — pick a board and Syniti
                  owner before saving.
                </p>
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
                          Routing rule
                        </p>
                        <div className="mt-1 space-y-1 text-xs">
                          <p className="text-gray-700 dark:text-slate-300">
                            <span className="font-semibold">IF</span>{" "}
                            {describeRule(rule, boardNameById, ownerDirectory).ifText}
                          </p>
                          <p className="text-gray-700 dark:text-slate-300">
                            <span className="font-semibold">THEN</span>{" "}
                            {describeRule(rule, boardNameById, ownerDirectory).thenText}
                          </p>
                        </div>
                      </div>
                      <div className="flex shrink-0 flex-col items-end gap-1">
                        <span
                          className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                            rule.isEnabled
                              ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                              : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                          }`}
                        >
                          {rule.isEnabled ? "On" : "Off"}
                        </span>
                        {rule.isValidConfiguration === false ? (
                          <span
                            className="rounded-full bg-amber-100 px-2.5 py-1 text-xs font-medium text-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
                            title="Configuration error"
                          >
                            Configuration error
                          </span>
                        ) : null}
                      </div>
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
                {selectedRule.isValidConfiguration === false ? (
                  <div className="rounded-xl border border-amber-200 bg-amber-50/80 px-4 py-3 dark:border-amber-800/60 dark:bg-amber-950/30">
                    <p className="text-sm font-semibold text-amber-900 dark:text-amber-100">
                      Configuration error
                    </p>
                    <p className="mt-1 text-sm text-amber-900 dark:text-amber-100">
                      This rule will not be used because the selected owner is
                      not eligible.
                    </p>
                    <p className="mt-1 text-xs text-amber-800 dark:text-amber-200">
                      Pick an eligible owner and save to fix. Cortex skips this
                      rule until then; no tickets are reassigned.
                    </p>
                  </div>
                ) : null}
                <ConfigDetailCard
                  title={isNewRule ? "New routing rule" : "Routing rule"}
                  subtitle="Signals used"
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

                  <ConfigDetailCard title="Final Assignment" subtitle="Recommended Syniti and business owners">
                    <div className="space-y-4">
                      <UserCombobox
                        label="Syniti owner"
                        value={selectedRule.synitiOwner}
                        users={synitiEligibleOwners}
                        onChange={(value) => onChange("synitiOwner", value)}
                        placeholder="Search eligible Syniti owners..."
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled || noEligibleSynitiOwners}
                        helperText={synitiOwnerHelperText}
                        noResultsText="No eligible Syniti owners match."
                      />

                      <UserCombobox
                        label="Business owner"
                        value={selectedRule.businessOwner}
                        users={businessEligibleOwners}
                        onChange={(value) => onChange("businessOwner", value)}
                        placeholder="Search eligible business owners..."
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled || noEligibleBusinessOwners}
                        helperText={businessOwnerHelperText}
                        noResultsText="No eligible business owners match."
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
                        Advanced: priority &amp; tie breaker
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
                              Decision priority
                              <CortexTooltip content="Higher priority rules are considered first when multiple routing rules match a ticket.">
                                <span
                                  className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                  aria-label="About Decision Priority"
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
                              Tie breaker
                              <CortexTooltip content="Breaks ties when two routing rules share the same decision priority. Higher values win.">
                                <span
                                  className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                  aria-label="About tie breaker"
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

                <ConfigDetailCard title="Rule logic" subtitle="Readable IF / THEN">
                    {(selectedRule.boardId.trim() ||
                      selectedRule.priority.trim() ||
                      selectedRule.requesterDepartment.trim() ||
                      selectedRule.requesterRole.trim()) &&
                    (selectedRule.synitiOwner.trim() || selectedRule.businessOwner.trim()) ? (
                      <div className="space-y-2 text-sm text-gray-800 dark:text-slate-200">
                        <p>
                          <span className="font-semibold">IF</span>{" "}
                          {describeRule(selectedRule, boardNameById, ownerDirectory).ifText}
                        </p>
                        <p>
                          <span className="font-semibold">THEN</span>{" "}
                          {describeRule(selectedRule, boardNameById, ownerDirectory).thenText}
                        </p>
                        <p className="text-xs text-gray-500 dark:text-slate-400">
                          Rule summary:{" "}
                          {describeRule(selectedRule, boardNameById, ownerDirectory).summary}
                        </p>
                      </div>
                    ) : (
                      <p className="text-sm text-gray-800 dark:text-slate-200">
                        Add at least one decision factor and one owner.
                      </p>
                    )}
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
        </>
      )}
    </ConfigPageShell>
  );
}
