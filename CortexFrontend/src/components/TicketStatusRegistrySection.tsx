import { useMemo, useState } from "react";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";
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
  configFieldClass,
} from "./configurationAdminUi";

interface TicketStatusRegistrySectionProps {
  statuses: TicketStatusDefinition[];
  loading: boolean;
  error: string | null;
  saving: boolean;
  deletingId: number | null;
  onRefresh: () => void;
  onCreate: (definition: UpsertTicketStatusDefinitionInput) => Promise<void>;
  onUpdate: (
    id: number,
    definition: UpsertTicketStatusDefinitionInput,
  ) => Promise<void>;
  onDelete: (id: number) => Promise<void>;
}

const EMPTY_DRAFT: UpsertTicketStatusDefinitionInput = {
  name: "",
  description: "",
  isEnabled: true,
};

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function TicketStatusRegistrySection({
  statuses,
  loading,
  error,
  saving,
  deletingId,
  onRefresh,
  onCreate,
  onUpdate,
  onDelete,
}: TicketStatusRegistrySectionProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] =
    useState<UpsertTicketStatusDefinitionInput>(EMPTY_DRAFT);
  const isBusy = saving || deletingId !== null;

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving…" : "Creating…";
    }

    return editingId ? "Save changes" : "Create status";
  }, [editingId, saving]);

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
  };

  const startEdit = (status: TicketStatusDefinition) => {
    setEditingId(status.id);
    setDraft({
      name: status.name,
      description: status.description ?? "",
      isEnabled: status.isEnabled,
    });
  };

  const saveDefinition = async () => {
    if (!draft.name.trim()) {
      return;
    }

    const payload = {
      ...draft,
      name: draft.name.trim(),
      description: draft.description?.trim() || undefined,
    };

    if (editingId) {
      await onUpdate(editingId, payload);
    } else {
      await onCreate(payload);
    }

    resetForm();
  };

  const toggleDefinition = async (status: TicketStatusDefinition) => {
    await onUpdate(status.id, {
      name: status.name,
      description: status.description,
      isEnabled: !status.isEnabled,
    });
  };

  const deleteDefinition = async (status: TicketStatusDefinition) => {
    const confirmed = window.confirm(
      `Delete "${status.name}"? Tickets or archive policies using it must be updated first.`,
    );

    if (!confirmed) {
      return;
    }

    await onDelete(status.id);

    if (editingId === status.id) {
      resetForm();
    }
  };

  const isNewMode = editingId === null;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Statuses"
        description="Workflow stages tickets can use. Also referenced by archive and routing."
        actions={
          <>
            <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
              New status
            </ConfigPrimaryButton>
            <ConfigGhostButton onClick={onRefresh} disabled={isBusy}>
              Reload
            </ConfigGhostButton>
          </>
        }
      />

      {error ? <ConfigErrorBanner>{error}</ConfigErrorBanner> : null}

      <ConfigPageBody>
        <ConfigTwoColumnWideCatalog
          left={
            <div className="flex min-h-[200px] flex-col gap-2">
              {loading ? (
                <p className="py-8 text-center text-sm text-gray-500 dark:text-slate-400">
                  Loading statuses…
                </p>
              ) : statuses.length === 0 ? (
                <div className="flex flex-1 flex-col justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                  <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No statuses yet</p>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                    Add a status to drive ticket workflow.
                  </p>
                  <div className="mt-4 flex justify-center">
                    <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
                      New status
                    </ConfigPrimaryButton>
                  </div>
                </div>
              ) : (
                <ul className="max-h-[min(420px,50vh)] space-y-1 overflow-y-auto pr-0.5">
                  {statuses.map((status) => {
                    const selected = editingId === status.id;
                    return (
                      <li key={status.id}>
                        <button
                          type="button"
                          onClick={() => startEdit(status)}
                          disabled={isBusy}
                          className={`group w-full rounded-lg border px-3 py-2.5 text-left text-sm transition disabled:opacity-50 ${configCatalogItemClass(selected)}`}
                        >
                          <div className="flex items-start justify-between gap-2">
                            <span
                              className={`truncate font-medium ${
                                selected
                                  ? "text-cortex-blue dark:text-cortex-cyan"
                                  : "text-gray-900 group-hover:text-gray-950 dark:text-slate-100"
                              }`}
                            >
                              {status.name}
                            </span>
                            <span
                              className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                                status.isEnabled
                                  ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                  : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                              }`}
                            >
                              {status.isEnabled ? "On" : "Off"}
                            </span>
                          </div>
                          <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-gray-500 dark:text-slate-400">
                            <span>Updated {formatDate(status.lastModifiedDateUtc ?? status.createdDateUtc)}</span>
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                void toggleDefinition(status);
                              }}
                              disabled={isBusy}
                              className="text-cortex-blue hover:underline dark:text-cortex-cyan"
                            >
                              {status.isEnabled ? "Turn off" : "Turn on"}
                            </button>
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
              <ConfigDetailCard title={isNewMode ? "New status" : "Edit status"}>
                <div className="space-y-3">
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Name
                    </label>
                    <input
                      type="text"
                      value={draft.name}
                      onChange={(event) =>
                        setDraft((current) => ({ ...current, name: event.target.value }))
                      }
                      className={configFieldClass}
                      placeholder="e.g. In review"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Description
                    </label>
                    <textarea
                      value={draft.description ?? ""}
                      onChange={(event) =>
                        setDraft((current) => ({
                          ...current,
                          description: event.target.value,
                        }))
                      }
                      rows={3}
                      className={configFieldClass}
                      placeholder="When should this status apply?"
                    />
                  </div>
                </div>
              </ConfigDetailCard>

              <ConfigDetailCard title="Status">
                <label className="flex cursor-pointer items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-0.5 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    checked={draft.isEnabled}
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        isEnabled: event.target.checked,
                      }))
                    }
                  />
                  <span className="text-sm text-gray-800 dark:text-slate-200">
                    Status is enabled for editing tickets
                  </span>
                </label>
              </ConfigDetailCard>

              <ConfigDetailCard title="Actions">
                <div className="flex flex-wrap items-center gap-2">
                  <ConfigPrimaryButton
                    onClick={() => void saveDefinition()}
                    disabled={isBusy || !draft.name.trim()}
                  >
                    {saveLabel}
                  </ConfigPrimaryButton>
                  <ConfigSecondaryButton onClick={resetForm} disabled={isBusy}>
                    Clear
                  </ConfigSecondaryButton>
                  {editingId ? (
                    <button
                      type="button"
                      onClick={() => {
                        const s = statuses.find((x) => x.id === editingId);
                        if (s) void deleteDefinition(s);
                      }}
                      disabled={isBusy || deletingId === editingId}
                      className="rounded-lg border border-red-200 px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50 dark:border-red-800/60 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      {deletingId === editingId ? "Deleting…" : "Delete"}
                    </button>
                  ) : null}
                </div>
              </ConfigDetailCard>
            </div>
          }
        />
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
