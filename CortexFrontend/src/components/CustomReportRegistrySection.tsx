import { useMemo, useState } from "react";
import type {
  CustomReportDefinition,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";
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
      return editingId ? "Saving…" : "Creating…";
    }

    return editingId ? "Save changes" : "Create report";
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
      `Delete "${report.name}"? This removes it from the Reports area.`,
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

  const isNewMode = editingId === null;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Reports"
        description="Register SQL-backed reports for the Reports workspace. Read-only queries only."
        actions={
          <>
            <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
              New report
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
                  Loading reports…
                </p>
              ) : reports.length === 0 ? (
                <div className="flex flex-1 flex-col justify-center rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-4 py-8 text-center dark:border-slate-700 dark:bg-slate-800/30">
                  <p className="text-sm font-medium text-gray-800 dark:text-slate-200">No custom reports</p>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                    Add a definition or start from a database view.
                  </p>
                  <div className="mt-4 flex justify-center">
                    <ConfigPrimaryButton onClick={resetForm} disabled={isBusy}>
                      New report
                    </ConfigPrimaryButton>
                  </div>
                </div>
              ) : (
                <ul className="max-h-[min(420px,50vh)] space-y-1 overflow-y-auto pr-0.5">
                  {reports.map((report) => {
                    const selected = editingId === report.id;
                    return (
                      <li key={report.id}>
                        <button
                          type="button"
                          onClick={() => startEdit(report)}
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
                              {report.name}
                            </span>
                            <span
                              className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                                report.isEnabled
                                  ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                  : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                              }`}
                            >
                              {report.isEnabled ? "On" : "Off"}
                            </span>
                          </div>
                          <div className="mt-1 line-clamp-2 font-mono text-[10px] text-gray-500 dark:text-slate-400">
                            {report.viewName}
                          </div>
                          <div className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                            Updated {formatDate(report.lastModifiedDateUtc ?? report.createdDateUtc)}
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
              <ConfigDetailCard title={isNewMode ? "New report" : "Edit report"}>
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
                      placeholder="Open tickets by owner"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      View name
                    </label>
                    <input
                      type="text"
                      value={draft.viewName}
                      onChange={(event) =>
                        setDraft((current) => ({ ...current, viewName: event.target.value }))
                      }
                      className={`${configFieldClass} font-mono text-xs`}
                      placeholder="dbo.vw_..."
                    />
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      Optional. Leave blank to auto-generate from the report name.
                    </p>
                    {!draft.viewName.trim() && draft.name.trim() ? (
                      <p className="mt-1 text-xs text-cortex-blue dark:text-cortex-cyan">
                        Generated: {buildDefaultViewName(draft.name)}
                      </p>
                    ) : null}
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
                      rows={2}
                      className={configFieldClass}
                      placeholder="What this report shows."
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      SQL
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
                      className={`${configFieldClass} font-mono text-xs`}
                      placeholder={"SELECT TOP 100 Id, Title, Status\nFROM Tickets\nORDER BY CreatedDate DESC"}
                    />
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      Read-only SQL only. SELECT or CREATE VIEW as supported by the API.
                    </p>
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
                    Show in Reports when enabled
                  </span>
                </label>
              </ConfigDetailCard>

              <ConfigDetailCard title="Actions">
                <div className="flex flex-wrap items-center gap-2">
                  <ConfigPrimaryButton
                    onClick={() => void saveDefinition()}
                    disabled={
                      isBusy ||
                      !draft.name.trim() ||
                      !draft.sqlQuery.trim()
                    }
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
                        const r = reports.find((x) => x.id === editingId);
                        if (r) void deleteDefinition(r);
                      }}
                      disabled={isBusy || deletingId === editingId}
                      className="rounded-lg border border-red-200 px-4 py-2.5 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50 dark:border-red-800/60 dark:text-red-300 dark:hover:bg-red-950/30"
                    >
                      {deletingId === editingId ? "Deleting…" : "Delete"}
                    </button>
                  ) : null}
                </div>
              </ConfigDetailCard>

              <ConfigDetailCard
                title="Database views"
                subtitle="Start from an existing view to prefill SQL."
              >
                <div className="max-h-56 space-y-2 overflow-y-auto pr-1">
                  {databaseViewsLoading ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">Loading views…</p>
                  ) : databaseViews.length === 0 ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">No unregistered views found.</p>
                  ) : (
                    databaseViews.map((view) => (
                      <div
                        key={view.viewName}
                        className="rounded-lg border border-gray-200 bg-white px-3 py-2 dark:border-slate-600 dark:bg-slate-900"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-mono text-xs text-gray-900 dark:text-slate-100">{view.viewName}</p>
                            <p className="mt-1 line-clamp-2 text-[10px] text-gray-500 dark:text-slate-400">
                              {view.definitionSql}
                            </p>
                          </div>
                          <ConfigSecondaryButton
                            className="flex-shrink-0 px-3 py-1.5 text-xs"
                            onClick={() => applyExistingView(view)}
                            disabled={isBusy}
                          >
                            Use
                          </ConfigSecondaryButton>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </ConfigDetailCard>
            </div>
          }
        />
      </ConfigPageBody>
    </ConfigPageShell>
  );
}
