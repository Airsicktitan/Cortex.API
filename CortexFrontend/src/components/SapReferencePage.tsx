import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useState } from "react";
import type {
  CreateSapFieldInput,
  CreateSapReferenceSourceInput,
  CreateSapTableInput,
  SapFieldMetadataResponse,
  SapReferenceSearchResultDto,
  SapReferenceSourceResponse,
  SapTableMetadataResponse,
} from "../types/sapReference";
import { getUserFacingErrorMessage } from "../services/api";
import { sapReferenceService } from "../services/sapReferenceService";
import {
  ConfigDetailCard,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configFieldClass,
} from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

type WizardStep = "catalog" | "table" | "field";

function formatSapReferenceCreateError(error: unknown, fallback: string): string {
  const msg = getUserFacingErrorMessage(error, fallback);
  const m = msg.toLowerCase();
  if (!m.includes("already exists")) {
    return msg;
  }
  if (m.includes("source") || m.includes("catalog")) {
    return "A reference catalog with that name already exists.";
  }
  if (m.includes("table")) {
    return "A table with that name already exists.";
  }
  if (m.includes("field")) {
    return "A field with that name already exists.";
  }
  if (m.includes("domain")) {
    return "A domain value with that combination already exists for this catalog.";
  }
  return "A reference catalog, table, or field with that name already exists.";
}

function resultTypeLabel(t: string): string {
  switch (t) {
    case "Table":
      return "Table";
    case "Field":
      return "Field";
    case "DomainValue":
      return "Domain value";
    default:
      return t;
  }
}

export default function SapReferencePage() {
  const { getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(
    () =>
      getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      }),
    [getAccessTokenSilently],
  );

  const [sources, setSources] = useState<SapReferenceSourceResponse[]>([]);
  const [sourcesLoading, setSourcesLoading] = useState(true);
  const [sourcesError, setSourcesError] = useState<string | null>(null);
  const [selectedSourceId, setSelectedSourceId] = useState<number | "">("");

  const [tables, setTables] = useState<SapTableMetadataResponse[]>([]);
  const [tablesLoading, setTablesLoading] = useState(false);
  const [tablesError, setTablesError] = useState<string | null>(null);
  const [selectedTableId, setSelectedTableId] = useState<number | "">("");

  const [fields, setFields] = useState<SapFieldMetadataResponse[]>([]);
  const [fieldsLoading, setFieldsLoading] = useState(false);
  const [fieldsError, setFieldsError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState("");
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [searchResults, setSearchResults] = useState<SapReferenceSearchResultDto[]>([]);

  const [sourceDraft, setSourceDraft] = useState<CreateSapReferenceSourceInput>({
    name: "",
    description: "",
    sourceType: "Manual",
    isEnabled: true,
  });
  const [tableDraft, setTableDraft] = useState<CreateSapTableInput>({
    tableName: "",
    description: "",
    module: "",
    businessObject: "",
    dataDomain: "",
    isCustom: false,
    notes: "",
  });
  const [fieldDraft, setFieldDraft] = useState<CreateSapFieldInput>({
    fieldName: "",
    description: "",
    dataElement: "",
    domainName: "",
    dataType: "",
    length: null,
    isKey: false,
    isRequired: null,
    isCustom: null,
    businessMeaning: "",
    exampleValue: "",
    notes: "",
  });

  const [deletingSource, setDeletingSource] = useState(false);
  const [deletingTable, setDeletingTable] = useState(false);
  const [deletingFieldId, setDeletingFieldId] = useState<number | null>(null);

  const [savingSource, setSavingSource] = useState(false);
  const [savingTable, setSavingTable] = useState(false);
  const [savingField, setSavingField] = useState(false);
  const [banner, setBanner] = useState<{ type: "ok" | "err"; text: string } | null>(null);
  const [wizardStep, setWizardStep] = useState<WizardStep>("catalog");

  const loadSources = useCallback(async (): Promise<SapReferenceSourceResponse[]> => {
    setSourcesLoading(true);
    setSourcesError(null);
    try {
      const token = await getToken();
      const list = await sapReferenceService.listSources(token);
      setSources(list);
      return list;
    } catch (e) {
      setSourcesError(getUserFacingErrorMessage(e, "Unable to load reference catalogs."));
      setSources([]);
      return [];
    } finally {
      setSourcesLoading(false);
    }
  }, [getToken]);

  const executeSearchQuery = useCallback(
    async (query: string) => {
      setSearchLoading(true);
      setSearchError(null);
      try {
        const token = await getToken();
        const hits = await sapReferenceService.search(token, query);
        setSearchResults(hits);
      } catch (e) {
        setSearchError(getUserFacingErrorMessage(e, "Search failed."));
        setSearchResults([]);
      } finally {
        setSearchLoading(false);
      }
    },
    [getToken],
  );

  const runSearch = useCallback(async () => {
    const q = searchQuery.trim();
    if (!q) {
      setSearchError("Enter a search term.");
      return;
    }
    await executeSearchQuery(q);
  }, [executeSearchQuery, searchQuery]);

  const refreshSearchIfActive = useCallback(async () => {
    const q = searchQuery.trim();
    if (!q) {
      return;
    }
    await executeSearchQuery(q);
  }, [executeSearchQuery, searchQuery]);

  const loadTables = useCallback(
    async (sourceId: number, opts?: { keepTableId?: number }) => {
      setTablesLoading(true);
      setTablesError(null);
      try {
        const token = await getToken();
        const list = await sapReferenceService.listTables(token, sourceId);
        setTables(list);
        const keepIdRaw = opts?.keepTableId;
        if (keepIdRaw === undefined) {
          setSelectedTableId("");
          setFields([]);
        } else {
          const stillThere = list.some((t) => t.id === keepIdRaw);
          if (!stillThere) {
            setSelectedTableId("");
            setFields([]);
          }
        }
      } catch (e) {
        setTablesError(getUserFacingErrorMessage(e, "Unable to load tables."));
        setTables([]);
      } finally {
        setTablesLoading(false);
      }
    },
    [getToken],
  );

  const loadFields = useCallback(
    async (tableId: number) => {
      setFieldsLoading(true);
      setFieldsError(null);
      try {
        const token = await getToken();
        const list = await sapReferenceService.listFields(token, tableId);
        setFields(list);
      } catch (e) {
        setFieldsError(getUserFacingErrorMessage(e, "Unable to load fields."));
        setFields([]);
      } finally {
        setFieldsLoading(false);
      }
    },
    [getToken],
  );

  useEffect(() => {
    void loadSources();
  }, [loadSources]);

  useEffect(() => {
    if (!banner || banner.type !== "ok") {
      return;
    }
    const t = window.setTimeout(() => setBanner(null), 4500);
    return () => window.clearTimeout(t);
  }, [banner]);

  useEffect(() => {
    if (selectedSourceId === "") {
      setTables([]);
      setSelectedTableId("");
      setFields([]);
      return;
    }
    void loadTables(selectedSourceId);
  }, [selectedSourceId, loadTables]);

  useEffect(() => {
    if (selectedTableId === "") {
      setFields([]);
      return;
    }
    void loadFields(selectedTableId);
  }, [selectedTableId, loadFields]);

  useEffect(() => {
    if (selectedSourceId === "" && (wizardStep === "table" || wizardStep === "field")) {
      setWizardStep("catalog");
    }
  }, [selectedSourceId, wizardStep]);

  useEffect(() => {
    if (wizardStep === "field" && selectedTableId === "" && selectedSourceId !== "") {
      setWizardStep("table");
    }
  }, [wizardStep, selectedTableId, selectedSourceId]);

  const createSource = async () => {
    if (!sourceDraft.name?.trim()) {
      setBanner({ type: "err", text: "Reference catalog name is required." });
      return;
    }
    setSavingSource(true);
    try {
      const token = await getToken();
      const created = await sapReferenceService.createSource(token, {
        ...sourceDraft,
        name: sourceDraft.name.trim(),
      });
      setBanner({ type: "ok", text: "Reference catalog created." });
      setSelectedSourceId(created.id);
      await loadSources();
      setSourceDraft({
        name: "",
        description: "",
        sourceType: "Manual",
        isEnabled: true,
      });
      void refreshSearchIfActive();
      setWizardStep("table");
    } catch (e) {
      setBanner({ type: "err", text: formatSapReferenceCreateError(e, "Unable to create reference catalog.") });
    } finally {
      setSavingSource(false);
    }
  };

  const createTable = async () => {
    if (selectedSourceId === "") {
      return;
    }
    if (!tableDraft.tableName?.trim()) {
      setBanner({ type: "err", text: "Table name is required." });
      return;
    }
    setSavingTable(true);
    try {
      const token = await getToken();
      const created = await sapReferenceService.createTable(token, selectedSourceId, {
        ...tableDraft,
        tableName: tableDraft.tableName.trim(),
      });
      setBanner({ type: "ok", text: "Table created." });
      await loadTables(selectedSourceId);
      setSelectedTableId(created.id);
      setTableDraft({
        tableName: "",
        description: "",
        module: "",
        businessObject: "",
        dataDomain: "",
        isCustom: false,
        notes: "",
      });
      void refreshSearchIfActive();
      setWizardStep("field");
    } catch (e) {
      setBanner({ type: "err", text: formatSapReferenceCreateError(e, "Unable to create table.") });
    } finally {
      setSavingTable(false);
    }
  };

  const createField = async () => {
    if (selectedTableId === "") {
      return;
    }
    if (!fieldDraft.fieldName?.trim()) {
      setBanner({ type: "err", text: "Field name is required." });
      return;
    }
    setSavingField(true);
    try {
      const token = await getToken();
      await sapReferenceService.createField(token, selectedTableId, {
        ...fieldDraft,
        fieldName: fieldDraft.fieldName.trim(),
      });
      setBanner({ type: "ok", text: "Field created." });
      await loadFields(selectedTableId);
      void loadTables(selectedSourceId as number, { keepTableId: selectedTableId as number });
      setFieldDraft({
        fieldName: "",
        description: "",
        dataElement: "",
        domainName: "",
        dataType: "",
        length: null,
        isKey: false,
        isRequired: null,
        isCustom: null,
        businessMeaning: "",
        exampleValue: "",
        notes: "",
      });
      void refreshSearchIfActive();
    } catch (e) {
      setBanner({ type: "err", text: formatSapReferenceCreateError(e, "Unable to create field.") });
    } finally {
      setSavingField(false);
    }
  };

  const confirmDeleteSource = async () => {
    if (selectedSourceId === "") {
      return;
    }
    if (
      !window.confirm(
        "Deleting this reference catalog removes its tables, fields, and domain values from Cortex only. It does not affect SAP.\n\nThis cannot be undone.",
      )
    ) {
      return;
    }
    const id = selectedSourceId as number;
    setDeletingSource(true);
    try {
      const token = await getToken();
      await sapReferenceService.deleteSource(token, id);
      setBanner({ type: "ok", text: "Reference catalog deleted." });
      const list = await loadSources();
      setSelectedSourceId(list[0]?.id ?? "");
      setWizardStep("catalog");
      if (list.length === 0) {
        setSelectedTableId("");
        setTables([]);
        setFields([]);
      }
      void refreshSearchIfActive();
    } catch (e) {
      setBanner({ type: "err", text: getUserFacingErrorMessage(e, "Unable to delete reference catalog.") });
    } finally {
      setDeletingSource(false);
    }
  };

  const confirmDeleteTable = async () => {
    if (selectedTableId === "" || selectedSourceId === "") {
      return;
    }
    if (
      !window.confirm(
        "Deleting this SAP table removes its fields from Cortex only. It does not affect SAP.\n\nThis cannot be undone.",
      )
    ) {
      return;
    }
    const srcId = selectedSourceId as number;
    const tid = selectedTableId as number;
    setDeletingTable(true);
    try {
      const token = await getToken();
      await sapReferenceService.deleteTable(token, tid);
      setBanner({ type: "ok", text: "Table deleted." });
      setSelectedTableId("");
      setFields([]);
      setWizardStep("table");
      await loadTables(srcId);
      void refreshSearchIfActive();
    } catch (e) {
      setBanner({ type: "err", text: getUserFacingErrorMessage(e, "Unable to delete table.") });
    } finally {
      setDeletingTable(false);
    }
  };

  const confirmDeleteField = async (fieldId: number) => {
    if (
      !window.confirm(
        "Deleting this SAP field removes it from Cortex only. It does not affect SAP.\n\nThis cannot be undone.",
      )
    ) {
      return;
    }
    setDeletingFieldId(fieldId);
    try {
      const token = await getToken();
      await sapReferenceService.deleteField(token, fieldId);
      setBanner({ type: "ok", text: "Field deleted." });
      if (selectedTableId !== "" && selectedSourceId !== "") {
        await loadFields(selectedTableId as number);
        await loadTables(selectedSourceId as number, { keepTableId: selectedTableId as number });
      }
      void refreshSearchIfActive();
    } catch (e) {
      setBanner({ type: "err", text: getUserFacingErrorMessage(e, "Unable to delete field.") });
    } finally {
      setDeletingFieldId(null);
    }
  };

  const toggleSourceEnabled = async (id: number, enabled: boolean) => {
    try {
      const token = await getToken();
      await sapReferenceService.setSourceEnabled(token, id, enabled);
      await loadSources();
      setBanner({ type: "ok", text: enabled ? "Reference catalog enabled." : "Reference catalog disabled." });
      void refreshSearchIfActive();
    } catch (e) {
      setBanner({ type: "err", text: getUserFacingErrorMessage(e, "Unable to update reference catalog.") });
    }
  };

  const selectedCatalog = sources.find((s) => s.id === selectedSourceId);
  const selectedTableMeta =
    selectedTableId === "" ? undefined : tables.find((t) => t.id === selectedTableId);

  return (
    <div className="min-w-0 max-w-full space-y-6">
      {banner ? (
        <div
          className={
            banner.type === "ok"
              ? "rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-900 dark:border-green-900 dark:bg-green-950/40 dark:text-green-100"
              : "rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/40 dark:text-red-100"
          }
        >
          {banner.text}
        </div>
      ) : null}

      <ConfigPageShell>
        <ConfigPageHeader
          title="SAP reference"
          description="Store SAP table, field, and domain reference knowledge for future ticket intelligence. This is metadata only—Cortex does not connect to SAP systems or run live OData, RFC, or BAPI calls from here."
        />
        <ConfigPageBody>
          <div className="space-y-6">
            <ConfigDetailCard
              className="!p-5"
              title="Search SAP Reference Knowledge"
              subtitle="Tables, fields, domain values, and notes stored in Cortex."
            >
            <div className="flex min-w-0 flex-col gap-3 sm:flex-row sm:items-end">
              <div className="min-w-0 flex-1">
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                  Search SAP tables, fields, domains…
                </label>
                <input
                  className={configFieldClass}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      void runSearch();
                    }
                  }}
                  placeholder="e.g. MARC, YYNGM_ACTIVE, Material Master"
                  autoComplete="off"
                />
              </div>
              <ConfigPrimaryButton onClick={() => void runSearch()} disabled={searchLoading}>
                {searchLoading ? "Searching…" : "Search"}
              </ConfigPrimaryButton>
            </div>
            {searchError ? (
              <p className="mt-2 text-sm text-amber-800 dark:text-amber-200/90">{searchError}</p>
            ) : null}
            {searchResults.length > 0 ? (
              <ul className="mt-5 space-y-3">
                {searchResults.map((r, idx) => (
                  <li
                    key={`${r.resultType}-${r.tableId ?? ""}-${r.fieldId ?? ""}-${r.domainValueId ?? ""}-${idx}`}
                    className="rounded-lg border border-gray-200 bg-white/90 px-4 py-4 dark:border-slate-700 dark:bg-slate-900/60"
                  >
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-semibold text-gray-800 dark:bg-slate-800 dark:text-slate-200">
                        {resultTypeLabel(r.resultType)}
                      </span>
                      {r.isCustom ? (
                        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-950 dark:bg-amber-900/40 dark:text-amber-100">
                          Custom field
                        </span>
                      ) : null}
                    </div>
                    <p className="mt-1 text-base font-semibold text-gray-900 dark:text-slate-100">{r.title}</p>
                    {r.subtitle?.trim() ? (
                      <p className="text-sm text-gray-600 dark:text-slate-400">{r.subtitle}</p>
                    ) : null}
                    {r.description?.trim() ? (
                      <p className="mt-1 text-sm text-gray-700 dark:text-slate-300">{r.description}</p>
                    ) : null}
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-600 dark:text-slate-400">
                      {r.module?.trim() ? <span>Module: {r.module}</span> : null}
                      {r.businessObject?.trim() ? <span>Business object: {r.businessObject}</span> : null}
                      <span>Reference catalog: {r.sourceName}</span>
                    </div>
                    <p className="mt-2 text-xs text-gray-500 dark:text-slate-500">{r.relevanceReason}</p>
                  </li>
                ))}
              </ul>
            ) : null}
          </ConfigDetailCard>

          <ConfigDetailCard
            className="!p-5"
            title="Catalog management"
            subtitle="Guided wizard: choose a catalog, add SAP tables, then add fields one step at a time."
          >
            {sourcesLoading ? (
              <p className="text-sm text-gray-500">Loading reference catalogs…</p>
            ) : sourcesError ? (
              <p className="text-sm text-red-600 dark:text-red-400">{sourcesError}</p>
            ) : (
              <div className="overflow-hidden rounded-xl border border-gray-200 dark:border-slate-700">
                <div className="border-b border-gray-200 bg-gray-50/90 px-5 py-4 dark:border-slate-700 dark:bg-slate-800/60">
                  <p className="text-base font-semibold text-gray-900 dark:text-slate-100">
                    {wizardStep === "catalog" && "Step 1 of 3 — Choose reference catalog"}
                    {wizardStep === "table" && "Step 2 of 3 — Add or select SAP table"}
                    {wizardStep === "field" &&
                      `Step 3 of 3 — Fields for ${selectedTableMeta?.tableName ?? "…"}`}
                  </p>
                  <div className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-gray-600 dark:text-slate-400">
                    <span
                      className={
                        wizardStep === "catalog"
                          ? "font-semibold text-gray-900 dark:text-slate-100"
                          : "text-gray-500 dark:text-slate-500"
                      }
                    >
                      {wizardStep === "catalog" ? "Catalog · current" : "Catalog ✓"}
                    </span>
                    <span className="text-gray-400 dark:text-slate-600" aria-hidden>
                      →
                    </span>
                    <span
                      className={
                        wizardStep === "table"
                          ? "font-semibold text-gray-900 dark:text-slate-100"
                          : wizardStep === "field"
                            ? "text-gray-500 dark:text-slate-500"
                            : "text-gray-400 dark:text-slate-600"
                      }
                    >
                      {wizardStep === "field" ? "Table ✓" : wizardStep === "table" ? "Table · current" : "Table"}
                    </span>
                    <span className="text-gray-400 dark:text-slate-600" aria-hidden>
                      →
                    </span>
                    <span
                      className={
                        wizardStep === "field"
                          ? "font-semibold text-gray-900 dark:text-slate-100"
                          : "text-gray-400 dark:text-slate-600"
                      }
                    >
                      {wizardStep === "field" ? "Fields · current" : "Fields"}
                    </span>
                  </div>
                </div>

                <div className="p-5">
                  {wizardStep === "catalog" ? (
                    <div className="space-y-4">
                      <p className="text-sm leading-relaxed text-gray-600 dark:text-slate-400">
                        A reference catalog groups SAP metadata from one system, export, project, or client. SAP
                        tables like MARC, EINA, and LFA1 should be added as tables in the next step.
                      </p>
                      {sources.length === 0 ? (
                        <p className="rounded-lg border border-dashed border-amber-200 bg-amber-50/80 px-4 py-3 text-sm text-amber-950 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-100">
                          Create a reference catalog before adding SAP tables.
                        </p>
                      ) : null}
                      <div>
                        <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                          Reference catalog
                        </label>
                        <select
                          className={`${configFieldClass} max-w-full`}
                          value={selectedSourceId === "" ? "" : String(selectedSourceId)}
                          onChange={(e) => {
                            const v = e.target.value;
                            setSelectedSourceId(v ? Number(v) : "");
                            setWizardStep("catalog");
                          }}
                        >
                          <option value="">— Select a reference catalog —</option>
                          {sources.map((s) => (
                            <option
                              key={s.id}
                              value={s.id}
                              title={`${s.name}${!s.isEnabled ? " (disabled)" : ""}`}
                            >
                              {s.name}
                              {!s.isEnabled ? " (disabled)" : ""}
                            </option>
                          ))}
                        </select>
                      </div>

                      <details className="rounded-lg border border-gray-200 bg-gray-50/80 dark:border-slate-700 dark:bg-slate-800/40">
                        <summary className="cursor-pointer select-none px-4 py-3 text-sm font-medium text-gray-800 dark:text-slate-200">
                          Create new reference catalog
                        </summary>
                        <div className="border-t border-gray-200 px-4 py-4 dark:border-slate-700">
                          <p className="mb-3 text-xs text-gray-500 dark:text-slate-500">
                            Use this only when you need a new catalog (for example a separate project or system
                            label). SAP tables are added in step 2.
                          </p>
                          <div className="grid gap-3 md:grid-cols-2">
                            <div className="md:col-span-2">
                              <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                Name
                              </label>
                              <input
                                className={configFieldClass}
                                placeholder="e.g. Syniti SAP Metadata Export"
                                value={sourceDraft.name ?? ""}
                                onChange={(e) => setSourceDraft({ ...sourceDraft, name: e.target.value })}
                              />
                            </div>
                            <div className="md:col-span-2">
                              <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                Description
                              </label>
                              <input
                                className={configFieldClass}
                                placeholder="e.g. SAP metadata exported from project reference files"
                                value={sourceDraft.description ?? ""}
                                onChange={(e) => setSourceDraft({ ...sourceDraft, description: e.target.value })}
                              />
                            </div>
                          </div>
                          <div className="mt-4">
                            <ConfigSecondaryButton
                              onClick={() => void createSource()}
                              disabled={savingSource}
                              className="border-cortex-blue/40 text-cortex-blue hover:bg-blue-50 dark:border-cortex-blue/50 dark:hover:bg-slate-800"
                            >
                              {savingSource ? "Creating reference catalog…" : "Create reference catalog"}
                            </ConfigSecondaryButton>
                          </div>
                        </div>
                      </details>

                      {selectedSourceId !== "" ? (
                        <div className="space-y-2 rounded-lg border border-amber-100 bg-amber-50/60 px-4 py-3 dark:border-amber-900/40 dark:bg-amber-950/30">
                          <p className="text-xs text-amber-950/90 dark:text-amber-100/90">
                            Deleting this reference catalog removes its tables, fields, and domain values from
                            Cortex only. It does not affect SAP.
                          </p>
                          <div className="flex flex-wrap gap-3">
                            <ConfigSecondaryButton
                              onClick={() => {
                                const s = sources.find((x) => x.id === selectedSourceId);
                                if (s) {
                                  void toggleSourceEnabled(s.id, !s.isEnabled);
                                }
                              }}
                            >
                              Toggle catalog enabled
                            </ConfigSecondaryButton>
                            <ConfigSecondaryButton
                              className="border-red-300 text-red-800 hover:bg-red-50 dark:border-red-800 dark:text-red-200 dark:hover:bg-red-950/40"
                              disabled={deletingSource}
                              onClick={() => void confirmDeleteSource()}
                            >
                              {deletingSource ? "Deleting…" : "Delete reference catalog"}
                            </ConfigSecondaryButton>
                          </div>
                        </div>
                      ) : null}
                    </div>
                  ) : null}

                  {wizardStep === "table" && selectedSourceId !== "" ? (
                    <div className="space-y-4">
                      <p className="text-sm leading-relaxed text-gray-600 dark:text-slate-400">
                        Add SAP tables such as MARC, EINA, LFA1, or KNA1 to the selected reference catalog.
                      </p>
                      <p className="text-sm font-medium text-gray-800 dark:text-slate-200">
                        Catalog:{" "}
                        <span className="font-normal text-gray-600 dark:text-slate-400">
                          {selectedCatalog?.name ?? "—"}
                        </span>
                      </p>
                      <div className="rounded-lg border border-gray-200 p-4 dark:border-slate-700 sm:p-5">
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <p className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
                            SAP table
                          </p>
                          {selectedTableId !== "" ? (
                            <ConfigSecondaryButton
                              className="border-red-300 text-red-800 hover:bg-red-50 dark:border-red-800 dark:text-red-200 dark:hover:bg-red-950/40"
                              disabled={deletingTable}
                              onClick={() => void confirmDeleteTable()}
                            >
                              {deletingTable ? "Deleting…" : "Delete table"}
                            </ConfigSecondaryButton>
                          ) : null}
                        </div>
                        {tablesLoading ? (
                          <p className="mt-2 text-sm text-gray-500">Loading tables…</p>
                        ) : tablesError ? (
                          <p className="mt-2 text-sm text-red-600 dark:text-red-400">{tablesError}</p>
                        ) : (
                          <>
                            <select
                              className={`${configFieldClass} mt-3 max-w-full`}
                              value={selectedTableId === "" ? "" : String(selectedTableId)}
                              onChange={(e) => {
                                const v = e.target.value;
                                setSelectedTableId(v ? Number(v) : "");
                              }}
                            >
                              <option value="">— Select a SAP table —</option>
                              {tables.map((t) => (
                                <option
                                  key={t.id}
                                  value={t.id}
                                  title={`${t.tableName} (${t.fieldCount} fields)`}
                                >
                                  {t.tableName} ({t.fieldCount} fields)
                                </option>
                              ))}
                            </select>
                            <div className="mt-4 grid gap-3 md:grid-cols-2">
                              <div>
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Table name
                                </label>
                                <input
                                  className={configFieldClass}
                                  placeholder="e.g. EINA"
                                  value={tableDraft.tableName ?? ""}
                                  onChange={(e) =>
                                    setTableDraft({ ...tableDraft, tableName: e.target.value })
                                  }
                                />
                              </div>
                              <div>
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Module
                                </label>
                                <input
                                  className={configFieldClass}
                                  placeholder="e.g. MM"
                                  value={tableDraft.module ?? ""}
                                  onChange={(e) => setTableDraft({ ...tableDraft, module: e.target.value })}
                                />
                              </div>
                              <div className="md:col-span-2">
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Description
                                </label>
                                <input
                                  className={configFieldClass}
                                  placeholder="e.g. Purchasing Info Record - General Data"
                                  value={tableDraft.description ?? ""}
                                  onChange={(e) =>
                                    setTableDraft({ ...tableDraft, description: e.target.value })
                                  }
                                />
                              </div>
                              <div className="md:col-span-2">
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Business object
                                </label>
                                <input
                                  className={configFieldClass}
                                  placeholder="e.g. Purchasing Info Record"
                                  value={tableDraft.businessObject ?? ""}
                                  onChange={(e) =>
                                    setTableDraft({ ...tableDraft, businessObject: e.target.value })
                                  }
                                />
                              </div>
                              <div className="md:col-span-2">
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Data domain
                                </label>
                                <input
                                  className={configFieldClass}
                                  placeholder="e.g. Procurement"
                                  value={tableDraft.dataDomain ?? ""}
                                  onChange={(e) =>
                                    setTableDraft({ ...tableDraft, dataDomain: e.target.value })
                                  }
                                />
                              </div>
                              <div className="md:col-span-2 space-y-2">
                                <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                                  <input
                                    type="checkbox"
                                    checked={tableDraft.isCustom ?? false}
                                    onChange={(e) =>
                                      setTableDraft({ ...tableDraft, isCustom: e.target.checked })
                                    }
                                    className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                                  />
                                  Custom table
                                </label>
                                <p className="text-xs text-gray-500 dark:text-slate-500">
                                  Use this for custom Z/Y tables. Custom fields like YYNGM_ACTIVE or ZZTEST_FLAG
                                  are added in the Fields step.
                                </p>
                              </div>
                              <div className="md:col-span-2">
                                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                  Notes
                                </label>
                                <input
                                  className={configFieldClass}
                                  value={tableDraft.notes ?? ""}
                                  onChange={(e) =>
                                    setTableDraft({ ...tableDraft, notes: e.target.value })
                                  }
                                />
                              </div>
                            </div>
                            <div className="mt-4">
                              <ConfigPrimaryButton onClick={() => void createTable()} disabled={savingTable}>
                                {savingTable ? "Adding table…" : "Add table"}
                              </ConfigPrimaryButton>
                            </div>
                          </>
                        )}
                      </div>
                    </div>
                  ) : null}

                  {wizardStep === "field" && selectedSourceId !== "" && selectedTableId !== "" ? (
                    <div className="space-y-4">
                      <p className="text-sm leading-relaxed text-gray-600 dark:text-slate-400">
                        Add standard or custom SAP fields for this table. Fields beginning with YY or ZZ are
                        marked as custom automatically.
                      </p>
                      <p className="text-xs text-gray-500 dark:text-slate-500">
                        YY* and ZZ* field names are marked custom automatically.
                      </p>
                      <div className="space-y-1 text-sm text-gray-800 dark:text-slate-200">
                        <p>
                          <span className="font-medium">Catalog:</span>{" "}
                          <span className="text-gray-600 dark:text-slate-400">
                            {selectedCatalog?.name ?? "—"}
                          </span>
                        </p>
                        <p>
                          <span className="font-medium">Table:</span>{" "}
                          <span className="font-mono text-gray-600 dark:text-slate-400">
                            {selectedTableMeta?.tableName ?? "—"}
                          </span>
                        </p>
                      </div>
                      {fieldsLoading ? (
                        <p className="text-sm text-gray-500">Loading fields…</p>
                      ) : fieldsError ? (
                        <p className="text-sm text-red-600 dark:text-red-400">{fieldsError}</p>
                      ) : (
                        <ul className="max-h-48 space-y-1 overflow-y-auto text-sm text-gray-700 dark:text-slate-300">
                          {fields.map((f) => (
                            <li
                              key={f.id}
                              className="flex items-start justify-between gap-2 border-b border-gray-100 py-2 dark:border-slate-800"
                            >
                              <div className="min-w-0 flex-1">
                                <span className="font-mono font-medium">{f.fieldName}</span>
                                {f.isCustom ? (
                                  <span className="ml-2 text-xs text-amber-700 dark:text-amber-300">
                                    custom
                                  </span>
                                ) : null}
                                {f.description ? (
                                  <span className="ml-2 text-gray-600 dark:text-slate-400">
                                    — {f.description}
                                  </span>
                                ) : null}
                              </div>
                              <button
                                type="button"
                                className="shrink-0 rounded border border-red-200 px-2 py-0.5 text-xs font-medium text-red-800 hover:bg-red-50 disabled:opacity-50 dark:border-red-900 dark:text-red-200 dark:hover:bg-red-950/40"
                                disabled={deletingFieldId === f.id}
                                onClick={() => void confirmDeleteField(f.id)}
                              >
                                {deletingFieldId === f.id ? "…" : "Delete"}
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                      <div className="rounded-lg border border-gray-200 p-4 dark:border-slate-700 sm:p-5">
                        <p className="text-xs text-gray-500 dark:text-slate-500">
                          Deleting a field removes it from Cortex only—not in SAP.
                        </p>
                        <div className="mt-4 grid gap-3 md:grid-cols-2">
                          <div>
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Field name
                            </label>
                            <input
                              className={configFieldClass}
                              placeholder="e.g. ZZTEST_FLAG"
                              value={fieldDraft.fieldName ?? ""}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, fieldName: e.target.value })}
                            />
                          </div>
                          <div>
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Data type / length
                            </label>
                            <div className="flex gap-2">
                              <input
                                className={configFieldClass}
                                placeholder="Type"
                                value={fieldDraft.dataType ?? ""}
                                onChange={(e) => setFieldDraft({ ...fieldDraft, dataType: e.target.value })}
                              />
                              <input
                                className={configFieldClass}
                                type="number"
                                placeholder="Len"
                                value={fieldDraft.length ?? ""}
                                onChange={(e) =>
                                  setFieldDraft({
                                    ...fieldDraft,
                                    length: e.target.value === "" ? null : Number(e.target.value),
                                  })
                                }
                              />
                            </div>
                          </div>
                          <div className="md:col-span-2">
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Description
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.description ?? ""}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, description: e.target.value })}
                            />
                          </div>
                          <div>
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Data element
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.dataElement ?? ""}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, dataElement: e.target.value })}
                            />
                          </div>
                          <div>
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Domain
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.domainName ?? ""}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, domainName: e.target.value })}
                            />
                          </div>
                          <div className="md:col-span-2">
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Business meaning
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.businessMeaning ?? ""}
                              onChange={(e) =>
                                setFieldDraft({ ...fieldDraft, businessMeaning: e.target.value })
                              }
                            />
                          </div>
                          <div className="md:col-span-2">
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Example value
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.exampleValue ?? ""}
                              onChange={(e) =>
                                setFieldDraft({ ...fieldDraft, exampleValue: e.target.value })
                              }
                            />
                          </div>
                          <div className="md:col-span-2">
                            <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                              Notes
                            </label>
                            <input
                              className={configFieldClass}
                              value={fieldDraft.notes ?? ""}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, notes: e.target.value })}
                            />
                          </div>
                          <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                            <input
                              type="checkbox"
                              checked={fieldDraft.isKey ?? false}
                              onChange={(e) => setFieldDraft({ ...fieldDraft, isKey: e.target.checked })}
                              className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                            />
                            Key field
                          </label>
                          <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                            <input
                              type="checkbox"
                              checked={fieldDraft.isRequired === true}
                              onChange={(e) =>
                                setFieldDraft({
                                  ...fieldDraft,
                                  isRequired: e.target.checked ? true : null,
                                })
                              }
                              className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                            />
                            Required
                          </label>
                          <label className="flex flex-wrap items-center gap-2 text-sm text-gray-800 dark:text-slate-200 md:col-span-2">
                            <input
                              type="checkbox"
                              checked={fieldDraft.isCustom === true}
                              onChange={(e) =>
                                setFieldDraft({
                                  ...fieldDraft,
                                  isCustom: e.target.checked ? true : null,
                                })
                              }
                              className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                            />
                            Custom field
                            <span className="text-xs font-normal text-gray-500 dark:text-slate-500">
                              (Optional override; leave unchecked for automatic detection.)
                            </span>
                          </label>
                        </div>
                        <div className="mt-5">
                          <ConfigPrimaryButton onClick={() => void createField()} disabled={savingField}>
                            {savingField ? "Adding field…" : "Add field"}
                          </ConfigPrimaryButton>
                        </div>
                      </div>
                    </div>
                  ) : null}
                </div>

                <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-200 bg-gray-50/50 px-5 py-4 dark:border-slate-700 dark:bg-slate-800/40">
                  {wizardStep === "catalog" ? (
                    <>
                      <span />
                      <ConfigPrimaryButton
                        disabled={selectedSourceId === "" || sources.length === 0}
                        onClick={() => setWizardStep("table")}
                      >
                        Continue to tables
                      </ConfigPrimaryButton>
                    </>
                  ) : null}
                  {wizardStep === "table" ? (
                    <>
                      <ConfigSecondaryButton onClick={() => setWizardStep("catalog")}>
                        Back to catalog
                      </ConfigSecondaryButton>
                      <ConfigPrimaryButton
                        disabled={selectedTableId === ""}
                        onClick={() => setWizardStep("field")}
                      >
                        Continue to fields
                      </ConfigPrimaryButton>
                    </>
                  ) : null}
                  {wizardStep === "field" ? (
                    <>
                      <div className="flex flex-wrap gap-2">
                        <ConfigSecondaryButton onClick={() => setWizardStep("table")}>
                          Back to tables
                        </ConfigSecondaryButton>
                        <ConfigSecondaryButton onClick={() => setWizardStep("catalog")}>
                          Choose another catalog
                        </ConfigSecondaryButton>
                      </div>
                      <ConfigSecondaryButton
                        onClick={() =>
                          setBanner({
                            type: "ok",
                            text: "You can keep editing this catalog or use search above.",
                          })
                        }
                      >
                        Done
                      </ConfigSecondaryButton>
                    </>
                  ) : null}
                </div>
              </div>
            )}
          </ConfigDetailCard>

          </div>
        </ConfigPageBody>
      </ConfigPageShell>
    </div>
  );
}
