import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type {
  CreateExternalWorkSourceInput,
  CreateIntegrationConnectionInput,
  CreateTicketFromExternalItemInput,
  CortexField,
  ExternalBoardMappingItemInput,
  ExternalBoardMappingMode,
  ExternalFieldMappingItemInput,
  ExternalSourceReadinessResponse,
  ExternalSourceSyncResponse,
  ExternalSourceType,
  ExternalWorkItemResponse,
  ExternalWorkSourceResponse,
  IntegrationAuthMode,
  IntegrationConnectionResponse,
  IntegrationProvider,
  IntegrationReadinessCheckStatus,
  IntegrationSyncMode,
  ManualUpsertExternalWorkItemInput,
  SharePointDiscoveredFieldResponse,
  UpdateExternalWorkSourceInput,
  UpdateIntegrationConnectionInput,
} from "../types/integrations";
import {
  AUTH_MODES,
  BOARD_MAPPING_MODES,
  CORTEX_FIELDS,
  EXTERNAL_TICKET_PRIORITIES,
  INTEGRATION_PROVIDERS,
  SOURCE_TYPES,
  SYNC_MODES,
} from "../types/integrations";
import { getUserFacingErrorMessage } from "../services/api";
import { integrationsService } from "../services/integrationsService";
import {
  ConfigDetailCard,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configFieldClass,
} from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

type IntegrationsTab = "connections" | "sources" | "fields" | "boards" | "items";

function Callout({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-lg border border-sky-200 bg-sky-50/90 px-4 py-3 text-sm text-sky-950 dark:border-sky-800 dark:bg-sky-950/40 dark:text-sky-100">
      <p className="font-medium text-sky-900 dark:text-sky-100">{title}</p>
      <div className="mt-1.5 text-sky-800 dark:text-sky-200/90">{children}</div>
    </div>
  );
}

function formatWhen(iso?: string | null): string {
  if (!iso) {
    return "—";
  }
  try {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    });
  } catch {
    return iso;
  }
}

function toDatetimeLocalInput(iso?: string | null): string {
  if (!iso?.trim()) {
    return "";
  }
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return "";
  }
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function normalizeExternalPriority(p?: string | null): string {
  if (!p?.trim()) {
    return "Medium";
  }
  const hit = EXTERNAL_TICKET_PRIORITIES.find(
    (x) => x.toLowerCase() === p.trim().toLowerCase(),
  );
  return hit ?? "Medium";
}

/** Readable label for a linked Cortex ticket id in integrations UI. */
function formatLinkedTicketDisplay(ticketId: string): string {
  const id = ticketId.trim();
  return id ? `Ticket #${id}` : "—";
}

/** Display labels only; API values stay as enum strings. */
function humanizeExternalSourceType(sourceType: ExternalSourceType): string {
  switch (sourceType) {
    case "SharePointList":
      return "SharePoint List";
    case "JiraProject":
      return "Jira Project";
    case "ServiceNowTable":
      return "ServiceNow Table";
    default:
      return sourceType;
  }
}

function humanizeIntegrationSyncMode(syncMode: IntegrationSyncMode): string {
  switch (syncMode) {
    case "ReadOnly":
      return "Read only";
    case "ImportToCortex":
      return "Import to Cortex";
    case "TwoWay":
      return "Two-way";
    default:
      return syncMode;
  }
}

function humanizeIntegrationAuthMode(authMode: IntegrationAuthMode): string {
  switch (authMode) {
    case "Manual":
      return "Manual";
    case "OAuth":
      return "OAuth";
    case "AppRegistration":
      return "App registration";
    default:
      return authMode;
  }
}

function humanizeExternalBoardMappingMode(mode: ExternalBoardMappingMode): string {
  switch (mode) {
    case "ReferenceOnly":
      return "Reference only";
    case "Import":
      return "Import";
    case "Mirror":
      return "Mirror";
    default:
      return mode;
  }
}

/** Readable Cortex field labels in dropdowns; values stay PascalCase enums. */
function humanizeCortexFieldDisplay(field: CortexField): string {
  return field.replace(/([A-Z])/g, " $1").trim();
}

function readinessHeadline(r: ExternalSourceReadinessResponse): string {
  if (r.canSync) {
    return "Ready for SharePoint discovery and read-only sync.";
  }
  if (r.canDiscoverFields) {
    return "Some setup is complete, but Cortex needs more information before sync.";
  }
  return "Setup required before live discovery or sync can run.";
}

function readinessCheckRowClass(status: IntegrationReadinessCheckStatus): string {
  switch (status) {
    case "Passed":
      return "text-green-800 dark:text-green-200/90";
    case "Warning":
      return "text-amber-900 dark:text-amber-100/90";
    case "Failed":
      return "text-red-800 dark:text-red-200/90";
    default:
      return "text-gray-800 dark:text-slate-200";
  }
}

function primaryReadinessHint(
  readiness: ExternalSourceReadinessResponse | null,
  which: "discover" | "sync",
): string | null {
  if (!readiness) {
    return null;
  }
  if (which === "discover" && readiness.canDiscoverFields) {
    return null;
  }
  if (which === "sync" && readiness.canSync) {
    return null;
  }
  const failed = readiness.checks.filter((c) => c.status === "Failed");
  if (failed.length > 0) {
    return failed.map((c) => c.message).join(" ");
  }
  if (which === "sync" && readiness.canDiscoverFields && !readiness.canSync) {
    const fm = readiness.checks.find((c) => c.key === "fieldMappings");
    return fm?.message ?? "Save field mappings before syncing.";
  }
  const warn = readiness.checks.find((c) => c.status === "Warning");
  return warn?.message ?? null;
}

function mappingRowIdentity(name: string, key?: string | null): string {
  const k = key?.trim();
  const n = name.trim();
  return (k || n).toLowerCase();
}

function humanizeConnectionSyncStatus(status?: string | null): string {
  if (!status?.trim()) {
    return "—";
  }
  return status.trim();
}

function DetailField({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="border-b border-gray-100 py-3 last:border-b-0 dark:border-slate-800">
      <div className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">{label}</div>
      <div className="mt-1 text-sm text-gray-900 dark:text-slate-100">{children}</div>
    </div>
  );
}

const emptyFieldRow = (): ExternalFieldMappingItemInput => ({
  externalFieldName: "",
  externalFieldKey: "",
  cortexField: "Title",
  isRequired: false,
  transformHint: "",
});

const emptyBoardRow = (): ExternalBoardMappingItemInput => ({
  boardId: 0,
  mappingMode: "ReferenceOnly",
  isDefault: false,
});

export interface IntegrationsPageProps {
  ticketBoards: TicketBoardDefinition[];
  ticketBoardLoading: boolean;
  onRefreshTicketBoards: () => void;
  onOpenCortexTicketById?: (ticketId: string) => void | Promise<void>;
}

export default function IntegrationsPage({
  ticketBoards,
  ticketBoardLoading,
  onRefreshTicketBoards,
  onOpenCortexTicketById,
}: IntegrationsPageProps) {
  const { getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(async () => {
    return getAccessTokenSilently({
      authorizationParams: { audience: API_AUDIENCE },
    });
  }, [getAccessTokenSilently]);

  const [tab, setTab] = useState<IntegrationsTab>("connections");
  const [banner, setBanner] = useState<{ type: "ok" | "err"; text: string } | null>(null);

  const [connections, setConnections] = useState<IntegrationConnectionResponse[]>([]);
  const [connectionsLoading, setConnectionsLoading] = useState(true);
  const [connectionsError, setConnectionsError] = useState<string | null>(null);

  const [selectedConnectionId, setSelectedConnectionId] = useState<number | null>(null);
  const [sources, setSources] = useState<ExternalWorkSourceResponse[]>([]);
  const [sourcesLoading, setSourcesLoading] = useState(false);
  const [sourcesError, setSourcesError] = useState<string | null>(null);

  const [selectedSourceId, setSelectedSourceId] = useState<number | null>(null);

  const [fieldDraft, setFieldDraft] = useState<ExternalFieldMappingItemInput[]>([]);
  const [fieldLoading, setFieldLoading] = useState(false);
  const [fieldSaving, setFieldSaving] = useState(false);
  const [fieldError, setFieldError] = useState<string | null>(null);

  const [boardDraft, setBoardDraft] = useState<ExternalBoardMappingItemInput[]>([]);
  const [boardLoading, setBoardLoading] = useState(false);
  const [boardSaving, setBoardSaving] = useState(false);
  const [boardError, setBoardError] = useState<string | null>(null);

  const [items, setItems] = useState<ExternalWorkItemResponse[]>([]);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [itemDetail, setItemDetail] = useState<ExternalWorkItemResponse | null>(null);

  const [createTicketOpen, setCreateTicketOpen] = useState(false);
  const [createTicketFor, setCreateTicketFor] = useState<ExternalWorkItemResponse | null>(null);
  const [createTicketDraft, setCreateTicketDraft] = useState({
    title: "",
    description: "",
    boardId: "" as number | "",
    priority: "Medium",
    dueDateUtc: "",
    department: "",
    category: "",
    requester: "",
    assignedTo: "",
  });
  const [createTicketSaving, setCreateTicketSaving] = useState(false);
  const [createTicketError, setCreateTicketError] = useState<string | null>(null);

  const [connectionModal, setConnectionModal] = useState<
    | { mode: "create"; draft: CreateIntegrationConnectionInput }
    | { mode: "edit"; id: number; draft: UpdateIntegrationConnectionInput & { provider: IntegrationProvider } }
    | null
  >(null);

  const [sourceModal, setSourceModal] = useState<
    | { mode: "create"; draft: CreateExternalWorkSourceInput }
    | { mode: "edit"; id: number; draft: UpdateExternalWorkSourceInput & { provider: IntegrationProvider; sourceType: ExternalSourceType; externalSourceId: string } }
    | null
  >(null);

  const [upsertOpen, setUpsertOpen] = useState(false);
  const [upsertDraft, setUpsertDraft] = useState<ManualUpsertExternalWorkItemInput>({
    externalItemId: "",
    title: "",
    externalUrl: "",
    description: "",
    status: "",
    priority: "",
    requester: "",
    assignedTo: "",
    department: "",
    category: "",
    dueDateUtc: "",
    lastModifiedUtc: "",
    rawJson: "",
  });
  const [upsertSaving, setUpsertSaving] = useState(false);

  const [discoveredFields, setDiscoveredFields] = useState<SharePointDiscoveredFieldResponse[]>([]);
  const [discoverLoading, setDiscoverLoading] = useState(false);
  const [discoverError, setDiscoverError] = useState<string | null>(null);

  const [syncLoading, setSyncLoading] = useState(false);
  const [syncSummary, setSyncSummary] = useState<
    | { kind: "success"; data: ExternalSourceSyncResponse }
    | { kind: "error"; message: string }
    | null
  >(null);

  const [sourceReadiness, setSourceReadiness] = useState<ExternalSourceReadinessResponse | null>(null);
  const [readinessLoading, setReadinessLoading] = useState(false);
  const [readinessError, setReadinessError] = useState<string | null>(null);

  const selectedConnection = useMemo(
    () => connections.find((c) => c.id === selectedConnectionId) ?? null,
    [connections, selectedConnectionId],
  );

  const selectedSource = useMemo(
    () => sources.find((s) => s.id === selectedSourceId) ?? null,
    [sources, selectedSourceId],
  );

  const loadSourceReadiness = useCallback(
    async (sourceId: number) => {
      setReadinessLoading(true);
      setReadinessError(null);
      try {
        const token = await getToken();
        const r = await integrationsService.getSourceReadiness(token, sourceId);
        setSourceReadiness(r);
      } catch {
        setSourceReadiness(null);
        setReadinessError("Unable to check source readiness.");
      } finally {
        setReadinessLoading(false);
      }
    },
    [getToken],
  );

  const loadConnections = useCallback(async () => {
    setConnectionsLoading(true);
    setConnectionsError(null);
    try {
      const token = await getToken();
      const list = await integrationsService.listConnections(token);
      setConnections(list);
      setSelectedConnectionId((prev) => {
        if (prev !== null && list.some((c) => c.id === prev)) {
          return prev;
        }
        return list[0]?.id ?? null;
      });
    } catch (e) {
      setConnectionsError(getUserFacingErrorMessage(e, "Unable to load connections."));
    } finally {
      setConnectionsLoading(false);
    }
  }, [getToken]);

  const loadSources = useCallback(
    async (connectionId: number) => {
      setSourcesLoading(true);
      setSourcesError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.listSources(token, connectionId);
        setSources(list);
        setSelectedSourceId((prev) => {
          if (prev !== null && list.some((s) => s.id === prev)) {
            return prev;
          }
          return list[0]?.id ?? null;
        });
      } catch (e) {
        setSourcesError(getUserFacingErrorMessage(e, "Unable to load sources."));
        setSources([]);
        setSelectedSourceId(null);
      } finally {
        setSourcesLoading(false);
      }
    },
    [getToken],
  );

  const loadFieldMappings = useCallback(
    async (sourceId: number) => {
      setFieldLoading(true);
      setFieldError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.getFieldMappings(token, sourceId);
        setFieldDraft(
          list.map((m) => ({
            externalFieldName: m.externalFieldName,
            externalFieldKey: m.externalFieldKey ?? "",
            cortexField: m.cortexField,
            isRequired: m.isRequired,
            transformHint: m.transformHint ?? "",
          })),
        );
      } catch (e) {
        setFieldError(getUserFacingErrorMessage(e, "Unable to load field mappings."));
        setFieldDraft([]);
      } finally {
        setFieldLoading(false);
      }
    },
    [getToken],
  );

  const loadBoardMappings = useCallback(
    async (sourceId: number) => {
      setBoardLoading(true);
      setBoardError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.getBoardMappings(token, sourceId);
        setBoardDraft(
          list.map((m) => ({
            boardId: m.boardId,
            mappingMode: m.mappingMode,
            isDefault: m.isDefault,
          })),
        );
      } catch (e) {
        setBoardError(getUserFacingErrorMessage(e, "Unable to load board mappings."));
        setBoardDraft([]);
      } finally {
        setBoardLoading(false);
      }
    },
    [getToken],
  );

  const loadItems = useCallback(
    async (sourceId: number) => {
      setItemsLoading(true);
      setItemsError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.listWorkItems(token, sourceId);
        setItems(list);
      } catch (e) {
        setItemsError(getUserFacingErrorMessage(e, "Unable to load external work items."));
        setItems([]);
      } finally {
        setItemsLoading(false);
      }
    },
    [getToken],
  );

  useEffect(() => {
    void loadConnections();
  }, [loadConnections]);

  useEffect(() => {
    if (selectedConnectionId !== null) {
      void loadSources(selectedConnectionId);
    } else {
      setSources([]);
      setSelectedSourceId(null);
    }
  }, [selectedConnectionId, loadSources]);

  useEffect(() => {
    if (selectedSourceId === null) {
      setSourceReadiness(null);
      setReadinessError(null);
      return;
    }
    void loadSourceReadiness(selectedSourceId);
  }, [selectedSourceId, loadSourceReadiness]);

  useEffect(() => {
    if (tab === "fields" && selectedSourceId !== null) {
      void loadFieldMappings(selectedSourceId);
    }
  }, [tab, selectedSourceId, loadFieldMappings]);

  useEffect(() => {
    if (tab === "boards" && selectedSourceId !== null) {
      void loadBoardMappings(selectedSourceId);
      if (ticketBoards.length === 0 && !ticketBoardLoading) {
        void onRefreshTicketBoards();
      }
    }
  }, [tab, selectedSourceId, loadBoardMappings, ticketBoards.length, ticketBoardLoading, onRefreshTicketBoards]);

  useEffect(() => {
    if (tab === "items" && selectedSourceId !== null) {
      void loadItems(selectedSourceId);
    }
  }, [tab, selectedSourceId, loadItems]);

  useEffect(() => {
    setDiscoveredFields([]);
    setDiscoverError(null);
    setSyncSummary(null);
  }, [selectedSourceId]);

  useEffect(() => {
    setItemDetail(null);
  }, [tab, selectedSourceId]);

  useEffect(() => {
    if (!itemDetail || itemsLoading) {
      return;
    }
    if (!items.some((i) => i.id === itemDetail.id)) {
      setItemDetail(null);
    }
  }, [items, itemsLoading, itemDetail]);

  useEffect(() => {
    if (!itemDetail) {
      return;
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setItemDetail(null);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [itemDetail]);

  const discoverActionDisabled =
    !selectedSourceId ||
    discoverLoading ||
    fieldLoading ||
    readinessLoading ||
    !!readinessError ||
    !sourceReadiness?.canDiscoverFields;

  const syncActionDisabled =
    !selectedSourceId ||
    syncLoading ||
    readinessLoading ||
    !!readinessError ||
    !sourceReadiness?.canSync;

  const showBanner = (type: "ok" | "err", text: string) => {
    setBanner({ type, text });
    window.setTimeout(() => setBanner(null), 6000);
  };

  const startCreateCortexTicket = useCallback(
    async (item: ExternalWorkItemResponse) => {
      if (selectedSourceId === null) {
        showBanner("err", "Select an external source first.");
        return;
      }
      setCreateTicketError(null);
      let defaultBoard: number | undefined;
      try {
        const token = await getToken();
        const maps = await integrationsService.getBoardMappings(token, selectedSourceId);
        defaultBoard = maps.find((m) => m.isDefault)?.boardId ?? maps[0]?.boardId;
      } catch {
        /* board dropdown still populated from ticketBoards */
      }
      const enabledBoards = ticketBoards.filter((b) => b.isEnabled);
      const boardId =
        defaultBoard !== undefined && enabledBoards.some((b) => b.id === defaultBoard)
          ? defaultBoard
          : (enabledBoards[0]?.id ?? "");
      setCreateTicketDraft({
        title: item.title ?? "",
        description: item.description ?? "",
        boardId,
        priority: normalizeExternalPriority(item.priority),
        dueDateUtc: toDatetimeLocalInput(item.dueDateUtc),
        department: item.department ?? "",
        category: item.category ?? "",
        requester: item.requester ?? "",
        assignedTo: item.assignedTo ?? "",
      });
      setCreateTicketFor(item);
      setCreateTicketOpen(true);
    },
    [getToken, selectedSourceId, ticketBoards],
  );

  const submitCreateCortexTicket = useCallback(async () => {
    if (!createTicketFor || selectedSourceId === null) {
      return;
    }
    if (createTicketDraft.boardId === "" || createTicketDraft.boardId === 0) {
      setCreateTicketError("Select a Cortex board.");
      return;
    }
    if (!createTicketDraft.title.trim()) {
      setCreateTicketError("Title is required.");
      return;
    }
    setCreateTicketSaving(true);
    setCreateTicketError(null);
    try {
      const token = await getToken();
      const body: CreateTicketFromExternalItemInput = {
        boardId: createTicketDraft.boardId,
        title: createTicketDraft.title.trim(),
        description: createTicketDraft.description.trim() || null,
        priority: createTicketDraft.priority,
        department: createTicketDraft.department.trim() || null,
        category: createTicketDraft.category.trim() || null,
        requester: createTicketDraft.requester.trim() || null,
        assignedTo: createTicketDraft.assignedTo.trim() || null,
        dueDateUtc: createTicketDraft.dueDateUtc
          ? new Date(createTicketDraft.dueDateUtc).toISOString()
          : null,
      };
      const result = await integrationsService.createTicketFromExternalItem(
        token,
        createTicketFor.id,
        body,
      );
      showBanner("ok", result.message);
      setCreateTicketOpen(false);
      setCreateTicketFor(null);
      await loadItems(selectedSourceId);
      setItemDetail(result.externalItem);
    } catch (e) {
      setCreateTicketError(
        getUserFacingErrorMessage(e, "Unable to create Cortex ticket from this external item."),
      );
    } finally {
      setCreateTicketSaving(false);
    }
  }, [
    createTicketDraft,
    createTicketFor,
    getToken,
    loadItems,
    selectedSourceId,
  ]);

  const openCreateConnection = () => {
    setConnectionModal({
      mode: "create",
      draft: {
        provider: "SharePoint",
        displayName: "",
        tenantId: "",
        organizationId: "",
        authMode: "Manual",
        syncMode: "ReadOnly",
        isEnabled: true,
      },
    });
  };

  const openEditConnection = (c: IntegrationConnectionResponse) => {
    setConnectionModal({
      mode: "edit",
      id: c.id,
      draft: {
        provider: c.provider,
        displayName: c.displayName,
        tenantId: c.tenantId ?? "",
        organizationId: c.organizationId ?? "",
        authMode: c.authMode,
        syncMode: c.syncMode,
        isEnabled: c.isEnabled,
      },
    });
  };

  const saveConnectionModal = async () => {
    if (!connectionModal) {
      return;
    }
    try {
      const token = await getToken();
      if (connectionModal.mode === "create") {
        const d = connectionModal.draft;
        if (!d.displayName.trim()) {
          showBanner("err", "Display name is required.");
          return;
        }
        await integrationsService.createConnection(token, {
          provider: d.provider,
          displayName: d.displayName.trim(),
          tenantId: d.tenantId?.trim() || null,
          organizationId: d.organizationId?.trim() || null,
          authMode: d.authMode ?? "Manual",
          syncMode: d.syncMode ?? "ReadOnly",
          isEnabled: d.isEnabled ?? true,
        });
        showBanner("ok", "Connection created.");
      } else {
        const d = connectionModal.draft;
        if (!d.displayName.trim()) {
          showBanner("err", "Display name is required.");
          return;
        }
        await integrationsService.updateConnection(token, connectionModal.id, {
          displayName: d.displayName.trim(),
          tenantId: d.tenantId?.trim() || null,
          organizationId: d.organizationId?.trim() || null,
          authMode: d.authMode ?? undefined,
          syncMode: d.syncMode ?? undefined,
          isEnabled: d.isEnabled ?? undefined,
        });
        showBanner("ok", "Connection updated.");
      }
      setConnectionModal(null);
      await loadConnections();
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save connection."));
    }
  };

  const toggleConnectionEnabled = async (c: IntegrationConnectionResponse) => {
    try {
      const token = await getToken();
      await integrationsService.setConnectionEnabled(token, c.id, !c.isEnabled);
      showBanner("ok", c.isEnabled ? "Connection disabled." : "Connection enabled.");
      await loadConnections();
      if (selectedConnectionId === c.id) {
        await loadSources(c.id);
      }
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to update connection."));
    }
  };

  const openCreateSource = () => {
    if (!selectedConnection) {
      showBanner("err", "Select a connection first.");
      return;
    }
    setSourceModal({
      mode: "create",
      draft: {
        provider: selectedConnection.provider,
        sourceType: "SharePointList",
        externalSourceId: "",
        name: "",
        externalUrl: "",
        isEnabled: true,
      },
    });
  };

  const openEditSource = (s: ExternalWorkSourceResponse) => {
    setSourceModal({
      mode: "edit",
      id: s.id,
      draft: {
        provider: s.provider,
        sourceType: s.sourceType,
        externalSourceId: s.externalSourceId,
        name: s.name,
        externalUrl: s.externalUrl ?? "",
        isEnabled: s.isEnabled,
      },
    });
  };

  const saveSourceModal = async () => {
    if (!sourceModal || selectedConnectionId === null) {
      return;
    }
    try {
      const token = await getToken();
      if (sourceModal.mode === "create") {
        const d = sourceModal.draft;
        if (!d.name.trim() || !d.externalSourceId.trim()) {
          showBanner("err", "Name and external source ID are required.");
          return;
        }
        await integrationsService.createSource(token, selectedConnectionId, {
          provider: d.provider,
          sourceType: d.sourceType,
          externalSourceId: d.externalSourceId.trim(),
          name: d.name.trim(),
          externalUrl: d.externalUrl?.trim() || null,
          isEnabled: d.isEnabled ?? true,
        });
        showBanner("ok", "Source created.");
      } else {
        const d = sourceModal.draft;
        if (!d.name.trim()) {
          showBanner("err", "Name is required.");
          return;
        }
        await integrationsService.updateSource(token, sourceModal.id, {
          name: d.name.trim(),
          externalUrl: d.externalUrl?.trim() || null,
          provider: d.provider,
          sourceType: d.sourceType,
          externalSourceId: d.externalSourceId.trim() || undefined,
          isEnabled: d.isEnabled ?? undefined,
        });
        showBanner("ok", "Source updated.");
      }
      setSourceModal(null);
      await loadSources(selectedConnectionId);
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save source."));
    }
  };

  const toggleSourceEnabled = async (s: ExternalWorkSourceResponse) => {
    if (selectedConnectionId === null) {
      return;
    }
    try {
      const token = await getToken();
      await integrationsService.setSourceEnabled(token, s.id, !s.isEnabled);
      showBanner("ok", s.isEnabled ? "Source disabled." : "Source enabled.");
      await loadSources(selectedConnectionId);
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to update source."));
    }
  };

  const saveFieldMappings = async () => {
    if (selectedSourceId === null) {
      return;
    }
    for (const row of fieldDraft) {
      if (!row.externalFieldName.trim()) {
        showBanner("err", "Each row needs an external field name.");
        return;
      }
    }
    setFieldSaving(true);
    try {
      const token = await getToken();
      const body = fieldDraft.map((row) => ({
        externalFieldName: row.externalFieldName.trim(),
        externalFieldKey: row.externalFieldKey?.trim() || null,
        cortexField: row.cortexField,
        isRequired: row.isRequired,
        transformHint: row.transformHint?.trim() || null,
      }));
      await integrationsService.replaceFieldMappings(token, selectedSourceId, body);
      showBanner("ok", "Field mappings saved.");
      await loadFieldMappings(selectedSourceId);
      void loadSourceReadiness(selectedSourceId);
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save field mappings."));
    } finally {
      setFieldSaving(false);
    }
  };

  const saveBoardMappings = async () => {
    if (selectedSourceId === null) {
      return;
    }
    for (const row of boardDraft) {
      if (!row.boardId || !ticketBoards.some((b) => b.id === row.boardId)) {
        showBanner("err", "Each row needs a valid Cortex board.");
        return;
      }
    }
    setBoardSaving(true);
    try {
      const token = await getToken();
      await integrationsService.replaceBoardMappings(token, selectedSourceId, boardDraft);
      showBanner("ok", "Board mappings saved.");
      await loadBoardMappings(selectedSourceId);
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save board mappings."));
    } finally {
      setBoardSaving(false);
    }
  };

  const submitUpsert = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (!upsertDraft.externalItemId.trim() || !upsertDraft.title.trim()) {
      showBanner("err", "External item ID and title are required.");
      return;
    }
    setUpsertSaving(true);
    try {
      const token = await getToken();
      const raw =
        upsertDraft.rawJson?.trim() ||
        JSON.stringify({
          externalItemId: upsertDraft.externalItemId.trim(),
          title: upsertDraft.title.trim(),
          description: upsertDraft.description?.trim() || undefined,
          status: upsertDraft.status?.trim() || undefined,
          priority: upsertDraft.priority?.trim() || undefined,
        });
      const body: ManualUpsertExternalWorkItemInput = {
        externalItemId: upsertDraft.externalItemId.trim(),
        title: upsertDraft.title.trim(),
        externalUrl: upsertDraft.externalUrl?.trim() || null,
        description: upsertDraft.description?.trim() || null,
        status: upsertDraft.status?.trim() || null,
        priority: upsertDraft.priority?.trim() || null,
        requester: upsertDraft.requester?.trim() || null,
        assignedTo: upsertDraft.assignedTo?.trim() || null,
        department: upsertDraft.department?.trim() || null,
        category: upsertDraft.category?.trim() || null,
        dueDateUtc: upsertDraft.dueDateUtc
          ? new Date(upsertDraft.dueDateUtc).toISOString()
          : null,
        lastModifiedUtc: upsertDraft.lastModifiedUtc
          ? new Date(upsertDraft.lastModifiedUtc).toISOString()
          : null,
        rawJson: raw,
      };
      await integrationsService.manualUpsertWorkItem(token, selectedSourceId, body);
      setUpsertOpen(false);
      showBanner("ok", "External work item saved.");
      await loadItems(selectedSourceId);
      setUpsertDraft({
        externalItemId: "",
        title: "",
        externalUrl: "",
        description: "",
        status: "",
        priority: "",
        requester: "",
        assignedTo: "",
        department: "",
        category: "",
        dueDateUtc: "",
        lastModifiedUtc: "",
        rawJson: "",
      });
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save work item."));
    } finally {
      setUpsertSaving(false);
    }
  };

  const discoverFieldsFromSource = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (
      readinessLoading ||
      readinessError ||
      !sourceReadiness?.canDiscoverFields ||
      discoverLoading ||
      fieldLoading
    ) {
      return;
    }
    setDiscoverLoading(true);
    setDiscoverError(null);
    try {
      const token = await getToken();
      const list = await integrationsService.discoverSharePointFields(token, selectedSourceId);
      setDiscoveredFields(list);
    } catch (e) {
      setDiscoverError(getUserFacingErrorMessage(e, "Unable to discover fields."));
      setDiscoveredFields([]);
    } finally {
      setDiscoverLoading(false);
      void loadSourceReadiness(selectedSourceId);
    }
  };

  const addDiscoveredFieldToMapping = (field: SharePointDiscoveredFieldResponse) => {
    const id = mappingRowIdentity(field.externalFieldName, field.externalFieldKey);
    if (!id) {
      return;
    }
    const exists = fieldDraft.some(
      (row) => mappingRowIdentity(row.externalFieldName, row.externalFieldKey) === id,
    );
    if (exists) {
      return;
    }
    const suggested = field.suggestedCortexField;
    const cortex: CortexField =
      suggested && CORTEX_FIELDS.includes(suggested) ? suggested : "Unknown";
    setFieldDraft([
      ...fieldDraft,
      {
        externalFieldName: field.externalFieldName,
        externalFieldKey: field.externalFieldKey?.trim() || "",
        cortexField: cortex,
        isRequired: false,
        transformHint: "",
      },
    ]);
  };

  const syncExternalSourceNow = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (readinessLoading || readinessError || !sourceReadiness?.canSync || syncLoading) {
      return;
    }
    setSyncLoading(true);
    setSyncSummary(null);
    try {
      const token = await getToken();
      const result = await integrationsService.syncSharePointSource(token, selectedSourceId);
      setSyncSummary({ kind: "success", data: result });
      await loadItems(selectedSourceId);
      await loadConnections();
      if (selectedConnectionId !== null) {
        await loadSources(selectedConnectionId);
      }
    } catch (e) {
      const msg = getUserFacingErrorMessage(e, "Sync failed.");
      setSyncSummary({ kind: "error", message: msg });
    } finally {
      setSyncLoading(false);
      void loadSourceReadiness(selectedSourceId);
    }
  };

  const selectSourceForMapping = (sourceId: number) => {
    setSelectedSourceId(sourceId);
    setTab("fields");
  };

  const tabButtons: { id: IntegrationsTab; label: string }[] = [
    { id: "connections", label: "Connections" },
    { id: "sources", label: "Sources" },
    { id: "fields", label: "Field mapping" },
    { id: "boards", label: "Board mapping" },
    { id: "items", label: "External items" },
  ];

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

      <section className="min-w-0 max-w-full rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">Integrations</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          Connect external work sources, map their fields to Cortex concepts, and inspect external work items before
          importing them into Cortex.
        </p>
      </section>

      <ConfigPageShell>
        <ConfigPageHeader
          title="External integrations"
          description="Connect SharePoint lists, Jira projects, or ServiceNow tables as external sources. For SharePoint Lists, you can discover fields, map them to Cortex, and run a read-only sync that updates external work items without changing SharePoint or creating Cortex tickets automatically."
        />
        <ConfigPageBody>
          <div className="flex min-w-0 max-w-full flex-wrap gap-2 border-b border-gray-200 pb-4 dark:border-slate-700">
            {tabButtons.map((b) => (
              <button
                key={b.id}
                type="button"
                onClick={() => setTab(b.id)}
                className={`rounded-lg px-4 py-2 text-sm font-medium transition ${
                  tab === b.id
                    ? "bg-cortex-blue text-white shadow-sm dark:bg-cortex-blue"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                {b.label}
              </button>
            ))}
          </div>

          <div className="mt-6 min-w-0 max-w-full space-y-6">
            {tab !== "connections" && (
              <ConfigDetailCard title="Selection" subtitle="Choose where mapping and items apply.">
                <div className="grid gap-4 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Connection</label>
                    <select
                      className={configFieldClass}
                      value={selectedConnectionId ?? ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        setSelectedConnectionId(v ? Number(v) : null);
                      }}
                      disabled={connectionsLoading || connections.length === 0}
                    >
                      <option value="">— Select —</option>
                      {connections.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.displayName}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External source</label>
                    <select
                      className={configFieldClass}
                      value={selectedSourceId ?? ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        setSelectedSourceId(v ? Number(v) : null);
                      }}
                      disabled={!selectedConnectionId || sourcesLoading || sources.length === 0}
                    >
                      <option value="">— Select —</option>
                      {sources.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                {selectedSourceId ? (
                  <div className="mt-4 rounded-lg border border-gray-200 bg-gray-50/90 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/50">
                    <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Source readiness</h3>
                    {readinessLoading ? (
                      <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                    ) : readinessError ? (
                      <p className="mt-2 text-sm text-amber-900 dark:text-amber-100/90">{readinessError}</p>
                    ) : sourceReadiness ? (
                      <>
                        <p className="mt-2 text-sm text-gray-800 dark:text-slate-200">{readinessHeadline(sourceReadiness)}</p>
                        <ul className="mt-3 space-y-2 text-xs">
                          {sourceReadiness.checks.map((c) => (
                            <li
                              key={c.key}
                              className={`flex gap-2 rounded-md px-2 py-1 ${readinessCheckRowClass(c.status)}`}
                            >
                              <span className="shrink-0 font-medium" aria-hidden>
                                {c.status === "Passed" ? "✓" : c.status === "Warning" ? "!" : "✗"}
                              </span>
                              <span className="min-w-0">
                                <span className="font-medium">{c.label}</span>
                                <span className="block text-gray-600 dark:text-slate-400">{c.message}</span>
                              </span>
                            </li>
                          ))}
                        </ul>
                      </>
                    ) : (
                      <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">No readiness data.</p>
                    )}
                  </div>
                ) : null}
              </ConfigDetailCard>
            )}

            {tab === "connections" && (
              <div className="space-y-4">
                <Callout title="Connections represent systems Cortex can read from.">
                  Live authentication is not connected yet. Manual mode lets Cortex model and test external sources safely.
                </Callout>
                <div className="flex justify-end">
                  <ConfigPrimaryButton onClick={openCreateConnection}>Add connection</ConfigPrimaryButton>
                </div>
                {connectionsError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{connectionsError}</p>
                ) : null}
                {connectionsLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading connections…</p>
                ) : connections.length === 0 ? (
                  <p className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    No connections yet. Add a connection to register an external system.
                  </p>
                ) : (
                  <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                    <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Name</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Provider</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Auth</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Sync</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Enabled</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Last sync</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Created</th>
                          <th className="px-4 py-3 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {connections.map((c) => (
                          <tr key={c.id} className="bg-white dark:bg-slate-900">
                            <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">{c.displayName}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{c.provider}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeIntegrationAuthMode(c.authMode)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeIntegrationSyncMode(c.syncMode)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{c.isEnabled ? "Yes" : "No"}</td>
                            <td
                              className="max-w-[220px] px-4 py-3 text-gray-700 dark:text-slate-300"
                              title={
                                c.lastSyncMessage?.trim()
                                  ? c.lastSyncMessage.trim()
                                  : c.lastSyncUtc
                                    ? undefined
                                    : "No sync has completed for this connection yet."
                              }
                            >
                              {!c.lastSyncUtc ? (
                                <span className="text-gray-600 dark:text-slate-400">Never synced</span>
                              ) : (
                                <div className="space-y-0.5">
                                  <div className="font-medium text-gray-900 dark:text-slate-100">
                                    {humanizeConnectionSyncStatus(c.lastSyncStatus)}
                                  </div>
                                  <div className="text-xs text-gray-600 dark:text-slate-400">
                                    {formatWhen(c.lastSyncUtc)}
                                  </div>
                                  {c.lastSyncMessage?.trim() ? (
                                    <div className="line-clamp-2 text-xs text-gray-500 dark:text-slate-500">
                                      {c.lastSyncMessage.trim()}
                                    </div>
                                  ) : null}
                                </div>
                              )}
                            </td>
                            <td className="px-4 py-3 text-gray-600 dark:text-slate-400">{formatWhen(c.createdAtUtc)}</td>
                            <td className="space-x-2 whitespace-nowrap px-4 py-3 text-right">
                              <ConfigGhostButton className="!py-1.5" onClick={() => openEditConnection(c)}>
                                Edit
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => void toggleConnectionEnabled(c)}>
                                {c.isEnabled ? "Disable" : "Enable"}
                              </ConfigGhostButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {tab === "sources" && (
              <div className="space-y-4">
                <Callout title="Sources represent boards, lists, projects, or tables inside those systems.">
                  A SharePoint list can act like a lightweight board. Cortex stores it as an external work source before creating
                  any Cortex tickets.
                </Callout>
                <div className="flex flex-wrap justify-end gap-2">
                  <ConfigSecondaryButton onClick={() => selectedConnectionId && void loadSources(selectedConnectionId)} disabled={!selectedConnectionId || sourcesLoading}>
                    Refresh sources
                  </ConfigSecondaryButton>
                  <ConfigPrimaryButton onClick={openCreateSource} disabled={!selectedConnectionId}>
                    Add source
                  </ConfigPrimaryButton>
                </div>
                {sourcesError ? <p className="text-sm text-red-600 dark:text-red-400">{sourcesError}</p> : null}
                {!selectedConnectionId ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">Select a connection above to manage sources.</p>
                ) : sourcesLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading sources…</p>
                ) : sources.length === 0 ? (
                  <p className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    No sources for this connection yet.
                  </p>
                ) : (
                  <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                    <table className="min-w-[960px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Name</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Type</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Provider</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">External ID</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">URL</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Enabled</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Created</th>
                          <th className="px-4 py-3 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {sources.map((s) => (
                          <tr key={s.id} className="bg-white dark:bg-slate-900">
                            <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">{s.name}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeExternalSourceType(s.sourceType)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{s.provider}</td>
                            <td className="max-w-[140px] truncate px-4 py-3 text-gray-700 dark:text-slate-300" title={s.externalSourceId}>
                              {s.externalSourceId}
                            </td>
                            <td className="max-w-[160px] truncate px-4 py-3 text-cortex-blue dark:text-cortex-cyan">
                              {s.externalUrl ? (
                                <a href={s.externalUrl} target="_blank" rel="noreferrer" className="hover:underline">
                                  Link
                                </a>
                              ) : (
                                "—"
                              )}
                            </td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{s.isEnabled ? "Yes" : "No"}</td>
                            <td className="px-4 py-3 text-gray-600 dark:text-slate-400">{formatWhen(s.createdAtUtc)}</td>
                            <td className="space-x-2 whitespace-nowrap px-4 py-3 text-right">
                              <ConfigGhostButton className="!py-1.5" onClick={() => selectSourceForMapping(s.id)}>
                                Map fields
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => openEditSource(s)}>
                                Edit
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => void toggleSourceEnabled(s)}>
                                {s.isEnabled ? "Disable" : "Enable"}
                              </ConfigGhostButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {tab === "fields" && (
              <div className="space-y-4">
                <div className="flex min-w-0 max-w-full flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/40">
                  <div className="flex flex-wrap items-center gap-3">
                    <ConfigPrimaryButton
                      onClick={() => void discoverFieldsFromSource()}
                      disabled={discoverActionDisabled}
                    >
                      {discoverLoading ? "Discovering…" : "Discover fields"}
                    </ConfigPrimaryButton>
                    <p className="min-w-0 flex-1 text-sm text-gray-600 dark:text-slate-400">
                      Read the source schema and suggest Cortex field mappings.
                    </p>
                  </div>
                  {!selectedSourceId ? (
                    <p className="text-xs text-gray-500 dark:text-slate-500">Select an external source above to enable discovery.</p>
                  ) : readinessLoading ? (
                    <p className="text-xs text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                  ) : readinessError ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">{readinessError}</p>
                  ) : primaryReadinessHint(sourceReadiness, "discover") ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">
                      {primaryReadinessHint(sourceReadiness, "discover")}
                    </p>
                  ) : null}
                </div>
                <Callout title="Mappings translate customer-specific fields into Cortex concepts.">
                  Saving replaces the full mapping list for this source. Add every field you need before saving.
                  <span className="mt-2 block text-sky-900/90 dark:text-sky-200/90">
                    Optional note for how Cortex should interpret values from this external field.
                  </span>
                </Callout>
                {discoverError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{discoverError}</p>
                ) : null}
                {discoveredFields.length > 0 ? (
                  <div className="min-w-0 max-w-full space-y-2 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
                    <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Discovered source fields</h3>
                    <p className="text-sm text-gray-600 dark:text-slate-400">
                      These fields were read from the external source. Review the suggestions before adding them to the
                      mapping table.
                    </p>
                    <div className="max-w-full overflow-x-auto rounded-lg border border-gray-100 dark:border-slate-800">
                      <table className="min-w-[720px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Source field</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Field key</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Type</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Suggested Cortex field</th>
                            <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Action</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {discoveredFields.map((f) => {
                            const label = f.displayName?.trim() || f.externalFieldName;
                            const keyDisp = f.externalFieldKey?.trim() || f.externalFieldName || "—";
                            const already = fieldDraft.some(
                              (row) =>
                                mappingRowIdentity(row.externalFieldName, row.externalFieldKey)
                                === mappingRowIdentity(f.externalFieldName, f.externalFieldKey),
                            );
                            return (
                              <tr key={`${f.externalFieldName}:${f.externalFieldKey ?? ""}`} className="bg-white dark:bg-slate-900">
                                <td className="px-3 py-2 text-gray-900 dark:text-slate-100" title={label}>
                                  {label}
                                  {f.isHidden ? (
                                    <span className="ml-2 text-xs text-gray-500 dark:text-slate-500">(hidden)</span>
                                  ) : null}
                                </td>
                                <td className="max-w-[180px] truncate px-3 py-2 font-mono text-xs text-gray-700 dark:text-slate-300" title={keyDisp}>
                                  {keyDisp}
                                </td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{f.type?.trim() || "—"}</td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">
                                  {f.suggestedCortexField
                                    ? humanizeCortexFieldDisplay(f.suggestedCortexField)
                                    : "—"}
                                </td>
                                <td className="px-3 py-2 text-right">
                                  {already ? (
                                    <span className="text-xs text-gray-500 dark:text-slate-500">Already mapped</span>
                                  ) : (
                                    <ConfigGhostButton className="!py-1" onClick={() => addDiscoveredFieldToMapping(f)}>
                                      Add to mapping
                                    </ConfigGhostButton>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : null}
                {!selectedSourceId ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">Select an external source above.</p>
                ) : fieldError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{fieldError}</p>
                ) : fieldLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading mappings…</p>
                ) : (
                  <>
                    <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                      <table className="min-w-[1200px] w-max max-w-none divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="min-w-[220px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">External name</th>
                            <th className="min-w-[220px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Key</th>
                            <th className="min-w-[190px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Cortex field</th>
                            <th className="w-[90px] min-w-[90px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Required</th>
                            <th className="min-w-[280px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Mapping note</th>
                            <th className="w-[100px] min-w-[100px] px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {fieldDraft.map((row, idx) => (
                            <tr key={idx} className="bg-white dark:bg-slate-900">
                              <td className="min-w-[220px] px-3 py-2 align-top">
                                <input
                                  className={configFieldClass}
                                  value={row.externalFieldName}
                                  title={row.externalFieldName || undefined}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, externalFieldName: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="min-w-[220px] px-3 py-2 align-top">
                                <input
                                  className={configFieldClass}
                                  value={row.externalFieldKey ?? ""}
                                  title={row.externalFieldKey?.trim() ? row.externalFieldKey : undefined}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, externalFieldKey: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="min-w-[190px] px-3 py-2 align-top">
                                <select
                                  className={`${configFieldClass} min-w-[190px]`}
                                  value={row.cortexField}
                                  title={humanizeCortexFieldDisplay(row.cortexField)}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, cortexField: e.target.value as CortexField };
                                    setFieldDraft(next);
                                  }}
                                >
                                  {CORTEX_FIELDS.map((f) => (
                                    <option key={f} value={f}>
                                      {humanizeCortexFieldDisplay(f)}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="w-[90px] min-w-[90px] px-3 py-2 align-top">
                                <input
                                  type="checkbox"
                                  checked={row.isRequired}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, isRequired: e.target.checked };
                                    setFieldDraft(next);
                                  }}
                                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                                />
                              </td>
                              <td className="min-w-[280px] px-3 py-2 align-top">
                                <input
                                  className={`${configFieldClass} min-w-[260px]`}
                                  value={row.transformHint ?? ""}
                                  title={row.transformHint ?? ""}
                                  placeholder="Example: Map P1 to Critical, P2 to High, P3 to Medium"
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, transformHint: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="w-[100px] min-w-[100px] whitespace-nowrap px-3 py-2 text-right align-top">
                                <ConfigGhostButton
                                  className="!py-1 text-red-600 dark:text-red-400"
                                  onClick={() => setFieldDraft(fieldDraft.filter((_, i) => i !== idx))}
                                >
                                  Remove
                                </ConfigGhostButton>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <ConfigSecondaryButton onClick={() => setFieldDraft([...fieldDraft, emptyFieldRow()])}>Add row</ConfigSecondaryButton>
                      <ConfigPrimaryButton onClick={() => void saveFieldMappings()} disabled={fieldSaving}>
                        {fieldSaving ? "Saving…" : "Save field mappings"}
                      </ConfigPrimaryButton>
                    </div>
                  </>
                )}
              </div>
            )}

            {tab === "boards" && (
              <div className="space-y-4">
                <Callout title="Board mapping tells Cortex where external work belongs conceptually.">
                  Reference-only mapping does not create Cortex tickets automatically.
                </Callout>
                {!selectedSourceId ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">Select an external source above.</p>
                ) : boardError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{boardError}</p>
                ) : boardLoading || ticketBoardLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading…</p>
                ) : ticketBoards.length === 0 ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">
                    No Cortex boards found. Define boards under Configuration → Boards first.
                  </p>
                ) : (
                  <>
                    <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                      <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Cortex board</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Mapping mode</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Default</th>
                            <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300" />
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {boardDraft.map((row, idx) => (
                            <tr key={idx} className="bg-white dark:bg-slate-900">
                              <td className="px-3 py-2">
                                <select
                                  className={configFieldClass}
                                  value={row.boardId || ticketBoards[0]?.id}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = { ...row, boardId: Number(e.target.value) };
                                    setBoardDraft(next);
                                  }}
                                >
                                  {ticketBoards.filter((b) => b.isEnabled).map((b) => (
                                    <option key={b.id} value={b.id}>
                                      {b.name}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="px-3 py-2">
                                <select
                                  className={configFieldClass}
                                  value={row.mappingMode}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = {
                                      ...row,
                                      mappingMode: e.target.value as ExternalBoardMappingMode,
                                    };
                                    setBoardDraft(next);
                                  }}
                                >
                                  {BOARD_MAPPING_MODES.map((m) => (
                                    <option key={m} value={m}>
                                      {humanizeExternalBoardMappingMode(m)}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="px-3 py-2">
                                <input
                                  type="checkbox"
                                  checked={row.isDefault}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = { ...row, isDefault: e.target.checked };
                                    setBoardDraft(next);
                                  }}
                                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                                />
                              </td>
                              <td className="px-3 py-2 text-right">
                                <ConfigGhostButton
                                  className="!py-1 text-red-600 dark:text-red-400"
                                  onClick={() => setBoardDraft(boardDraft.filter((_, i) => i !== idx))}
                                >
                                  Remove
                                </ConfigGhostButton>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <ConfigSecondaryButton
                        onClick={() => {
                          const b = ticketBoards.find((x) => x.isEnabled) ?? ticketBoards[0];
                          setBoardDraft([
                            ...boardDraft,
                            { ...emptyBoardRow(), boardId: b?.id ?? 0 },
                          ]);
                        }}
                        disabled={!ticketBoards.length}
                      >
                        Add row
                      </ConfigSecondaryButton>
                      <ConfigPrimaryButton onClick={() => void saveBoardMappings()} disabled={boardSaving}>
                        {boardSaving ? "Saving…" : "Save board mappings"}
                      </ConfigPrimaryButton>
                    </div>
                  </>
                )}
              </div>
            )}

            {tab === "items" && (
              <div className="min-w-0 max-w-full space-y-4">
                <Callout title="External items are stored safely before becoming Cortex tickets.">
                  No automatic Cortex ticket import runs from this screen. Add a manual test item or use Sync now for a
                  SharePoint List after mappings are saved.
                </Callout>
                <div className="flex min-w-0 max-w-full flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/40">
                  <div className="flex flex-wrap items-center gap-3">
                    <ConfigPrimaryButton
                      onClick={() => void syncExternalSourceNow()}
                      disabled={syncActionDisabled}
                    >
                      {syncLoading ? "Syncing…" : "Sync now"}
                    </ConfigPrimaryButton>
                    <p className="min-w-0 flex-1 text-sm text-gray-600 dark:text-slate-400">
                      Reads the selected external source and updates external work items. Cortex tickets are not created
                      automatically.
                    </p>
                  </div>
                  {!selectedSourceId ? (
                    <p className="text-xs text-gray-500 dark:text-slate-500">Select an external source above to run a sync.</p>
                  ) : readinessLoading ? (
                    <p className="text-xs text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                  ) : readinessError ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">{readinessError}</p>
                  ) : primaryReadinessHint(sourceReadiness, "sync") ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">
                      {primaryReadinessHint(sourceReadiness, "sync")}
                    </p>
                  ) : null}
                </div>
                {syncSummary?.kind === "success" ? (
                  <div className="rounded-lg border border-green-200 bg-green-50/90 px-4 py-3 text-sm text-green-950 dark:border-green-900 dark:bg-green-950/40 dark:text-green-100">
                    <p className="font-medium text-green-900 dark:text-green-100">Sync complete</p>
                    <ul className="mt-2 list-inside list-disc space-y-0.5 text-green-900/95 dark:text-green-100/95">
                      <li>Created: {syncSummary.data.createdCount}</li>
                      <li>Updated: {syncSummary.data.updatedCount}</li>
                      <li>Unchanged: {syncSummary.data.unchangedCount}</li>
                      <li>Skipped: {syncSummary.data.skippedCount}</li>
                      <li>Errors: {syncSummary.data.errorCount}</li>
                      <li>Items processed: {syncSummary.data.itemCount}</li>
                    </ul>
                    {syncSummary.data.message?.trim() ? (
                      <p className="mt-2 text-green-900/90 dark:text-green-100/90">{syncSummary.data.message.trim()}</p>
                    ) : null}
                  </div>
                ) : null}
                {syncSummary?.kind === "error" ? (
                  <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/40 dark:text-red-100">
                    <p className="font-medium">Sync failed</p>
                    <p className="mt-1">{syncSummary.message}</p>
                  </div>
                ) : null}
                <div className="flex flex-wrap justify-end gap-2">
                  <ConfigSecondaryButton
                    onClick={() => selectedSourceId && void loadItems(selectedSourceId)}
                    disabled={!selectedSourceId || itemsLoading}
                  >
                    Refresh list
                  </ConfigSecondaryButton>
                  <ConfigPrimaryButton onClick={() => setUpsertOpen(true)} disabled={!selectedSourceId}>
                    Manual upsert test item
                  </ConfigPrimaryButton>
                </div>
                {!selectedSourceId ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">Select an external source above.</p>
                ) : itemsError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{itemsError}</p>
                ) : itemsLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading items…</p>
                ) : items.length === 0 ? (
                  <p className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    No external work items found yet. Use manual upsert to test this source before enabling live sync.
                  </p>
                ) : (
                  <div className="min-w-0 max-w-full overflow-hidden rounded-lg border border-gray-200 dark:border-slate-700">
                    <div className="w-full max-w-full overflow-x-auto overscroll-x-contain">
                      <table className="min-w-[1200px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="min-w-[220px] max-w-[280px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Title</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Status</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Priority</th>
                          <th className="min-w-[100px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Requester</th>
                          <th className="min-w-[100px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Assigned</th>
                          <th className="min-w-[80px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Dept</th>
                          <th className="min-w-[80px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Category</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Due</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Modified</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Last seen</th>
                          <th className="min-w-[160px] whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Linked Cortex ticket</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Link</th>
                          <th className="whitespace-nowrap px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {items.map((it) => (
                          <tr key={it.id} className="bg-white dark:bg-slate-900">
                            <td className="max-w-[280px] min-w-[220px] px-3 py-2 font-medium text-gray-900 dark:text-slate-100">
                              <span className="block truncate" title={it.title || undefined}>
                                {it.title}
                              </span>
                            </td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-700 dark:text-slate-300">{it.status ?? "—"}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-700 dark:text-slate-300">{it.priority ?? "—"}</td>
                            <td className="max-w-[160px] truncate px-3 py-2 text-gray-700 dark:text-slate-300" title={it.requester ?? undefined}>
                              {it.requester ?? "—"}
                            </td>
                            <td className="max-w-[160px] truncate px-3 py-2 text-gray-700 dark:text-slate-300" title={it.assignedTo ?? undefined}>
                              {it.assignedTo ?? "—"}
                            </td>
                            <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{it.department ?? "—"}</td>
                            <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{it.category ?? "—"}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.dueDateUtc)}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.lastModifiedUtc)}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.lastSeenUtc)}</td>
                            <td className="min-w-[160px] whitespace-nowrap px-3 py-2 text-gray-700 dark:text-slate-300">
                              {it.cortexTicketId ? formatLinkedTicketDisplay(it.cortexTicketId) : "Not linked"}
                            </td>
                            <td className="whitespace-nowrap px-3 py-2">
                              {it.externalUrl ? (
                                <a href={it.externalUrl} target="_blank" rel="noreferrer" className="text-cortex-blue hover:underline dark:text-cortex-cyan">
                                  Open
                                </a>
                              ) : (
                                "—"
                              )}
                            </td>
                            <td className="whitespace-nowrap px-3 py-2 text-right">
                              <ConfigGhostButton className="!whitespace-nowrap !py-1.5" onClick={() => setItemDetail(it)}>
                                View details
                              </ConfigGhostButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </ConfigPageBody>
      </ConfigPageShell>

      {connectionModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              {connectionModal.mode === "create" ? "Add connection" : "Edit connection"}
            </h3>
            <div className="mt-4 space-y-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Display name</label>
                <input
                  className={configFieldClass}
                  value={connectionModal.draft.displayName}
                  onChange={(e) => {
                    if (connectionModal.mode === "create") {
                      setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, displayName: e.target.value } });
                    } else {
                      setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, displayName: e.target.value } });
                    }
                  }}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Provider</label>
                <select
                  className={configFieldClass}
                  value={connectionModal.draft.provider}
                  onChange={(e) => {
                    const v = e.target.value as IntegrationProvider;
                    setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, provider: v } });
                  }}
                  disabled={connectionModal.mode === "edit"}
                >
                  {INTEGRATION_PROVIDERS.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
                {connectionModal.mode === "edit" ? (
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">Provider cannot be changed after creation.</p>
                ) : null}
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Auth mode</label>
                  <select
                    className={configFieldClass}
                    value={connectionModal.draft.authMode ?? "Manual"}
                    onChange={(e) => {
                      const v = e.target.value as IntegrationAuthMode;
                      setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, authMode: v } });
                    }}
                  >
                    {AUTH_MODES.map((a) => (
                      <option key={a} value={a}>
                        {humanizeIntegrationAuthMode(a)}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Sync mode</label>
                  <select
                    className={configFieldClass}
                    value={connectionModal.draft.syncMode ?? "ReadOnly"}
                    onChange={(e) => {
                      const v = e.target.value as IntegrationSyncMode;
                      setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, syncMode: v } });
                    }}
                  >
                    {SYNC_MODES.map((s) => (
                      <option key={s} value={s}>
                        {humanizeIntegrationSyncMode(s)}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Tenant ID</label>
                <input
                  className={configFieldClass}
                  value={connectionModal.draft.tenantId ?? ""}
                  onChange={(e) =>
                    setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, tenantId: e.target.value } })
                  }
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Organization ID</label>
                <input
                  className={configFieldClass}
                  value={connectionModal.draft.organizationId ?? ""}
                  onChange={(e) =>
                    setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, organizationId: e.target.value } })
                  }
                />
              </div>
              <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                <input
                  type="checkbox"
                  checked={connectionModal.draft.isEnabled ?? true}
                  onChange={(e) =>
                    setConnectionModal({ ...connectionModal, draft: { ...connectionModal.draft, isEnabled: e.target.checked } })
                  }
                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                />
                Enabled
              </label>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setConnectionModal(null)}>Cancel</ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void saveConnectionModal()}>
                Save
              </ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {sourceModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              {sourceModal.mode === "create" ? "Add external source" : "Edit external source"}
            </h3>
            <div className="mt-4 space-y-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Name</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.name}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, name: e.target.value } })
                  }
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Source type</label>
                <select
                  className={configFieldClass}
                  value={sourceModal.draft.sourceType}
                  onChange={(e) =>
                    setSourceModal({
                      ...sourceModal,
                      draft: { ...sourceModal.draft, sourceType: e.target.value as ExternalSourceType },
                    })
                  }
                >
                  {SOURCE_TYPES.map((t) => (
                    <option key={t} value={t}>
                      {humanizeExternalSourceType(t)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External source ID</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.externalSourceId}
                  onChange={(e) =>
                    setSourceModal({
                      ...sourceModal,
                      draft: { ...sourceModal.draft, externalSourceId: e.target.value },
                    })
                  }
                />
                <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                  Identifier in the source system (for example list GUID, project key, or table name).
                </p>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External URL</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.externalUrl ?? ""}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, externalUrl: e.target.value } })
                  }
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Provider</label>
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-900 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100">
                  {sourceModal.draft.provider}
                </div>
                <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                  Inherited from the connection to this source system. It cannot be changed here.
                </p>
              </div>
              <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                <input
                  type="checkbox"
                  checked={sourceModal.draft.isEnabled ?? true}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, isEnabled: e.target.checked } })
                  }
                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                />
                Enabled
              </label>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setSourceModal(null)}>Cancel</ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void saveSourceModal()}>Save</ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {itemDetail ? (
        <div
          className="fixed inset-0 z-[60] flex justify-end bg-black/40"
          onClick={() => setItemDetail(null)}
          role="presentation"
        >
          <div
            className="flex h-full w-full max-w-md flex-col border-l border-gray-200 bg-white shadow-xl dark:border-slate-700 dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-labelledby="external-item-detail-title"
          >
            <div className="flex shrink-0 items-start justify-between gap-3 border-b border-gray-100 px-5 py-4 dark:border-slate-800">
              <h3 id="external-item-detail-title" className="pr-2 text-lg font-semibold text-gray-900 dark:text-slate-100">
                External work item
              </h3>
              <ConfigSecondaryButton onClick={() => setItemDetail(null)}>Close</ConfigSecondaryButton>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-6">
              <div>
                <DetailField label="Title">
                  <span className="break-words">{itemDetail.title || "—"}</span>
                </DetailField>
                <DetailField label="Description">
                  {itemDetail.description
                    ? (
                        <p className="whitespace-pre-wrap break-words text-gray-800 dark:text-slate-200">
                          {itemDetail.description}
                        </p>
                      )
                    : "—"}
                </DetailField>
                <DetailField label="Status">{itemDetail.status ?? "—"}</DetailField>
                <DetailField label="Priority">{itemDetail.priority ?? "—"}</DetailField>
                <DetailField label="Requester">{itemDetail.requester ?? "—"}</DetailField>
                <DetailField label="Assigned">{itemDetail.assignedTo ?? "—"}</DetailField>
                <DetailField label="Department">{itemDetail.department ?? "—"}</DetailField>
                <DetailField label="Category">{itemDetail.category ?? "—"}</DetailField>
                <DetailField label="Due date">{formatWhen(itemDetail.dueDateUtc)}</DetailField>
                <DetailField label="Last modified">{formatWhen(itemDetail.lastModifiedUtc)}</DetailField>
                <DetailField label="Last seen">{formatWhen(itemDetail.lastSeenUtc)}</DetailField>
                <DetailField label="External source">{itemDetail.sourceName}</DetailField>
                <DetailField label="External item ID">
                  <span className="break-all font-mono text-xs">{itemDetail.externalItemId}</span>
                </DetailField>
                <DetailField label="Source system">{itemDetail.provider}</DetailField>
                <DetailField label="External link">
                  {itemDetail.externalUrl ? (
                    <a
                      href={itemDetail.externalUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="break-all text-cortex-blue hover:underline dark:text-cortex-cyan"
                    >
                      {itemDetail.externalUrl}
                    </a>
                  ) : (
                    "—"
                  )}
                </DetailField>
                <div className="border-b border-gray-100 py-3 last:border-b-0 dark:border-slate-800">
                  <div className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
                    Linked Cortex ticket
                  </div>
                  {itemDetail.cortexTicketId ? (
                    <div className="mt-2 space-y-2">
                      <p className="text-base font-semibold text-gray-900 dark:text-slate-100">
                        {formatLinkedTicketDisplay(itemDetail.cortexTicketId)}
                      </p>
                      {onOpenCortexTicketById ? (
                        <button
                          type="button"
                          onClick={() => void onOpenCortexTicketById(itemDetail.cortexTicketId!)}
                          className="text-sm font-medium text-cortex-blue hover:underline dark:text-cortex-cyan"
                        >
                          Open ticket
                        </button>
                      ) : null}
                    </div>
                  ) : (
                    <div className="mt-2 space-y-2">
                      <p className="text-sm text-gray-600 dark:text-slate-400">Not linked</p>
                      <ConfigPrimaryButton
                        className="!py-1.5"
                        onClick={() => void startCreateCortexTicket(itemDetail)}
                      >
                        Create Cortex ticket
                      </ConfigPrimaryButton>
                    </div>
                  )}
                </div>
              </div>
              <div className="mt-5 rounded-lg border border-sky-200 bg-sky-50/90 px-3 py-3 text-xs text-sky-900 dark:border-sky-800 dark:bg-sky-950/40 dark:text-sky-100">
                Cortex insight over external work items will appear here after live sync and analysis are enabled.
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {createTicketOpen && createTicketFor ? (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4">
          <div
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900"
            role="dialog"
            aria-modal="true"
            aria-labelledby="create-ticket-from-external-title"
          >
            <h3
              id="create-ticket-from-external-title"
              className="text-lg font-semibold text-gray-900 dark:text-slate-100"
            >
              Create Cortex ticket
            </h3>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              Creating a Cortex ticket does not update the external source. The ticket will follow the normal Cortex
              approval process.
            </p>
            <div className="mt-4 rounded-lg border border-gray-200 bg-gray-50/90 px-3 py-3 text-xs text-gray-800 dark:border-slate-700 dark:bg-slate-800/60 dark:text-slate-200">
              <p className="font-medium text-gray-900 dark:text-slate-100">External source context</p>
              <ul className="mt-2 list-inside list-disc space-y-0.5">
                <li>
                  Source: {createTicketFor.sourceName} ({createTicketFor.provider})
                </li>
                <li className="font-mono">External item ID: {createTicketFor.externalItemId}</li>
                <li>
                  {createTicketFor.externalUrl ? (
                    <a
                      href={createTicketFor.externalUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="text-cortex-blue hover:underline dark:text-cortex-cyan"
                    >
                      External link
                    </a>
                  ) : (
                    "No external link on this item"
                  )}
                </li>
              </ul>
            </div>
            <div className="mt-4 grid gap-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Title</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.title}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, title: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Description</label>
                <textarea
                  className={`${configFieldClass} min-h-[100px]`}
                  value={createTicketDraft.description}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, description: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Board</label>
                <select
                  className={configFieldClass}
                  value={createTicketDraft.boardId === "" ? "" : String(createTicketDraft.boardId)}
                  onChange={(e) => {
                    const v = e.target.value;
                    setCreateTicketDraft({
                      ...createTicketDraft,
                      boardId: v ? Number(v) : "",
                    });
                  }}
                  disabled={ticketBoardLoading || ticketBoards.filter((b) => b.isEnabled).length === 0}
                >
                  <option value="">— Select board —</option>
                  {ticketBoards
                    .filter((b) => b.isEnabled)
                    .map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name}
                      </option>
                    ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Priority</label>
                <select
                  className={configFieldClass}
                  value={createTicketDraft.priority}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, priority: e.target.value })}
                >
                  {EXTERNAL_TICKET_PRIORITIES.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Due date</label>
                <input
                  className={configFieldClass}
                  type="datetime-local"
                  value={createTicketDraft.dueDateUtc}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, dueDateUtc: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Department</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.department}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, department: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Category</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.category}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, category: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Requester</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.requester}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, requester: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Assigned to</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.assignedTo}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, assignedTo: e.target.value })}
                />
              </div>
            </div>
            {createTicketError ? (
              <p className="mt-3 text-sm text-red-600 dark:text-red-400">{createTicketError}</p>
            ) : null}
            <div className="mt-6 flex flex-wrap justify-end gap-2">
              <ConfigSecondaryButton
                onClick={() => {
                  setCreateTicketOpen(false);
                  setCreateTicketFor(null);
                  setCreateTicketError(null);
                }}
                disabled={createTicketSaving}
              >
                Cancel
              </ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void submitCreateCortexTicket()} disabled={createTicketSaving}>
                {createTicketSaving ? "Creating…" : "Create ticket"}
              </ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {upsertOpen && selectedSourceId !== null ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">Manual upsert test item</h3>
            <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
              Source: <span className="font-medium">{selectedSource?.name ?? `#${selectedSourceId}`}</span>
            </p>
            <div className="mt-4 grid gap-3">
              {(
                [
                  ["externalItemId", "External item ID", "text"],
                  ["title", "Title", "text"],
                  ["externalUrl", "External URL", "text"],
                  ["description", "Description", "text"],
                  ["status", "Status", "text"],
                  ["priority", "Priority", "text"],
                  ["requester", "Requester", "text"],
                  ["assignedTo", "Assigned to", "text"],
                  ["department", "Department", "text"],
                  ["category", "Category", "text"],
                  ["dueDateUtc", "Due (local)", "datetime-local"],
                  ["lastModifiedUtc", "Last modified (local)", "datetime-local"],
                ] as const
              ).map(([key, label, type]) => (
                <div key={key}>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">{label}</label>
                  <input
                    className={configFieldClass}
                    type={type}
                    value={String(upsertDraft[key] ?? "")}
                    onChange={(e) =>
                      setUpsertDraft({ ...upsertDraft, [key]: e.target.value })
                    }
                  />
                </div>
              ))}
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Extra data (JSON)</label>
                <textarea
                  className={configFieldClass}
                  rows={4}
                  placeholder="Optional. If empty, a small JSON object is generated from the fields above."
                  value={upsertDraft.rawJson ?? ""}
                  onChange={(e) => setUpsertDraft({ ...upsertDraft, rawJson: e.target.value })}
                />
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setUpsertOpen(false)} disabled={upsertSaving}>
                Cancel
              </ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void submitUpsert()} disabled={upsertSaving}>
                {upsertSaving ? "Saving…" : "Save item"}
              </ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
