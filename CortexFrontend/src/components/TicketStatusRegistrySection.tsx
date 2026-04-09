import { useMemo, useState } from "react";
import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";

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
      return editingId ? "Saving..." : "Creating...";
    }

    return editingId ? "Save Status" : "Create Status";
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

  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Ticket Statuses
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Manage the statuses used in ticket workflows and archive policies.
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
              New Status
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
              Loading ticket statuses...
            </div>
          ) : statuses.length === 0 ? (
            <div className="py-10 text-center text-gray-500 dark:text-slate-400">
              No ticket statuses registered yet.
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Availability</th>
                  <th className="px-4 py-3 font-medium">Updated</th>
                  <th className="px-4 py-3 font-medium">Action</th>
                </tr>
              </thead>
              <tbody>
                {statuses.map((status) => (
                  <tr
                    key={status.id}
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <p className="font-medium text-gray-900 dark:text-slate-100">
                        {status.name}
                      </p>
                      <p className="mt-1 max-w-sm text-xs text-gray-500 dark:text-slate-400">
                        {status.description || "No description"}
                      </p>
                    </td>
                    <td className="px-4 py-3 align-top">
                      <span
                        className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${
                          status.isEnabled
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                            : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {status.isEnabled ? "Enabled" : "Disabled"}
                      </span>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap text-xs text-gray-500 dark:text-slate-400">
                      {formatDate(status.lastModifiedDateUtc ?? status.createdDateUtc)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      <div className="flex flex-wrap gap-2">
                        <button
                          onClick={() => startEdit(status)}
                          disabled={isBusy}
                          className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => void toggleDefinition(status)}
                          disabled={isBusy}
                          className="rounded-md border border-cortex-blue/30 px-3 py-2 text-sm text-cortex-blue transition-colors hover:bg-cortex-blue-soft disabled:opacity-60 dark:border-cortex-cyan/30 dark:text-cortex-cyan dark:hover:bg-cortex-blue/15"
                        >
                          {status.isEnabled ? "Disable" : "Enable"}
                        </button>
                        <button
                          onClick={() => void deleteDefinition(status)}
                          disabled={isBusy}
                          className="rounded-md border border-red-200 px-3 py-2 text-sm text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                        >
                          {deletingId === status.id ? "Deleting..." : "Delete"}
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
            {editingId ? "Edit Ticket Status" : "Add Ticket Status"}
          </h4>

          <div className="mt-4 space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Status name
              </label>
              <input
                type="text"
                value={draft.name}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, name: event.target.value }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Pending Vendor Review"
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
                placeholder="Explain when this ticket status should be used."
              />
            </div>

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
                  Disabled statuses stay in history but are hidden from normal ticket editing.
                </span>
              </span>
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                onClick={() => void saveDefinition()}
                disabled={isBusy || !draft.name.trim()}
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
  );
}
