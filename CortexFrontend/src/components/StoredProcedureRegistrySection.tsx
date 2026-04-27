import { useMemo, useRef, useState } from "react";
import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import { ScrollableViewport } from "./ui/ScrollableViewport";

interface StoredProcedureRegistrySectionProps {
  storedProcedures: StoredProcedureDefinition[];
  databaseStoredProcedures: DatabaseStoredProcedureDefinition[];
  databaseStoredProceduresLoading: boolean;
  loading: boolean;
  error: string | null;
  saving: boolean;
  deletingId: number | null;
  onRefresh: () => void;
  onCreate: (
    definition: UpsertStoredProcedureDefinitionInput,
  ) => Promise<void>;
  onUpdate: (
    id: number,
    definition: UpsertStoredProcedureDefinitionInput,
  ) => Promise<void>;
  onDelete: (id: number) => Promise<void>;
}

const EMPTY_DRAFT: UpsertStoredProcedureDefinitionInput = {
  name: "",
  procedureName: "",
  definitionSql: "",
  description: "",
  isEnabled: true,
};

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function StoredProcedureRegistrySection({
  storedProcedures,
  databaseStoredProcedures,
  databaseStoredProceduresLoading,
  loading,
  error,
  saving,
  deletingId,
  onRefresh,
  onCreate,
  onUpdate,
  onDelete,
}: StoredProcedureRegistrySectionProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] =
    useState<UpsertStoredProcedureDefinitionInput>(EMPTY_DRAFT);
  const databaseProcedureListRef = useRef<HTMLDivElement | null>(null);
  const isBusy = saving || deletingId !== null;

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving..." : "Creating...";
    }

    return editingId ? "Save Procedure" : "Create Procedure";
  }, [editingId, saving]);

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
  };

  const startEdit = (definition: StoredProcedureDefinition) => {
    setEditingId(definition.id);
    setDraft({
      name: definition.name,
      procedureName: definition.procedureName,
      definitionSql: definition.definitionSql,
      description: definition.description ?? "",
      isEnabled: definition.isEnabled,
    });
  };

  const saveDefinition = async () => {
    if (!draft.name.trim() || !draft.procedureName.trim()) {
      return;
    }

    const payload = {
      ...draft,
      name: draft.name.trim(),
      procedureName: draft.procedureName.trim(),
      definitionSql: draft.definitionSql.trim(),
      description: draft.description?.trim() || undefined,
    };

    if (editingId) {
      await onUpdate(editingId, payload);
    } else {
      await onCreate(payload);
    }

    resetForm();
  };

  const toggleDefinition = async (definition: StoredProcedureDefinition) => {
    await onUpdate(definition.id, {
      name: definition.name,
      procedureName: definition.procedureName,
      description: definition.description,
      isEnabled: !definition.isEnabled,
      definitionSql: definition.definitionSql,
    });
  };

  const deleteDefinition = async (definition: StoredProcedureDefinition) => {
    const confirmed = window.confirm(
      `Delete "${definition.name}"? Any jobs using it will be disabled and will need a replacement procedure before they can run again.`,
    );

    if (!confirmed) {
      return;
    }

    await onDelete(definition.id);

    if (editingId === definition.id) {
      resetForm();
    }
  };

  const applyExistingProcedure = (
    definition: DatabaseStoredProcedureDefinition,
  ) => {
    setEditingId(null);
    setDraft({
      name: definition.procedureName.split(".").pop() ?? definition.procedureName,
      procedureName: definition.procedureName,
      definitionSql: definition.definitionSql ?? "",
      description: "",
      isEnabled: false,
    });
  };

  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Stored Procedures
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Register the stored procedures that scheduled jobs are allowed to run.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              onClick={onRefresh}
              disabled={isBusy}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              Refresh
            </button>
            <button
              onClick={resetForm}
              disabled={isBusy}
              className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              New Procedure
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.2fr_0.9fr]">
        <div className="overflow-x-auto">
          {loading ? (
            <div className="py-10 text-center text-gray-500 dark:text-slate-400">
              Loading stored procedures...
            </div>
          ) : storedProcedures.length === 0 ? (
            <div className="py-10 text-center text-gray-500 dark:text-slate-400">
              No stored procedures registered yet.
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Label</th>
                  <th className="px-4 py-3 font-medium">Procedure</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Updated</th>
                  <th className="px-4 py-3 font-medium">Action</th>
                </tr>
              </thead>
              <tbody>
                {storedProcedures.map((definition) => (
                  <tr
                    key={definition.id}
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <p className="font-medium text-gray-900 dark:text-slate-100">
                        {definition.name}
                      </p>
                      <p className="mt-1 max-w-sm text-xs text-gray-500 dark:text-slate-400">
                        {definition.description || "No description"}
                      </p>
                    </td>
                    <td className="px-4 py-3 align-top font-mono text-xs">
                      {definition.procedureName}
                    </td>
                    <td className="px-4 py-3 align-top">
                      <span
                        className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${
                          definition.isEnabled
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                            : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {definition.isEnabled ? "Enabled" : "Disabled"}
                      </span>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap text-xs text-gray-500 dark:text-slate-400">
                      {formatDate(
                        definition.lastModifiedDateUtc ?? definition.createdDateUtc,
                      )}
                    </td>
                    <td className="px-4 py-3 align-top">
                      <div className="flex flex-wrap gap-2">
                        <button
                          onClick={() => startEdit(definition)}
                          disabled={isBusy}
                          className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => void toggleDefinition(definition)}
                          disabled={isBusy}
                          className="rounded-md border border-cortex-blue/30 px-3 py-2 text-sm text-cortex-blue transition-colors hover:bg-cortex-blue-soft disabled:opacity-60 dark:border-cortex-cyan/30 dark:text-cortex-cyan dark:hover:bg-cortex-blue/15"
                        >
                          {saving && editingId === definition.id
                            ? "Saving..."
                            : definition.isEnabled
                              ? "Disable"
                              : "Enable"}
                        </button>
                        <button
                          onClick={() => void deleteDefinition(definition)}
                          disabled={isBusy}
                          className="rounded-md border border-red-200 px-3 py-2 text-sm text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                        >
                          {deletingId === definition.id ? "Deleting..." : "Delete"}
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
            {editingId ? "Edit Stored Procedure" : "Add Stored Procedure"}
          </h4>

          <div className="mt-4 space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Display label
              </label>
              <input
                type="text"
                value={draft.name}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, name: event.target.value }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Refresh upstream ticket sync"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Procedure name
              </label>
              <input
                type="text"
                value={draft.procedureName}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    procedureName: event.target.value,
                  }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white font-mono text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="dbo.RefreshTicketSource"
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Use the database procedure name, optionally schema-qualified.
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Procedure SQL
              </label>
              <textarea
                value={draft.definitionSql}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    definitionSql: event.target.value,
                  }))
                }
                rows={10}
                className="mt-2 w-full rounded-md border-gray-300 bg-white font-mono text-sm text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder={"BEGIN\n    SELECT GETDATE() AS CurrentUtc\nEND"}
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Enter the body of the stored procedure. The app will create or alter the procedure in SQL Server.
              </p>
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
                placeholder="Explain what this procedure does before admins schedule it."
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
                  Only enabled stored procedures can be selected for automated jobs.
                </span>
              </span>
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                onClick={() => void saveDefinition()}
                disabled={
                  isBusy ||
                  !draft.name.trim() ||
                  !draft.procedureName.trim() ||
                  !draft.definitionSql.trim()
                }
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

            <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900/70">
              <h5 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
                Existing Database Procedures
              </h5>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Use an existing SQL stored procedure as the starting point for the registry.
              </p>

              <ScrollableViewport
                viewportRef={databaseProcedureListRef}
                outerClassName="mt-3"
                viewportClassName="max-h-56 space-y-2 overflow-y-auto pr-1"
                affordanceAriaLabel="Scroll database procedures to bottom"
              >
                {databaseStoredProceduresLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    Loading database stored procedures...
                  </p>
                ) : databaseStoredProcedures.length === 0 ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    No unregistered database stored procedures found.
                  </p>
                ) : (
                  databaseStoredProcedures.map((definition) => (
                    <div
                      key={definition.procedureName}
                      className="rounded-md border border-gray-200 px-3 py-3 dark:border-slate-700"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="font-mono text-sm text-gray-900 dark:text-slate-100">
                            {definition.procedureName}
                          </p>
                          <p className="mt-1 line-clamp-2 text-xs text-gray-500 dark:text-slate-400">
                            {definition.definitionSql}
                          </p>
                        </div>
                        <button
                          onClick={() => applyExistingProcedure(definition)}
                          disabled={isBusy}
                          className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Use Procedure
                        </button>
                      </div>
                    </div>
                  ))
                )}
              </ScrollableViewport>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
