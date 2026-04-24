import { useMemo, useState } from "react";
import type {
  CustomReportDefinition,
  DatabaseViewDefinition,
  ReportSource,
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
  reportSources: ReportSource[];
  reportSourcesLoading: boolean;
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

interface BuilderDraft {
  name: string;
  description: string;
  sourceKey: string;
  selectedColumnKeys: string[];
  isEnabled: boolean;
}

const EMPTY_BUILDER_DRAFT: BuilderDraft = {
  name: "",
  description: "",
  sourceKey: "",
  selectedColumnKeys: [],
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

function isLegacyReport(report: CustomReportDefinition) {
  return !report.sourceKey;
}

export default function CustomReportRegistrySection({
  reports,
  databaseViews,
  databaseViewsLoading,
  reportSources,
  reportSourcesLoading,
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
  const [draft, setDraft] = useState<BuilderDraft>(EMPTY_BUILDER_DRAFT);
  const isBusy = saving || deletingId !== null;

  const selectedSource = useMemo(
    () => reportSources.find((s) => s.key === draft.sourceKey) ?? null,
    [reportSources, draft.sourceKey],
  );

  const saveLabel = useMemo(() => {
    if (saving) {
      return editingId ? "Saving…" : "Creating…";
    }
    return editingId ? "Save changes" : "Create report";
  }, [editingId, saving]);

  const canSave =
    !isBusy &&
    draft.name.trim().length > 0 &&
    draft.sourceKey.length > 0 &&
    draft.selectedColumnKeys.length > 0;

  const resetForm = () => {
    setEditingId(null);
    setDraft(EMPTY_BUILDER_DRAFT);
  };

  const startEdit = (report: CustomReportDefinition) => {
    if (isLegacyReport(report)) {
      return;
    }
    setEditingId(report.id);
    setDraft({
      name: report.name,
      description: report.description ?? "",
      sourceKey: report.sourceKey ?? "",
      selectedColumnKeys: report.selectedColumns
        ? report.selectedColumns.split(",").map((k) => k.trim())
        : [],
      isEnabled: report.isEnabled,
    });
  };

  const saveDefinition = async () => {
    if (!canSave) return;

    const payload: UpsertCustomReportDefinitionInput = {
      name: draft.name.trim(),
      viewName: buildDefaultViewName(draft.name),
      description: draft.description.trim() || undefined,
      isEnabled: draft.isEnabled,
      sourceKey: draft.sourceKey,
      selectedColumns: draft.selectedColumnKeys.join(","),
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
    if (!confirmed) return;

    await onDelete(report.id);

    if (editingId === report.id) {
      resetForm();
    }
  };

  const toggleColumn = (key: string) => {
    setDraft((prev) => ({
      ...prev,
      selectedColumnKeys: prev.selectedColumnKeys.includes(key)
        ? prev.selectedColumnKeys.filter((k) => k !== key)
        : [...prev.selectedColumnKeys, key],
    }));
  };

  const selectAllColumns = () => {
    if (!selectedSource) return;
    setDraft((prev) => ({
      ...prev,
      selectedColumnKeys: selectedSource.columns.map((c) => c.key),
    }));
  };

  const clearAllColumns = () => {
    setDraft((prev) => ({ ...prev, selectedColumnKeys: [] }));
  };

  const isNewMode = editingId === null;

  return (
    <ConfigPageShell>
      <ConfigPageHeader
        title="Reports"
        description="Build reports from approved data sources. No SQL required."
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
                    Use the builder to create your first report.
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
                    const legacy = isLegacyReport(report);
                    return (
                      <li key={report.id}>
                        <button
                          type="button"
                          onClick={() => (legacy ? undefined : startEdit(report))}
                          disabled={isBusy || legacy}
                          title={legacy ? "Legacy report — read-only" : undefined}
                          className={`group w-full rounded-lg border px-3 py-2.5 text-left text-sm transition disabled:opacity-60 ${configCatalogItemClass(selected)}`}
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
                            <div className="flex flex-shrink-0 items-center gap-1">
                              {legacy && (
                                <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-gray-500 dark:bg-slate-700 dark:text-slate-400">
                                  Legacy
                                </span>
                              )}
                              <span
                                className={`rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
                                  report.isEnabled
                                    ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                                    : "bg-gray-200 text-gray-600 dark:bg-slate-600 dark:text-slate-300"
                                }`}
                              >
                                {report.isEnabled ? "On" : "Off"}
                              </span>
                            </div>
                          </div>
                          {report.sourceKey && (
                            <div className="mt-1 text-[10px] text-gray-500 dark:text-slate-400">
                              {reportSources.find((s) => s.key === report.sourceKey)?.label ?? report.sourceKey}
                            </div>
                          )}
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
                <div className="space-y-4">
                  {/* Report Name */}
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Report name
                    </label>
                    <input
                      type="text"
                      value={draft.name}
                      onChange={(e) =>
                        setDraft((prev) => ({ ...prev, name: e.target.value }))
                      }
                      className={configFieldClass}
                      placeholder="Open tickets by board"
                    />
                  </div>

                  {/* Description */}
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Description
                      <span className="ml-1 font-normal text-gray-400 dark:text-slate-500">
                        (optional)
                      </span>
                    </label>
                    <textarea
                      value={draft.description}
                      onChange={(e) =>
                        setDraft((prev) => ({ ...prev, description: e.target.value }))
                      }
                      rows={2}
                      className={configFieldClass}
                      placeholder="What this report shows."
                    />
                  </div>

                  {/* Source Selector */}
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Data source
                    </label>
                    {reportSourcesLoading ? (
                      <p className="text-sm text-gray-500 dark:text-slate-400">Loading sources…</p>
                    ) : (
                      <div className="space-y-2">
                        {reportSources.map((source) => {
                          const active = draft.sourceKey === source.key;
                          return (
                            <button
                              key={source.key}
                              type="button"
                              onClick={() =>
                                setDraft((prev) => ({
                                  ...prev,
                                  sourceKey: source.key,
                                  selectedColumnKeys: [],
                                }))
                              }
                              disabled={isBusy}
                              className={`w-full rounded-lg border px-3 py-2.5 text-left text-sm transition disabled:opacity-50 ${
                                active
                                  ? "border-cortex-blue bg-cortex-blue/5 dark:border-cortex-cyan dark:bg-cortex-cyan/10"
                                  : "border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50 dark:border-slate-700 dark:bg-slate-900 dark:hover:border-slate-600 dark:hover:bg-slate-800"
                              }`}
                            >
                              <p
                                className={`font-medium ${active ? "text-cortex-blue dark:text-cortex-cyan" : "text-gray-900 dark:text-slate-100"}`}
                              >
                                {source.label}
                              </p>
                              <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                                {source.description}
                              </p>
                            </button>
                          );
                        })}
                      </div>
                    )}
                  </div>

                  {/* Column Picker */}
                  {selectedSource && (
                    <div>
                      <div className="mb-2 flex items-center justify-between">
                        <label className="text-sm font-medium text-gray-700 dark:text-slate-300">
                          Columns
                        </label>
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={selectAllColumns}
                            className="text-xs text-cortex-blue hover:underline dark:text-cortex-cyan"
                          >
                            All
                          </button>
                          <button
                            type="button"
                            onClick={clearAllColumns}
                            className="text-xs text-gray-400 hover:underline dark:text-slate-500"
                          >
                            None
                          </button>
                        </div>
                      </div>
                      <div className="grid grid-cols-2 gap-1.5">
                        {selectedSource.columns.map((col) => {
                          const checked = draft.selectedColumnKeys.includes(col.key);
                          return (
                            <label
                              key={col.key}
                              className="flex cursor-pointer items-center gap-2 rounded-md border border-gray-100 px-2.5 py-2 hover:bg-gray-50 dark:border-slate-800 dark:hover:bg-slate-800/60"
                            >
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => toggleColumn(col.key)}
                                className="rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                              />
                              <span className="text-xs text-gray-700 dark:text-slate-300">
                                {col.label}
                              </span>
                            </label>
                          );
                        })}
                      </div>
                      {draft.selectedColumnKeys.length === 0 && (
                        <p className="mt-2 text-xs text-amber-600 dark:text-amber-400">
                          Select at least one column.
                        </p>
                      )}
                    </div>
                  )}

                  {/* SQL Preview */}
                  {selectedSource && draft.selectedColumnKeys.length > 0 && (
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Generated query preview
                      </label>
                      <div className="rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-slate-700 dark:bg-slate-950/60">
                        <pre className="whitespace-pre-wrap font-mono text-[10px] text-gray-600 dark:text-slate-400">
                          {`SELECT\n${
                            selectedSource.columns
                              .filter((c) => draft.selectedColumnKeys.includes(c.key))
                              .map((c) => `  … AS [${c.label}]`)
                              .join(",\n")
                          }\nFROM ${selectedSource.label} …`}
                        </pre>
                      </div>
                      <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                        The exact SQL is generated server-side from the source and column selection.
                      </p>
                    </div>
                  )}
                </div>
              </ConfigDetailCard>

              {/* Status */}
              <ConfigDetailCard title="Status">
                <label className="flex cursor-pointer items-start gap-3">
                  <input
                    type="checkbox"
                    className="mt-0.5 rounded border-gray-300 text-cortex-blue focus:ring-cortex-blue"
                    checked={draft.isEnabled}
                    onChange={(e) =>
                      setDraft((prev) => ({ ...prev, isEnabled: e.target.checked }))
                    }
                  />
                  <span className="text-sm text-gray-800 dark:text-slate-200">
                    Show in Reports when enabled
                  </span>
                </label>
              </ConfigDetailCard>

              {/* Actions */}
              <ConfigDetailCard title="Actions">
                <div className="flex flex-wrap items-center gap-2">
                  <ConfigPrimaryButton
                    onClick={() => void saveDefinition()}
                    disabled={!canSave}
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

              {/* Database views (advanced) */}
              <ConfigDetailCard
                title="Existing database views"
                subtitle="Register a view that already exists in SQL Server."
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
                        <p className="font-mono text-xs text-gray-900 dark:text-slate-100">
                          {view.viewName}
                        </p>
                        <p className="mt-0.5 text-[10px] text-gray-400 dark:text-slate-500">
                          Contact your administrator to register this view.
                        </p>
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
