import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useState } from "react";
import type { TicketRoutingRule } from "../types/ticketRouting";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { UserDirectoryEntry } from "../types/user";
import UserCombobox from "./UserCombobox";
import { getUserFacingErrorMessage, userService } from "../services/api";
import { ownerDisplayLabel } from "../utils/ownerIdentity";

const API_AUDIENCE = "https://cortex-api";

const PRIORITY_OPTIONS = ["Critical", "High", "Medium", "Low"] as const;

interface TicketRoutingSectionProps {
  rules: TicketRoutingRule[];
  boards: TicketBoardDefinition[];
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
  const boardNameById = new Map(
    boards.map((board) => [String(board.id), board.name]),
  );

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    let cancelled = false;

    (async () => {
      setOwnerDirectoryLoading(true);
      setOwnerDirectoryError(null);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const directoryEntries = await userService.getDirectory(token);
        if (!cancelled) {
          setOwnerDirectory(directoryEntries);
        }
      } catch (error) {
        console.error("Failed to load user directory for routing rules", error);
        if (!cancelled) {
          setOwnerDirectoryError(
            getUserFacingErrorMessage(error, "Unable to load users."),
          );
        }
      } finally {
        if (!cancelled) {
          setOwnerDirectoryLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [getAccessTokenSilently, isAuthenticated]);

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
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Ticket Routing
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Deterministic routing uses structured factors plus explicit rule precedence.
            </p>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              New tickets default the business owner to the requester and only use auto-routing when the owner fields are left blank.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              onClick={onRefresh}
              disabled={isBusy}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Refresh
            </button>
            <button
              onClick={onNew}
              disabled={isBusy}
              className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              New Rule
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {loading ? (
        <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
          Loading ticket routing rules...
        </div>
      ) : (
        <div className="grid gap-6 px-6 py-6 lg:grid-cols-[0.95fr_1.05fr]">
          <div className="space-y-3">
            {rules.length === 0 ? (
              <div className="rounded-lg border border-dashed border-gray-300 px-5 py-8 text-center text-sm text-gray-500 dark:border-slate-700 dark:text-slate-400">
                No routing rules have been added yet.
              </div>
            ) : (
              rules.map((rule) => {
                const isSelected = selectedRule?.id === rule.id && selectedRule.id !== 0;

                return (
                  <button
                    key={rule.id}
                    onClick={() => onSelect(rule.id)}
                    disabled={isBusy}
                    className={`w-full rounded-lg border px-4 py-4 text-left transition-colors disabled:opacity-60 ${
                      isSelected
                        ? "border-cortex-blue bg-cortex-blue-soft/70 dark:border-cortex-cyan dark:bg-cortex-blue/15"
                        : "border-gray-200 hover:bg-gray-50 dark:border-slate-700 dark:hover:bg-slate-800/70"
                    }`}
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
                        {rule.isEnabled ? "Enabled" : "Disabled"}
                      </span>
                    </div>
                  </button>
                );
              })
            )}
          </div>

          <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
            {selectedRule ? (
              <>
                <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
                  {isNewRule ? "New Routing Rule" : `Edit Rule #${selectedRule.id}`}
                </h4>

                <div className="mt-4 space-y-4">
                  <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900/70">
                    <h5 className="text-sm font-semibold text-gray-800 dark:text-slate-200">
                      When a ticket matches:
                    </h5>

                    <div className="mt-3 grid gap-4 md:grid-cols-2">
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
                        <input
                          type="text"
                          value={selectedRule.requesterRole}
                          onChange={(event) => onChange("requesterRole", event.target.value)}
                          className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                          placeholder="BusinessManager"
                        />
                      </div>
                    </div>
                  </div>

                  <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900/70">
                    <h5 className="text-sm font-semibold text-gray-800 dark:text-slate-200">
                      Route it to:
                    </h5>

                    <div className="mt-3 space-y-4">
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
                  </div>

                  <div className="rounded-lg border border-gray-200 bg-white dark:border-slate-700 dark:bg-slate-900/70">
                    <button
                      type="button"
                      onClick={() => setIsAdvancedOpen((current) => !current)}
                      className="flex w-full items-center justify-between px-4 py-3 text-left"
                    >
                      <h5 className="text-sm font-semibold text-gray-800 dark:text-slate-200">
                        Rule behavior (advanced)
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
                              <span
                                className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                title="When multiple rules match, the rule with the highest Rule Priority wins first."
                              >
                                ?
                              </span>
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
                              <span
                                className="ml-2 cursor-help text-xs text-gray-400 dark:text-slate-500"
                                title="Secondary tie-breaker when Rule Priority is the same. Higher weight wins."
                              >
                                ?
                              </span>
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

                  <label className="flex items-start gap-3 rounded-md border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900/60">
                    <input
                      type="checkbox"
                      checked={selectedRule.isEnabled}
                      onChange={(event) => onChange("isEnabled", event.target.checked)}
                      className="mt-1 h-4 w-4"
                    />
                    <span>
                      <span className="block font-medium text-gray-900 dark:text-slate-100">
                        Enabled
                      </span>
                      <span className="text-sm text-gray-500 dark:text-slate-400">
                        Disabled rules stay saved but are skipped during auto-assignment.
                      </span>
                    </span>
                  </label>

                  <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900/70">
                    <p className="text-sm font-medium text-gray-900 dark:text-slate-100">
                      {(selectedRule.boardId.trim() ||
                        selectedRule.priority.trim() ||
                        selectedRule.requesterDepartment.trim() ||
                        selectedRule.requesterRole.trim()) &&
                      (selectedRule.synitiOwner.trim() || selectedRule.businessOwner.trim())
                        ? describeRule(selectedRule, boardNameById, ownerDirectory)
                        : "Define at least one condition and who the ticket should be routed to."}
                    </p>
                  </div>

                  <div className="flex flex-wrap gap-3">
                    <button
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
                      className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
                    >
                      {saving ? "Saving..." : isNewRule ? "Create Rule" : "Save Rule"}
                    </button>
                    <button
                      onClick={onDelete}
                      disabled={isBusy || isNewRule}
                      className="rounded-md border border-red-200 px-4 py-2 text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      {deletingId === selectedRule.id ? "Deleting..." : "Delete Rule"}
                    </button>
                  </div>
                </div>
              </>
            ) : (
              <div className="flex h-full min-h-56 items-center justify-center text-center text-sm text-gray-500 dark:text-slate-400">
                Select a routing rule or create a new one to get started.
              </div>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
