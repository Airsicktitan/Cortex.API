import { useMemo, useState } from "react";
import type {
  CustomReportDefinition,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";

interface CustomReportRegistrySectionProps {
  reports: CustomReportDefinition[];
  databaseViews: DatabaseViewDefinition[];
  databaseViewsLoading: boolean;
  loading: boolean;
  error: string | null;
  saving: boolean;
  deletingId: number | null;
  onRefresh: () => void;
  onCreate: (definition: UpsertCustomReportDefinitionInput) => Promise<void>;
  onUpdate: (
    id: number,
    definition: UpsertCustomReportDefinitionInput,
  ) => Promise<void>;
  onDelete: (id: number) => Promise<void>;
}

const EMPTY_DRAFT: UpsertCustomReportDefinitionInput = {
  name: "",
  viewName: "",
  description: "",
  sqlQuery: "",
  isEnabled: true,
};

function buildDefaultViewName(reportName: string) {
  const slug = reportName
    .trim()
    .replace(/[^A-Za-z0-9_]+/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "");

  if (!slug) {
    return "dbo.vw_CortexReport_CustomReport";
  }

  const normalizedSlug = /^\d/.test(slug) ? `Report_${slug}` : slug;
  return `dbo.vw_CortexReport_${normalizedSlug}`;
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : "—";
}

export default function CustomReportRegistrySection({
  reports,
  databaseViews,
  databaseViewsLoading,
  loading,
  error,
  saving,
  deletingId,
  onRefresh,
  onCreate,
  onUpdate,
  onDelete,
}: CustomReportRegistrySectionProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draft, setDraft] = useState<UpsertCustomReportDefinitionInput>(EMPTY_DRAFT);
  const isBusy = saving || deletingId !== null;

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving..." : "Creating...";
    }

    return editingId ? "Save Report" : "Create Report";
  }, [editingId, saving]);

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_DRAFT);
  };

  const startEdit = (report: CustomReportDefinition) => {
    setEditingId(report.id);
    setDraft({
      name: report.name,
      viewName: report.viewName,
      description: report.description ?? "",
      sqlQuery: report.sqlQuery,
      isEnabled: report.isEnabled,
    });
  };

  const saveDefinition = async () => {
    if (!draft.name.trim() || !draft.sqlQuery.trim()) {
      return;
    }

    const payload = {
      ...draft,
      name: draft.name.trim(),
      viewName: draft.viewName.trim() || buildDefaultViewName(draft.name),
      description: draft.description?.trim() || undefined,
      sqlQuery: draft.sqlQuery.trim(),
    };

    if (editingId) {
      await onUpdate(editingId, payload);
    } else {
      await onCreate(payload);
    }

    resetForm();
  };

  const deleteDefinition = async (report: CustomReportDefinition) => {
    const confirmed = window.confirm(
      `Delete "${report.name}"? This will remove it from the Reports section.`,
    );
    if (!confirmed) {
      return;
    }

    await onDelete(report.id);

    if (editingId === report.id) {
      resetForm();
    }
  };

  const applyExistingView = (view: DatabaseViewDefinition) => {
    setEditingId(null);
    setDraft({
      name: view.viewName.split(".").pop() ?? view.viewName,
      viewName: view.viewName,
      description: "",
      sqlQuery: view.definitionSql,
      isEnabled: false,
    });
  };

  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-5 dark:border-slate-800">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Custom Reports
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Register read-only SQL reports for the Reports workspace.
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
              New Report
            </button>
          </div>
        </div>
      </div>

      {error && (
        <div className="border-b border-red-200 bg-red-50 px-6 py-4 dark:border-red-900/40 dark:bg-red-950/40">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      <div className="grid gap-6 px-6 py-6 lg:grid-cols-[1.15fr_1fr]">
        <div className="overflow-x-auto">
          {loading ? (
            <div className="py-10 text-center text-gray-500 dark:text-slate-400">
              Loading custom reports...
            </div>
          ) : reports.length === 0 ? (
            <div className="py-10 text-center text-gray-500 dark:text-slate-400">
              No custom reports registered yet.
            </div>
          ) : (
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Name</th>
                  <th className="px-4 py-3 font-medium">View</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Updated</th>
                  <th className="px-4 py-3 font-medium">Action</th>
                </tr>
              </thead>
              <tbody>
                {reports.map((report) => (
                  <tr
                    key={report.id}
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <p className="font-medium text-gray-900 dark:text-slate-100">
                        {report.name}
                      </p>
                      <p className="mt-1 max-w-sm text-xs text-gray-500 dark:text-slate-400">
                        {report.description || "No description"}
                      </p>
                    </td>
                    <td className="px-4 py-3 align-top font-mono text-xs">
                      {report.viewName}
                    </td>
                    <td className="px-4 py-3 align-top">
                      <span
                        className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${
                          report.isEnabled
                            ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200"
                            : "bg-gray-200 text-gray-700 dark:bg-slate-800 dark:text-slate-300"
                        }`}
                      >
                        {report.isEnabled ? "Enabled" : "Disabled"}
                      </span>
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap text-xs text-gray-500 dark:text-slate-400">
                      {formatDate(report.lastModifiedDateUtc ?? report.createdDateUtc)}
                    </td>
                    <td className="px-4 py-3 align-top">
                      <div className="flex flex-wrap gap-2">
                        <button
                          onClick={() => startEdit(report)}
                          disabled={isBusy}
                          className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => void deleteDefinition(report)}
                          disabled={isBusy}
                          className="rounded-md border border-red-200 px-3 py-2 text-sm text-red-700 transition-colors hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:text-red-300 dark:hover:bg-red-950/30"
                        >
                          {deletingId === report.id ? "Deleting..." : "Delete"}
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
            {editingId ? "Edit Custom Report" : "Add Custom Report"}
          </h4>

          <div className="mt-4 space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                Report name
              </label>
              <input
                type="text"
                value={draft.name}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, name: event.target.value }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Open tickets by owner"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                View name
              </label>
              <input
                type="text"
                value={draft.viewName}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, viewName: event.target.value }))
                }
                className="mt-2 w-full rounded-md border-gray-300 bg-white font-mono text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="dbo.vw_OpenTicketsByOwner"
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Optional. Leave blank to auto-generate a SQL Server view name from the report name.
              </p>
              {!draft.viewName.trim() && draft.name.trim() && (
                <p className="mt-1 text-xs text-cortex-blue dark:text-cortex-cyan">
                  Generated view name: {buildDefaultViewName(draft.name)}
                </p>
              )}
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
                rows={2}
                className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder="Summarize what this SQL report is intended to show."
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                SQL query
              </label>
              <textarea
                value={draft.sqlQuery}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    sqlQuery: event.target.value,
                  }))
                }
                rows={10}
                className="mt-2 w-full rounded-md border-gray-300 bg-white font-mono text-sm text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                placeholder={"SELECT TOP 100 Id, Title, Status\nFROM Tickets\nORDER BY CreatedDate DESC"}
              />
              <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                Only single-statement read-only SQL is allowed. You can paste either the SELECT/CTE body or a full CREATE VIEW statement.
              </p>
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
                  Only enabled reports appear in the Reports submenu.
                </span>
              </span>
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                onClick={() => void saveDefinition()}
                disabled={
                  isBusy ||
                  !draft.name.trim() ||
                  !draft.sqlQuery.trim()
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
                Existing Database Views
              </h5>
              <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                Use an existing SQL view as the starting point for a registered report.
              </p>

              <div className="mt-3 max-h-56 space-y-2 overflow-y-auto pr-1">
                {databaseViewsLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    Loading database views...
                  </p>
                ) : databaseViews.length === 0 ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    No unregistered database views found.
                  </p>
                ) : (
                  databaseViews.map((view) => (
                    <div
                      key={view.viewName}
                      className="rounded-md border border-gray-200 px-3 py-3 dark:border-slate-700"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="font-mono text-sm text-gray-900 dark:text-slate-100">
                            {view.viewName}
                          </p>
                          <p className="mt-1 line-clamp-2 text-xs text-gray-500 dark:text-slate-400">
                            {view.definitionSql}
                          </p>
                        </div>
                        <button
                          onClick={() => applyExistingView(view)}
                          disabled={isBusy}
                          className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Use View
                        </button>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
