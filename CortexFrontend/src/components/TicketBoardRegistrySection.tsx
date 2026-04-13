import { useMemo, useState } from "react";
import type {
  TicketBoardDefinition,
  UpsertTicketBoardDefinitionInput,
} from "../types/ticketBoard";
import ConfirmDeleteModal from "./ConfirmDeleteModal";

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
      return editingId ? "Saving..." : "Creating...";
    }

    return editingId ? "Save Board" : "Create Board";
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

  return (
    <>
      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                Ticket Boards
              </h3>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Create and manage the boards tickets can live on, including
                boards like Hypercare and Enhancement.
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
                onClick={resetForm}
                disabled={isBusy}
                className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                New Board
              </button>
            </div>
          </div>
        </div>

        {error && (
          <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{error}</p>
          </div>
        )}

        <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.05fr_0.95fr]">
          <div className="overflow-x-auto">
            {loading ? (
              <div className="py-10 text-center text-gray-500 dark:text-slate-400">
                Loading ticket boards...
              </div>
            ) : boards.length === 0 ? (
              <div className="py-10 text-center text-gray-500 dark:text-slate-400">
                No ticket boards have been created yet.
              </div>
            ) : (
              <table className="min-w-full text-sm">
                <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                  <tr>
                    <th className="px-4 py-3 font-medium">Board</th>
                    <th className="px-4 py-3 font-medium">Type</th>
                    <th className="px-4 py-3 font-medium">Availability</th>
                    <th className="px-4 py-3 font-medium">Updated</th>
                    <th className="px-4 py-3 font-medium">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {boards.map((board) => (
                    <tr
                      key={board.id}
                      className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                    >
                      <td className="px-4 py-3 align-top">
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {board.name}
                        </p>
                        <p className="mt-1 max-w-sm text-xs text-gray-500 dark:text-slate-400">
                          {board.description || "No description"}
                        </p>
                      </td>
                      <td className="px-4 py-3 align-top">
                        <span className="inline-flex rounded-full bg-cortex-blue-soft px-3 py-1 text-xs font-medium text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
                          {board.requiresStoryPoints
                            ? "Uses story points"
                            : "Standard board"}
                        </span>
                      </td>
                      <td className="px-4 py-3 align-top">
                        <span
                          className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${
                            board.isEnabled
                              ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                              : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                          }`}
                        >
                          {board.isEnabled ? "Enabled" : "Disabled"}
                        </span>
                      </td>
                      <td className="px-4 py-3 align-top whitespace-nowrap text-xs text-gray-500 dark:text-slate-400">
                        {formatDate(board.lastModifiedDateUtc ?? board.createdDateUtc)}
                      </td>
                      <td className="px-4 py-3 align-top">
                        <div className="flex flex-wrap gap-2">
                          <button
                            onClick={() => startEdit(board)}
                            disabled={isBusy}
                            className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => setPendingDeleteBoard(board)}
                            disabled={isBusy}
                            className="rounded-md border border-red-200 px-3 py-2 text-sm text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                          >
                            {deletingId === board.id ? "Deleting..." : "Delete"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="rounded-lg border border-gray-200 bg-gray-50 p-5 dark:border-slate-700 dark:bg-slate-950/40">
            <h4 className="text-sm font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-300">
              {editingId ? "Edit Ticket Board" : "Add Ticket Board"}
            </h4>

            <div className="mt-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                  Board name
                </label>
                <input
                  type="text"
                  value={draft.name}
                  onChange={(event) =>
                    setDraft((current) => ({ ...current, name: event.target.value }))
                  }
                  className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                  placeholder="Hypercare"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
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
                  className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                  placeholder="Highlight what kind of work belongs on this board."
                />
              </div>

              <label className="flex items-start gap-3 rounded-md border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900/60">
                <input
                  type="checkbox"
                  checked={draft.requiresStoryPoints}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      requiresStoryPoints: event.target.checked,
                    }))
                  }
                  className="mt-1 h-4 w-4"
                />
                <span>
                  <span className="block font-medium text-gray-900 dark:text-slate-100">
                    Requires story points
                  </span>
                  <span className="text-sm text-gray-500 dark:text-slate-400">
                    Use this for enhancement-style boards that need story points
                    from 1 to 5.
                  </span>
                </span>
              </label>

              <label className="flex items-start gap-3 rounded-md border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900/60">
                <input
                  type="checkbox"
                  checked={draft.isEnabled}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      isEnabled: event.target.checked,
                    }))
                  }
                  className="mt-1 h-4 w-4"
                />
                <span>
                  <span className="block font-medium text-gray-900 dark:text-slate-100">
                    Enabled
                  </span>
                  <span className="text-sm text-gray-500 dark:text-slate-400">
                    Disabled boards remain visible for history but can’t receive
                    new ticket assignments.
                  </span>
                </span>
              </label>

              <div className="flex flex-wrap gap-3">
                <button
                  onClick={() => void saveDefinition()}
                  disabled={isBusy || !draft.name?.trim()}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
                >
                  {saveLabel}
                </button>
                <button
                  onClick={resetForm}
                  disabled={isBusy}
                  className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  Clear
                </button>
              </div>
            </div>
          </div>
        </div>
      </section>

      <ConfirmDeleteModal
        isOpen={!!pendingDeleteBoard}
        title="Delete Board"
        message={
          pendingDeleteBoard
            ? `Delete "${pendingDeleteBoard.name}"? Tickets must be moved off this board before it can be removed.`
            : undefined
        }
        onCancel={() => setPendingDeleteBoard(null)}
        onConfirm={() => void deleteDefinition()}
        loading={deletingId === pendingDeleteBoard?.id}
      />
    </>
  );
}
