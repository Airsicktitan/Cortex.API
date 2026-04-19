import { useMemo, useState } from "react";
import type {
  TicketBoardDefinition,
  UpsertTicketBoardDefinitionInput,
} from "../types/ticketBoard";
import ConfirmDeleteModal from "./ConfirmDeleteModal";
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

interface TicketBoardRegistrySectionProps {
  boards: TicketBoardDefinition[];
  loading: boolean;
  error: string | null;
  saving: boolean;
  deletingId: number | null;
  onRefresh: () => void;
  onCreate: (definition: UpsertTicketBoardDefinitionInput) => Promise<void>;
  onUpdate: (
    id: number,
    definition: UpsertTicketBoardDefinitionInput,
  ) => Promise<void>;
  onDelete: (id: number) => Promise<void>;
}

const EMPTY_DRAFT: UpsertTicketBoardDefinitionInput = {
  name: "",
  description: "",
  requiresStoryPoints: false,
  isEnabled: true,
};

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function TicketBoardRegistrySection({
  boards,
  loading,
  error,
  saving,
  deletingId,
  onRefresh,
  onCreate,
  onUpdate,
  onDelete,
}: TicketBoardRegistrySectionProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] =
    useState<UpsertTicketBoardDefinitionInput>(EMPTY_DRAFT);
  const [pendingDeleteBoard, setPendingDeleteBoard] =
    useState<TicketBoardDefinition | null>(null);
  const isBusy = saving || deletingId !== null;

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving…" : "Creating…";
    }

    return editingId ? "Save changes" : "Create board";
  }, [editingId, saving]);

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
  };

  const startEdit = (board: TicketBoardDefinition) => {
    setEditingId(board.id);
    setDraft({
      name: board.name,
      description: board.description ?? "",
      requiresStoryPoints: board.requiresStoryPoints,
      isEnabled: board.isEnabled,
    });
  };

  const saveDefinition = async () => {
    if (!draft.name?.trim()) {
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

  const deleteDefinition = async () => {
    if (!pendingDeleteBoard) {
      return;
    }

    await onDelete(pendingDeleteBoard.id);

    if (editingId === pendingDeleteBoard.id) {
      resetForm();
    }

    setPendingDeleteBoard(null);
  };

  const isNewMode = editingId === null;

  return (
    <>
      <ConfigPageShell>
        <ConfigPageHeader
          title="Boards"
          description="Define ticket boards and how they behave (for example story points and availability)."
          actions={
            <>
              <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
                New board
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
                    Loading boards…
                  </p>
                ) : boards.length === 0 ? (
                  <div className="flex flex-1 flex-col justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                    <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No boards yet</p>
                    <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                      Create a board or reload after adding data elsewhere.
                    </p>
                    <div className="mt-4 flex justify-center gap-2">
                      <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
                        New board
                      </ConfigPrimaryButton>
                    </div>
                  </div>
                ) : (
                  <ul className="max-h-[min(420px,50vh)] space-y-1 overflow-y-auto pr-0.5">
                    {boards.map((board) => {
                      const selected = editingId === board.id;
                      return (
                        <li key={board.id}>
                          <button
                            type="button"
                            onClick={() => startEdit(board)}
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
                                {board.name}
                              </span>
                              <span
                                className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                                  board.isEnabled
                                    ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                    : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                                }`}
                              >
                                {board.isEnabled ? "On" : "Off"}
                              </span>
                            </div>
                            <div className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                              {board.requiresStoryPoints ? "Story points" : "Standard"} · Updated{" "}
                              {formatDate(board.lastModifiedDateUtc ?? board.createdDateUtc)}
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
                <ConfigDetailCard title={isNewMode ? "New board" : "Edit board"}>
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
                        placeholder="e.g. Hypercare"
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
                        placeholder="What work belongs on this board?"
                      />
                    </div>
                  </div>
                </ConfigDetailCard>

                <ConfigDetailCard title="Options">
                  <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white px-3 py-3 dark:border-slate-600 dark:bg-slate-900">
                    <input
                      type="checkbox"
                      className="mt-0.5 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                      checked={draft.requiresStoryPoints}
                      onChange={(event) =>
                        setDraft((current) => ({
                          ...current,
                          requiresStoryPoints: event.target.checked,
                        }))
                      }
                    />
                    <span>
                      <span className="block text-sm font-medium text-gray-900 dark:text-slate-100">
                        Requires story points
                      </span>
                      <span className="text-xs text-gray-500 dark:text-slate-400">
                        For boards that use 1–5 story point estimates.
                      </span>
                    </span>
                  </label>
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
                      Board is enabled for new work
                    </span>
                  </label>
                </ConfigDetailCard>

                <ConfigDetailCard title="Actions">
                  <div className="flex flex-wrap items-center gap-2">
                    <ConfigPrimaryButton
                      onClick={() => void saveDefinition()}
                      disabled={isBusy || !draft.name?.trim()}
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
                          const b = boards.find((x) => x.id === editingId);
                          if (b) setPendingDeleteBoard(b);
                        }}
                        disabled={isBusy}
                        className="rounded-lg border border-red-200 px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50 dark:border-red-800/60 dark:text-red-300 dark:hover:bg-red-950/30"
                      >
                        Delete
                      </button>
                    ) : null}
                  </div>
                </ConfigDetailCard>
              </div>
            }
          />
        </ConfigPageBody>
      </ConfigPageShell>

      <ConfirmDeleteModal
        isOpen={!!pendingDeleteBoard}
        title="Delete board"
        message={
          pendingDeleteBoard
            ? `Delete "${pendingDeleteBoard.name}"? Tickets must be moved off this board first.`
            : undefined
        }
        onCancel={() => setPendingDeleteBoard(null)}
        onConfirm={() => void deleteDefinition()}
        loading={deletingId === pendingDeleteBoard?.id}
      />
    </>
  );
}
