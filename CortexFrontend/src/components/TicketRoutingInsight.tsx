import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useMemo, useState } from "react";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type {
  OwnerWorkloadPreviewResponse,
  OwnerWorkloadSummaryDto,
  RoutingExplanationPayload,
  RoutingPreviewRequest,
  TicketRoutingDecisionDto,
  TicketRoutingLatestResponse,
  TicketRoutingOverrideDto,
} from "../types/ticketRoutingInsight";
import { ticketService } from "../services/api";
import type { CortexDecisionResult } from "../types/cortexDecision";
import { formatDisplayValue } from "../utils/presentation";
import { formatOwnerFieldForDisplay } from "../utils/ownerIdentity";
import { CortexTooltip } from "./ui/Tooltip";

const API_AUDIENCE = "https://cortex-api";

/** Max decision factors shown before readability drops. */
const MAX_FACTOR_LINES = 3;

/** Debounce title/department for routing preview POST to avoid spam while typing. */
const PREVIEW_TEXT_DEBOUNCE_MS = 400;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

/** Draft fields that drive live POST /routing/preview (existing tickets in the modal). */
export interface RoutingLivePreviewInput {
  boardId: number;
  priority: string;
  title: string;
  department: string;
}

type WorkloadSignalLabel = "Low Load" | "Balanced" | "High Load";

/** Advisory workload signal only; recommendation refresh never changes the final assignment. */
const WORKLOAD_SIGNAL_ABOUT_TOOLTIP =
  "Advisory signal based on current open, at-risk, and breached tickets. It does not change the final assignment.";

const WORKLOAD_SIGNAL_TOOLTIP: Record<WorkloadSignalLabel, string> = {
  "Low Load": "Owner has headroom based on current ticket volume.",
  Balanced: "Owner workload is manageable based on current ticket volume.",
  "High Load": "Owner workload is elevated; monitor before adding more work.",
};

function resolveBoardNameForDisplay(
  boardIdToken: string | null | undefined,
  ticket: Ticket,
  ticketBoards: TicketBoardDefinition[] | undefined,
): string {
  const trimmed = boardIdToken?.trim();
  if (!trimmed) {
    return "—";
  }
  const n = Number.parseInt(trimmed, 10);
  if (Number.isNaN(n)) {
    return trimmed;
  }
  const fromConfig = ticketBoards?.find((b) => b.id === n);
  if (fromConfig?.name?.trim()) {
    return fromConfig.name.trim();
  }
  if (ticket.boardId === n && ticket.boardName?.trim()) {
    return ticket.boardName.trim();
  }
  return "—";
}

function normalizeOwnerToken(value: string | undefined | null): string {
  return (value ?? "").trim().toLowerCase();
}

function ownersMatch(
  current: string | undefined,
  recommended: string | undefined,
): boolean {
  return normalizeOwnerToken(current) === normalizeOwnerToken(recommended);
}

function buildOwnerLabel(
  ownerKey: string | undefined | null,
  displayName?: string | null,
): string {
  const resolvedDisplayName = displayName?.trim();
  if (resolvedDisplayName) {
    return formatDisplayValue(resolvedDisplayName);
  }
  return formatDisplayValue(formatOwnerFieldForDisplay(ownerKey) || undefined);
}

function parseExplanationJson(json: string): RoutingExplanationPayload | null {
  if (!json?.trim()) {
    return null;
  }
  try {
    const parsed = JSON.parse(json) as unknown;
    if (typeof parsed !== "object" || parsed === null) {
      return null;
    }
    return parsed as RoutingExplanationPayload;
  } catch {
    return null;
  }
}

function humanizeOverrideReason(type: string): string {
  switch (type) {
    case "ManualAssignment":
      return "Manual override";
    case "WorkloadAdjustment":
      return "Workload adjustment";
    case "IncorrectRouting":
      return "Incorrect routing";
    case "Escalation":
      return "Escalation";
    case "Other":
      return "Other";
    default:
      return type.replace(/([A-Z])/g, " $1").trim();
  }
}

function humanizeImpactSource(source: string | undefined | null): string {
  switch (source) {
    case "cortex_recommendation_review":
      return "Cortex recommendation";
    default:
      return source?.trim() || "Cortex recommendation";
  }
}

function toTitleCaseWord(value: string | undefined | null): string {
  if (!value?.trim()) {
    return "Low";
  }
  const normalized = value.trim().toLowerCase();
  return `${normalized[0]?.toUpperCase() ?? ""}${normalized.slice(1)}`;
}

const FACTOR_LABELS: Record<string, string> = {
  boardId: "Board",
  BoardId: "Board",
  priority: "Priority",
  Priority: "Priority",
  requesterDepartment: "Requester department",
  RequesterDepartment: "Requester department",
  requesterRole: "Requester role",
  RequesterRole: "Requester role",
  legacyDepartment: "Department",
  LegacyDepartment: "Department",
  Department: "Department",
  legacyTitle: "Title",
  LegacyTitle: "Title",
};

/** True for boardId / BoardId / board_id style keys from API payloads. */
function isBoardFactorKey(key: string): boolean {
  return key.trim().replace(/_/g, "").toLowerCase() === "boardid";
}

/**
 * Reads a string factor value trying camelCase, PascalCase, and case-insensitive keys
 * (explanation JSON may use BoardId vs boardId depending on serializer/version).
 */
function readFactorString(
  factors: Record<string, string | null | undefined> | undefined,
  camelKey: string,
  pascalKey: string,
): string | null {
  const f = factors ?? {};
  const tryKeys = [camelKey, pascalKey];
  for (const k of tryKeys) {
    const raw = f[k as keyof typeof f];
    if (typeof raw === "string" && raw.trim()) {
      return raw.trim();
    }
  }
  const norm = (s: string) => s.replace(/_/g, "").toLowerCase();
  const target = norm(camelKey);
  for (const entryKey of Object.keys(f)) {
    if (norm(entryKey) === target) {
      const raw = f[entryKey];
      if (typeof raw === "string" && raw.trim()) {
        return raw.trim();
      }
    }
  }
  return null;
}

/** Resolves board id token from factors regardless of property casing. */
function readBoardIdTokenFromFactors(
  factors: Record<string, string | null | undefined> | undefined,
): string | null {
  if (!factors) {
    return null;
  }
  for (const [key, val] of Object.entries(factors)) {
    if (isBoardFactorKey(key) && typeof val === "string" && val.trim()) {
      return val.trim();
    }
  }
  return null;
}

function formatFactorLabel(key: string): string {
  if (isBoardFactorKey(key)) {
    return "Board";
  }
  return FACTOR_LABELS[key] ?? key.replace(/([A-Z])/g, " $1").trim();
}

/** Maps persisted routing criterion labels to readable lines using factor values from the explanation payload. */
function formatCriterionLine(
  criterion: string,
  factors: Record<string, string | null | undefined> | undefined,
  ticket: Ticket,
  ticketBoards: TicketBoardDefinition[] | undefined,
): string {
  const trimmed = criterion.trim();
  /** Rare: criterion stored as a preformatted line instead of a token. */
  const boardIdPreformatted =
    /^board\s*id\s*:\s*(.+)$/i.exec(trimmed) ??
    /^boardid\s*:\s*(.+)$/i.exec(trimmed);
  if (boardIdPreformatted) {
    return `Board: ${resolveBoardNameForDisplay(
      boardIdPreformatted[1]?.trim(),
      ticket,
      ticketBoards,
    )}`;
  }

  const f = factors ?? {};
  const v = (key: string) => {
    const raw = f[key];
    return typeof raw === "string" && raw.trim() ? raw.trim() : null;
  };

  switch (trimmed) {
    case "BoardId":
      return `Board: ${resolveBoardNameForDisplay(
        readBoardIdTokenFromFactors(factors) ??
          readFactorString(factors, "boardId", "BoardId"),
        ticket,
        ticketBoards,
      )}`;
    case "Priority":
      return `Priority: ${readFactorString(f, "priority", "Priority") ?? v("priority") ?? "—"}`;
    case "RequesterDepartment":
      return `Requester department: ${readFactorString(f, "requesterDepartment", "RequesterDepartment") ?? "—"}`;
    case "RequesterRole":
      return `Requester role: ${readFactorString(f, "requesterRole", "RequesterRole") ?? "—"}`;
    case "Department":
      return `Department: ${readFactorString(f, "legacyDepartment", "LegacyDepartment") ?? "—"}`;
    case "TitleContains": {
      const title =
        readFactorString(f, "legacyTitle", "LegacyTitle") ?? v("legacyTitle");
      if (!title) {
        return "Title matched a routing keyword";
      }
      const snippet = title.length > 52 ? `${title.slice(0, 49)}…` : title;
      return `Title matches “${snippet}”`;
    }
    default:
      return formatFactorLabel(criterion);
  }
}

function formatFallbackFactorLine(
  key: string,
  val: string | null | undefined,
  ticket: Ticket,
  ticketBoards: TicketBoardDefinition[] | undefined,
): string {
  if (isBoardFactorKey(key)) {
    return `Board: ${resolveBoardNameForDisplay(
      val != null && String(val).trim() ? String(val).trim() : undefined,
      ticket,
      ticketBoards,
    )}`;
  }
  const label = formatFactorLabel(key);
  const s = val != null && String(val).trim() ? String(val).trim() : "—";
  return `${label}: ${s}`;
}

/** One-line assignment posture: manual vs aligned vs fallback — confident, neutral tone. */
function assignmentSummaryLine(
  decision: TicketRoutingDecisionDto,
  override: TicketRoutingOverrideDto | null,
  ticket: Ticket,
): string {
  if (override) {
    return "Final assignment was manually selected.";
  }

  const hasSynitiRec = Boolean(decision.chosenSynitiOwner?.trim());
  const hasBusinessRec = Boolean(decision.chosenBusinessOwner?.trim());
  const hasRecommendation = hasSynitiRec || hasBusinessRec;

  if (hasRecommendation) {
    const synitiOk =
      !hasSynitiRec ||
      ownersMatch(ticket.synitiOwner, decision.chosenSynitiOwner);
    const businessOk =
      !hasBusinessRec ||
      ownersMatch(ticket.businessOwner, decision.chosenBusinessOwner);
    if (synitiOk && businessOk) {
      return "Final assignment aligns with the Cortex recommendation based on routing signals and workload context.";
    }
    return "Final assignment was manually selected.";
  }

  if (decision.outcomeType === "RuleMatch") {
    return "Cortex applied configured routing signals for this ticket.";
  }

  return "Final assignment reflects manual selection.";
}

function hasOwnerRecommendation(decision: TicketRoutingDecisionDto): boolean {
  return (
    Boolean(decision.chosenSynitiOwner?.trim()) ||
    Boolean(decision.chosenBusinessOwner?.trim())
  );
}

function humanizeCriterion(criterion: string): string {
  switch (criterion) {
    case "BoardId":
      return "board alignment";
    case "Priority":
      return "priority alignment";
    case "RequesterDepartment":
    case "Department":
      return "department alignment";
    case "RequesterRole":
      return "requester role alignment";
    case "TitleContains":
      return "title signal alignment";
    default:
      return criterion.replace(/([A-Z])/g, " $1").trim().toLowerCase();
  }
}

function extractSignalBullet(line: string): string | null {
  const [labelRaw, valueRaw] = line.split(":");
  const label = labelRaw?.trim().toLowerCase();
  const value = valueRaw?.trim();
  if (!label) {
    return null;
  }
  if (label === "board" && value) {
    return `Matches ${value} board decision factor`;
  }
  if (label === "priority" && value) {
    return `Matches ${value} priority decision factor`;
  }
  if (label === "requester department" && value) {
    return `Matches ${value} requester department decision factor`;
  }
  if (label === "requester role" && value) {
    return `Matches ${value} requester role decision factor`;
  }
  if (label === "department" && value) {
    return `Matches ${value} department decision factor`;
  }
  return null;
}

function collectWorkloadOwnerKeys(
  decision: TicketRoutingDecisionDto | null,
  ticket: Ticket,
  explanation: RoutingExplanationPayload | null,
): string[] {
  const keys: string[] = [];
  if (decision) {
    if (decision.chosenSynitiOwner?.trim()) {
      keys.push(decision.chosenSynitiOwner.trim());
    }
    if (decision.chosenBusinessOwner?.trim()) {
      keys.push(decision.chosenBusinessOwner.trim());
    }
  }
  if (keys.length === 0 && explanation?.slots) {
    const synitiOwner = explanation.slots.synitiOwner?.selectedOwnerKey;
    const businessOwner = explanation.slots.businessOwner?.selectedOwnerKey;
    if (synitiOwner?.trim()) {
      keys.push(synitiOwner.trim());
    }
    if (businessOwner?.trim()) {
      keys.push(businessOwner.trim());
    }
  }
  if (keys.length === 0) {
    if (ticket.synitiOwner?.trim()) {
      keys.push(ticket.synitiOwner.trim());
    }
    if (ticket.businessOwner?.trim()) {
      keys.push(ticket.businessOwner.trim());
    }
  }
  return [...new Set(keys)];
}

function resolveWorkloadSignal(
  summary: Pick<
    OwnerWorkloadSummaryDto,
    "activeTicketCount" | "atRiskTicketCount" | "outsideSlaOpenCount"
  >,
): WorkloadSignalLabel {
  if (
    summary.activeTicketCount >= 10 ||
    summary.atRiskTicketCount >= 3 ||
    summary.outsideSlaOpenCount >= 2
  ) {
    return "High Load";
  }
  if (
    summary.activeTicketCount >= 5 ||
    summary.atRiskTicketCount >= 1 ||
    summary.outsideSlaOpenCount >= 1
  ) {
    return "Balanced";
  }
  return "Low Load";
}

function workloadSignalClassName(signal: WorkloadSignalLabel): string {
  if (signal === "High Load") {
    return "bg-red-100 text-red-900 dark:bg-red-950/60 dark:text-red-100";
  }
  if (signal === "Balanced") {
    return "bg-amber-100 text-amber-950 dark:bg-amber-950/50 dark:text-amber-50";
  }
  return "bg-emerald-100 text-emerald-950 dark:bg-emerald-950/40 dark:text-emerald-100";
}

function aggregateWorkload(
  summaries: OwnerWorkloadSummaryDto[],
): {
  active: number;
  atRisk: number;
  breached: number;
  signal: WorkloadSignalLabel;
  sentence: string;
} {
  const active = summaries.reduce((a, s) => a + s.activeTicketCount, 0);
  const atRisk = summaries.reduce((a, s) => a + s.atRiskTicketCount, 0);
  const breached = summaries.reduce((a, s) => a + s.outsideSlaOpenCount, 0);
  const signal = resolveWorkloadSignal({
    activeTicketCount: active,
    atRiskTicketCount: atRisk,
    outsideSlaOpenCount: breached,
  });

  const multi = summaries.length > 1;
  let sentence: string;

  if (active === 0 && atRisk === 0 && breached === 0) {
    sentence =
      "No other active tickets are in view for the selected owners.";
  } else if (signal === "High Load") {
    sentence =
      "Final assignment should be monitored closely based on current workload.";
  } else if (signal === "Balanced") {
    sentence =
      "Current owner workload is manageable but should be watched.";
  } else if (active <= 4) {
    sentence = multi
      ? `Assigned owners currently have a light combined workload (${active} active tickets).`
      : `Assigned owners currently have a light workload (${active} active tickets).`;
  } else {
    sentence = multi
      ? `Combined workload is manageable (${active} active tickets).`
      : `Workload is manageable (${active} active tickets).`;
  }

  return { active, atRisk, breached, signal, sentence };
}

function RecommendationWorkloadPill({
  summary,
  loading,
}: {
  summary: OwnerWorkloadSummaryDto | null | undefined;
  loading: boolean;
}) {
  if (loading) {
    return (
      <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-500 dark:bg-slate-800 dark:text-slate-400">
        Workload...
      </span>
    );
  }

  if (!summary) {
    return (
      <span className="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-500 dark:bg-slate-800 dark:text-slate-400">
        —
      </span>
    );
  }

  const signal = resolveWorkloadSignal(summary);
  return (
    <CortexTooltip
      content={`${summary.activeTicketCount} active tickets · ${summary.atRiskTicketCount} at risk · ${summary.outsideSlaOpenCount} outside SLA`}
    >
      <span
        className={`cursor-help rounded-md px-2 py-0.5 text-[11px] font-semibold ${workloadSignalClassName(
          signal,
        )}`}
      >
        {signal}
      </span>
    </CortexTooltip>
  );
}

interface TicketRoutingInsightProps {
  ticket: Ticket;
  isModalOpen: boolean;
  /** When provided, routing signals show board names instead of numeric ids. */
  ticketBoards?: TicketBoardDefinition[];
  /**
   * When set for an existing ticket, decision + signals come from POST /routing/preview
   * using these draft values; persisted override is still loaded from GET /routing/latest.
   */
  livePreview?: RoutingLivePreviewInput | null;
  /** Called after guided reassignment apply when that flow is present on the ticket. */
  onReassignmentApplied?: (updatedTicket: Ticket) => void;
  riskLevel?: "Low" | "Medium" | "High" | null;
  onRecommendedOwnerClick?: () => void;
  highlightPanel?: boolean;
}

export default function TicketRoutingInsight({
  ticket,
  isModalOpen,
  ticketBoards,
  livePreview,
  riskLevel,
  onRecommendedOwnerClick,
  highlightPanel = false,
}: TicketRoutingInsightProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [data, setData] = useState<TicketRoutingLatestResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [persistedOverride, setPersistedOverride] =
    useState<TicketRoutingOverrideDto | null>(null);
  const [liveDecision, setLiveDecision] =
    useState<TicketRoutingDecisionDto | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [workload, setWorkload] = useState<OwnerWorkloadPreviewResponse | null>(
    null,
  );
  const [workloadLoading, setWorkloadLoading] = useState(false);
  const [cortexDecision, setCortexDecision] = useState<CortexDecisionResult | null>(
    null,
  );
  const [decisionPanelExpanded, setDecisionPanelExpanded] = useState(true);
  const [fullReasoningExpanded, setFullReasoningExpanded] = useState(false);
  const [refreshNonce, setRefreshNonce] = useState(0);

  const isLiveRoutingPreview = Boolean(livePreview && ticket.id);
  const debouncedPreviewTitle = useDebouncedValue(
    livePreview?.title ?? "",
    PREVIEW_TEXT_DEBOUNCE_MS,
  );
  const debouncedPreviewDepartment = useDebouncedValue(
    livePreview?.department ?? "",
    PREVIEW_TEXT_DEBOUNCE_MS,
  );
  const livePreviewBoardId = livePreview?.boardId;
  const livePreviewPriority = livePreview?.priority;

  useEffect(() => {
    setDecisionPanelExpanded(true);
    setFullReasoningExpanded(false);
  }, [ticket.id]);

  useEffect(() => {
    if (!isModalOpen || !ticket.id) {
      setData(null);
      setPersistedOverride(null);
      setError(null);
      return;
    }

    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const latest = await ticketService.getLatestRouting(ticket.id, token);
        if (cancelled) {
          return;
        }
        if (isLiveRoutingPreview) {
          setPersistedOverride(latest.override ?? null);
          setData(latest);
        } else {
          setData(latest);
          setPersistedOverride(null);
        }
      } catch {
        if (!cancelled) {
          setError("Recommendation details aren’t available right now.");
          setData(null);
          setPersistedOverride(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    isModalOpen,
    ticket.id,
    ticket.synitiOwner,
    ticket.businessOwner,
    ticket.lastModifiedDate,
    isLiveRoutingPreview,
    refreshNonce,
    getAccessTokenSilently,
  ]);

  useEffect(() => {
    if (!isModalOpen || !ticket.id) {
      setCortexDecision(null);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const latest = await ticketService.getTicketDecision(ticket.id, token);
        if (!cancelled) {
          setCortexDecision(latest);
        }
      } catch {
        if (!cancelled) {
          setCortexDecision(null);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [
    getAccessTokenSilently,
    isModalOpen,
    ticket.id,
    ticket.lastModifiedDate,
    refreshNonce,
  ]);

  useEffect(() => {
    if (
      !isModalOpen ||
      !ticket.id ||
      !isLiveRoutingPreview ||
      livePreviewBoardId == null ||
      livePreviewPriority == null
    ) {
      setLiveDecision(null);
      setPreviewLoading(false);
      return;
    }

    let cancelled = false;
    const body: RoutingPreviewRequest = {
      ticketId: ticket.id,
      boardId: livePreviewBoardId,
      priority: livePreviewPriority,
      title: debouncedPreviewTitle.trim() || undefined,
      department: debouncedPreviewDepartment.trim() || undefined,
    };

    (async () => {
      setPreviewLoading(true);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const preview = await ticketService.postRoutingPreview(body, token);
        if (!cancelled) {
          setLiveDecision(preview.decision);
        }
      } catch {
        /* keep previous liveDecision; no toast */
      } finally {
        if (!cancelled) {
          setPreviewLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    isModalOpen,
    ticket.id,
    isLiveRoutingPreview,
    livePreviewBoardId,
    livePreviewPriority,
    debouncedPreviewTitle,
    debouncedPreviewDepartment,
    refreshNonce,
    getAccessTokenSilently,
  ]);

  const decision = isLiveRoutingPreview
    ? (liveDecision ?? data?.decision ?? null)
    : (data?.decision ?? null);
  const override = isLiveRoutingPreview
    ? persistedOverride
    : (data?.override ?? null);

  const explanation = useMemo(
    () => (decision ? parseExplanationJson(decision.explanationJson) : null),
    [decision],
  );

  const matchedFactorLines = useMemo(() => {
    const criteria = explanation?.matchedCriteria;
    if (!criteria?.length) {
      return [];
    }
    return criteria.map((c) =>
      formatCriterionLine(c, explanation?.factors, ticket, ticketBoards),
    );
  }, [explanation, ticket, ticketBoards]);

  const displayedFactorLines = useMemo(
    () => matchedFactorLines.slice(0, MAX_FACTOR_LINES),
    [matchedFactorLines],
  );

  const factorOverflow = matchedFactorLines.length - displayedFactorLines.length;

  const fallbackFactorEntries = useMemo(() => {
    if (!explanation?.factors || matchedFactorLines.length > 0) {
      return [] as { key: string; line: string }[];
    }
    return Object.entries(explanation.factors)
      .filter(([, val]) => val && String(val).trim())
      .slice(0, MAX_FACTOR_LINES)
      .map(([key, val]) => ({
        key,
        line: formatFallbackFactorLine(key, val, ticket, ticketBoards),
      }));
  }, [explanation?.factors, matchedFactorLines.length, ticket, ticketBoards]);

  const assignmentLine = useMemo(() => {
    if (!decision) {
      return null;
    }
    return assignmentSummaryLine(decision, override, ticket);
  }, [decision, override, ticket]);

  const slotReasoning = useMemo(() => {
    const slots = explanation?.slots;
    if (!slots) {
      return [];
    }
    const output: Array<{
      label: string;
      selectedOwnerKey?: string | null;
      selectedOwnerDisplayName?: string | null;
      classification: string;
      candidates: Array<{
        ownerKey: string;
        displayName?: string | null;
        matchScore: number;
        workloadPenalty: number;
        finalScore: number;
      }>;
      skipped: Array<{
        ownerKey?: string | null;
        reason?: string;
        message?: string;
      }>;
    }> = [];
    if (slots.synitiOwner) {
      output.push({
        label: "Syniti Owner",
        selectedOwnerKey: slots.synitiOwner.selectedOwnerKey,
        selectedOwnerDisplayName: slots.synitiOwner.selectedOwnerDisplayName,
        classification: slots.synitiOwner.classification ?? "",
        candidates: (slots.synitiOwner.candidates ?? [])
          .filter((candidate): candidate is NonNullable<typeof candidate> =>
            Boolean(candidate?.ownerKey),
          )
          .map((candidate) => ({
            ownerKey: candidate.ownerKey!.trim(),
            displayName: candidate.displayName,
            matchScore: candidate.matchScore ?? 0,
            workloadPenalty: candidate.workloadPenalty ?? 0,
            finalScore: candidate.finalScore ?? 0,
          })),
        skipped: slots.synitiOwner.skippedReasons ?? [],
      });
    }
    if (slots.businessOwner) {
      output.push({
        label: "Business Owner",
        selectedOwnerKey: slots.businessOwner.selectedOwnerKey,
        selectedOwnerDisplayName: slots.businessOwner.selectedOwnerDisplayName,
        classification: slots.businessOwner.classification ?? "",
        candidates: (slots.businessOwner.candidates ?? [])
          .filter((candidate): candidate is NonNullable<typeof candidate> =>
            Boolean(candidate?.ownerKey),
          )
          .map((candidate) => ({
            ownerKey: candidate.ownerKey!.trim(),
            displayName: candidate.displayName,
            matchScore: candidate.matchScore ?? 0,
            workloadPenalty: candidate.workloadPenalty ?? 0,
            finalScore: candidate.finalScore ?? 0,
          })),
        skipped: slots.businessOwner.skippedReasons ?? [],
      });
    }
    return output;
  }, [explanation?.slots]);

  const selectedBecauseLines = useMemo(() => {
    const lines: string[] = [];
    const signalBullets = matchedFactorLines
      .map((line) => extractSignalBullet(line))
      .filter((line): line is string => Boolean(line))
      .slice(0, 2);
    lines.push(...signalBullets);

    if (lines.length === 0 && explanation?.matchedCriteria?.length) {
      lines.push(
        `Recommendation matched ${explanation.matchedCriteria
          .slice(0, 2)
          .map((criterion) => humanizeCriterion(criterion))
          .join(" and ")}.`,
      );
    }

    for (const slot of slotReasoning) {
      const selected = slot.selectedOwnerKey?.trim();
      if (!selected) {
        continue;
      }
      if (slot.candidates.length <= 1) {
        lines.push("No competing eligible recommendations met decision criteria");
        continue;
      }
      const sorted = [...slot.candidates].sort(
        (left, right) => right.finalScore - left.finalScore,
      );
      const winner = sorted.find(
        (candidate) =>
          normalizeOwnerToken(candidate.ownerKey) === normalizeOwnerToken(selected),
      );
      const nextBest = sorted.find(
        (candidate) =>
          normalizeOwnerToken(candidate.ownerKey) !== normalizeOwnerToken(selected),
      );
      if (!winner || !nextBest) {
        continue;
      }

      if (winner.workloadPenalty < nextBest.workloadPenalty) {
        lines.push("Lower workload than other eligible recommendations");
      } else if (winner.matchScore > nextBest.matchScore) {
        lines.push("Stronger decision factor match than alternatives");
      }
    }
    if (!slotReasoning.some((slot) => slot.candidates.length > 1)) {
      lines.push(
        "No better eligible alternative was identified based on current workload and routing signals",
      );
    }
    return lines.slice(0, 3);
  }, [matchedFactorLines, explanation?.matchedCriteria, slotReasoning]);

  const alternativesConsidered = useMemo(() => {
    return slotReasoning.flatMap((slot) => {
      const selected = slot.selectedOwnerKey?.trim();
      const sorted = [...slot.candidates]
        .sort((left, right) => right.finalScore - left.finalScore)
        .filter((candidate) => {
          if (!selected) {
            return true;
          }
          return (
            normalizeOwnerToken(candidate.ownerKey) !== normalizeOwnerToken(selected)
          );
        })
        .slice(0, 2);

      const alternatives = sorted.map((candidate) => {
        const selectedCandidate = slot.candidates.find(
          (item) =>
            selected &&
            normalizeOwnerToken(item.ownerKey) === normalizeOwnerToken(selected),
        );
        let reason = "weaker match";
        if (selectedCandidate) {
          if (candidate.workloadPenalty > selectedCandidate.workloadPenalty) {
            reason = "higher workload";
          } else if (candidate.matchScore < selectedCandidate.matchScore) {
            reason = "weaker match";
          }
        }
        return {
          slotLabel: slot.label,
          ownerLabel: buildOwnerLabel(candidate.ownerKey, candidate.displayName),
          reason,
        };
      });

      const skipped = slot.skipped
        .filter((entry) => entry.reason?.includes("Eligible") || entry.reason === "UnresolvedRuleOwner")
        .slice(0, 1)
        .map((entry) => ({
          slotLabel: slot.label,
          ownerLabel: buildOwnerLabel(entry.ownerKey),
          reason:
            entry.reason === "UnresolvedRuleOwner"
              ? entry.message ?? "not eligible (unresolved owner)"
              : "not eligible",
        }));

      return [...alternatives, ...skipped];
    });
  }, [slotReasoning]);

  const synitiOwnerDisplay = useMemo(() => {
    const slot = explanation?.slots?.synitiOwner;
    const ownerKey = decision?.chosenSynitiOwner?.trim() || slot?.selectedOwnerKey;
    const displayName =
      slot?.selectedOwnerDisplayName ||
      (ownersMatch(ticket.synitiOwner, ownerKey ?? undefined)
        ? ticket.synitiOwnerDisplayName
        : undefined);
    if (ownerKey || slot?.selectedOwnerDisplayName?.trim()) {
      return buildOwnerLabel(ownerKey, displayName);
    }
    const hasSynitiRuleEvidence = Boolean(
      slot &&
        ((slot.candidates?.length ?? 0) > 0 ||
          (slot.skippedReasons?.length ?? 0) > 0),
    );
    return hasSynitiRuleEvidence
      ? "No clear owner identified"
      : "No matching routing rule";
  }, [
    decision?.chosenSynitiOwner,
    explanation?.slots?.synitiOwner,
    ticket.synitiOwner,
    ticket.synitiOwnerDisplayName,
  ]);

  const businessOwnerDisplay = useMemo(() => {
    const slot = explanation?.slots?.businessOwner;
    const ownerKey =
      decision?.chosenBusinessOwner?.trim() || slot?.selectedOwnerKey;
    const displayName =
      slot?.selectedOwnerDisplayName ||
      (ownersMatch(ticket.businessOwner, ownerKey ?? undefined)
        ? ticket.businessOwnerDisplayName
        : undefined);
    if (ownerKey || slot?.selectedOwnerDisplayName?.trim()) {
      return buildOwnerLabel(ownerKey, displayName);
    }
    const hasBusinessRuleEvidence = Boolean(
      slot &&
        ((slot.candidates?.length ?? 0) > 0 ||
          (slot.skippedReasons?.length ?? 0) > 0),
    );
    return hasBusinessRuleEvidence
      ? "No clear business owner identified"
      : "No matching routing rule";
  }, [
    decision?.chosenBusinessOwner,
    explanation?.slots?.businessOwner,
    ticket.businessOwner,
    ticket.businessOwnerDisplayName,
  ]);

  const recommendedSynitiOwnerKey =
    decision?.chosenSynitiOwner?.trim() ||
    explanation?.slots?.synitiOwner?.selectedOwnerKey?.trim() ||
    "";
  const recommendedBusinessOwnerKey =
    decision?.chosenBusinessOwner?.trim() ||
    explanation?.slots?.businessOwner?.selectedOwnerKey?.trim() ||
    "";
  const finalSynitiOwnerDisplay = useMemo(
    () => buildOwnerLabel(ticket.synitiOwner, ticket.synitiOwnerDisplayName),
    [ticket.synitiOwner, ticket.synitiOwnerDisplayName],
  );
  const finalBusinessOwnerDisplay = useMemo(
    () => buildOwnerLabel(ticket.businessOwner, ticket.businessOwnerDisplayName),
    [ticket.businessOwner, ticket.businessOwnerDisplayName],
  );
  const synitiOwnerOverridden = Boolean(recommendedSynitiOwnerKey) &&
    !ownersMatch(ticket.synitiOwner, recommendedSynitiOwnerKey);
  const businessOwnerOverridden = Boolean(recommendedBusinessOwnerKey) &&
    !ownersMatch(ticket.businessOwner, recommendedBusinessOwnerKey);
  const hasManualOverride =
    Boolean(override) || synitiOwnerOverridden || businessOwnerOverridden;

  const workloadKeys = useMemo(
    () => (decision ? collectWorkloadOwnerKeys(decision, ticket, explanation) : []),
    [decision, ticket, explanation],
  );

  const workloadSummaryByOwner = useMemo(() => {
    const summaries = new Map<string, OwnerWorkloadSummaryDto>();
    for (const summary of workload?.summaries ?? []) {
      summaries.set(normalizeOwnerToken(summary.ownerKey), summary);
    }
    return summaries;
  }, [workload]);

  const synitiRecommendationWorkload = recommendedSynitiOwnerKey
    ? workloadSummaryByOwner.get(normalizeOwnerToken(recommendedSynitiOwnerKey))
    : null;
  const businessRecommendationWorkload = recommendedBusinessOwnerKey
    ? workloadSummaryByOwner.get(normalizeOwnerToken(recommendedBusinessOwnerKey))
    : null;

  useEffect(() => {
    if (!isModalOpen || !ticket.id || workloadKeys.length === 0) {
      setWorkload(null);
      setWorkloadLoading(false);
      return;
    }

    let cancelled = false;

    (async () => {
      setWorkloadLoading(true);
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const preview = await ticketService.postWorkloadPreview(
          {
            ownerKeys: workloadKeys,
            excludeTicketId: ticket.id,
          },
          token,
        );
        if (!cancelled) {
          setWorkload(preview);
        }
      } catch {
        if (!cancelled) {
          setWorkload(null);
        }
      } finally {
        if (!cancelled) {
          setWorkloadLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    isModalOpen,
    ticket.id,
    workloadKeys,
    refreshNonce,
    getAccessTokenSilently,
  ]);

  const workloadPreview = useMemo(() => {
    if (!workload?.summaries?.length) {
      return null;
    }
    return aggregateWorkload(workload.summaries);
  }, [workload]);

  const isLightweightNoRec =
    decision != null &&
    !hasOwnerRecommendation(decision) &&
    decision.outcomeType === "Fallback";

  const noRuleGuidanceCopy = useMemo(() => {
    const reason = decision?.noMatchReason?.trim();
    switch (reason) {
      case "NoRulesDefined":
        return "No routing rules are configured. Add a routing rule so Cortex can recommend an owner.";
      case "NoEnabledRules":
        return "All routing rules are disabled. Enable a rule so Cortex can recommend an owner.";
      case "MissingRequiredFactors":
        return "Required routing inputs are missing. Add a routing rule that matches the available ticket factors.";
      case "NoCriteriaMatched":
      default:
        return "No routing rule matched this ticket. Add or adjust a routing rule to enable owner recommendations.";
    }
  }, [decision?.noMatchReason]);

  const routingReasoningEmptyCopy = isLightweightNoRec
    ? noRuleGuidanceCopy
    : "No routing signals were applied for this recommendation.";
  const decisionImpact = ticket.decisionImpact?.hasImpact
    ? ticket.decisionImpact
    : null;
  const decisionImpactImproved = Boolean(
    decisionImpact?.riskImproved ||
      decisionImpact?.workloadImproved ||
      decisionImpact?.pressureImproved,
  );
  const currentOwnerWorkloadScore = ticket.synitiOwner
    ? workloadSummaryByOwner.get(normalizeOwnerToken(ticket.synitiOwner))
        ?.workloadScore ?? null
    : null;
  const recommendedOwnerWorkloadScore = synitiRecommendationWorkload?.workloadScore ?? null;
  const workloadDifference =
    currentOwnerWorkloadScore != null && recommendedOwnerWorkloadScore != null
      ? recommendedOwnerWorkloadScore - currentOwnerWorkloadScore
      : null;
  const workloadComparisonLabel =
    workloadDifference == null
      ? "—"
      : workloadDifference <= 0
        ? "Recommended owner has equal or lower workload"
        : "Recommended owner has higher workload; routing signals still ranked this owner first";
  const recommendationStrength = cortexDecision
    ? cortexDecision.confidenceScore >= 0.8
      ? "High"
      : cortexDecision.confidenceScore >= 0.55
        ? "Medium"
        : "Low"
    : "—";
  const expectedImpactSummary = decisionImpact?.summary ??
    (workloadDifference != null && workloadDifference <= 0
      ? "Clearer ownership with lower workload pressure."
      : "Clearer ownership based on matched routing signals.");
  const actionState =
    hasManualOverride || recommendationStrength === "Low" || isLightweightNoRec
      ? "Needs review"
      : synitiOwnerOverridden || businessOwnerOverridden
        ? "Ready to apply"
        : "Already aligned";
  const compactDecisionState = actionState;
  const compactDecisionStateClass =
    compactDecisionState === "Ready to apply"
      ? "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100"
      : compactDecisionState === "Needs review"
        ? "bg-amber-100 text-amber-950 dark:bg-amber-950/50 dark:text-amber-50"
        : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";
  const compactWhyLine =
    selectedBecauseLines[0] ??
    cortexDecision?.summary ??
    expectedImpactSummary ??
    routingReasoningEmptyCopy;
  const isHighRisk = riskLevel === "High";

  if (!ticket.id) {
    return null;
  }

  return (
    <div
      id="cortex-decision-panel"
      className={`px-4 py-4 transition-colors ${
        highlightPanel
          ? "rounded-md border border-amber-300 bg-amber-50/60 dark:border-amber-700 dark:bg-amber-950/25"
          : ""
      }`}
    >
      <button
        type="button"
        onClick={() => setDecisionPanelExpanded((open) => !open)}
        className="flex w-full items-start justify-between gap-2 border-b border-slate-100 pb-3 text-left transition-colors hover:bg-slate-50/50 dark:border-slate-800/80 dark:hover:bg-slate-900/30"
        id="cortex-decision-panel-header"
      >
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-semibold tracking-tight text-slate-900 dark:text-slate-50">
            Cortex Decision
          </h3>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
            Recommended ownership, action state, and concise reasoning
            {isLiveRoutingPreview && previewLoading && decision ? (
              <span className="ml-1.5 font-medium text-slate-400 dark:text-slate-500">
                · Updating…
              </span>
            ) : null}
          </p>
          {isHighRisk ? (
            <p className="mt-1 text-xs font-medium text-amber-800 dark:text-amber-200">
              ⚠️ This ticket is at risk - review recommended action below.
            </p>
          ) : null}
        </div>
        <span
          className="mt-0.5 shrink-0 text-slate-400 dark:text-slate-500"
          aria-hidden="true"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 24 24"
            fill="none"
            strokeWidth={2}
            stroke="currentColor"
            className={`h-5 w-5 transition-transform duration-200 ${
              decisionPanelExpanded ? "rotate-0" : "-rotate-90"
            }`}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="m19.5 8.25-7.5 7.5-7.5-7.5"
            />
          </svg>
        </span>
      </button>

      <div
        id="cortex-decision-panel-body"
        role="region"
        aria-labelledby="cortex-decision-panel-header"
        hidden={!decisionPanelExpanded}
      >
        {decisionPanelExpanded ? (
          !isLiveRoutingPreview && loading ? (
          <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
            Loading…
          </p>
        ) : error ? (
          <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
            {error}
          </p>
        ) : !decision ? (
          <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
            {isLiveRoutingPreview && (previewLoading || loading)
              ? "Updating recommendation…"
              : isLiveRoutingPreview
                ? "Live recommendation isn’t available. Try again in a moment."
                : "No Cortex recommendation is on file yet. One is recorded when the ticket is created or recommendation inputs change."}
          </p>
        ) : (
          // Intentionally NOT its own scroll container — this section used to
          // set `max-h + overflow-y-auto` which, combined with
          // `overscroll-behavior: contain` from `.scroll-surface`, trapped
          // wheel scroll at the bottom of Cortex Decision and blocked handoff
          // to the side-panel scroll that already owns vertical scrolling for
          // the ticket modal. The section now flows at its natural height and
          // the parent side panel scrolls through it.
          <div
            className={`mt-4 space-y-4 text-sm text-slate-800 dark:text-slate-100 ${
              isLiveRoutingPreview && previewLoading
                ? "opacity-[0.88] transition-opacity duration-200"
                : ""
            }`}
          >
            <div className="rounded-xl border border-cortex-blue/20 bg-sky-50/70 px-4 py-3.5 shadow-sm dark:border-sky-900/60 dark:bg-sky-950/20">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <p className="text-sm font-semibold tracking-wide text-cortex-blue dark:text-sky-300">
                  Cortex Recommendation
                </p>
                <span
                  className={`rounded-md px-2.5 py-1 text-xs font-semibold ${compactDecisionStateClass}`}
                >
                  {compactDecisionState}
                </span>
              </div>
              <div className="mt-3 space-y-2 text-sm text-slate-800 dark:text-slate-100">
                <p>
                  <span className="font-semibold text-slate-900 dark:text-slate-100">
                    Recommended Ownership
                  </span>
                  {isHighRisk ? (
                    <button
                      type="button"
                      onClick={onRecommendedOwnerClick}
                      className="ml-2 text-xs font-semibold text-cortex-blue underline-offset-2 hover:underline dark:text-cortex-cyan"
                    >
                      Check risk context
                    </button>
                  ) : null}
                  <span className="block mt-0.5 text-slate-500 dark:text-slate-400">
                    Syniti Owner:{" "}
                    <span className="text-slate-900 dark:text-slate-100">
                      {synitiOwnerDisplay}
                    </span>
                  </span>
                  <span className="block text-slate-500 dark:text-slate-400">
                    Business Owner:{" "}
                    <span className="text-slate-900 dark:text-slate-100">
                      {businessOwnerDisplay}
                    </span>
                  </span>
                </p>
                <p>
                  <span className="font-semibold text-slate-900 dark:text-slate-100">
                    Reasoning
                  </span>
                  <span className="block mt-0.5 text-slate-900 dark:text-slate-100">
                    {compactWhyLine}
                  </span>
                </p>
                <p>
                  <span className="font-semibold text-slate-900 dark:text-slate-100">
                    Suggested Action State
                  </span>
                  <span className="block mt-0.5 text-slate-900 dark:text-slate-100">
                    {compactDecisionState}
                  </span>
                </p>
              </div>
              {hasManualOverride ? (
                <div className="mt-3 rounded-md border border-amber-200 bg-amber-50/90 px-3 py-2 dark:border-amber-800/60 dark:bg-amber-950/30">
                  <p className="text-xs font-semibold text-amber-900 dark:text-amber-200">
                    ⚠️ Manual Override Detected
                  </p>
                  <p className="mt-0.5 text-xs text-amber-800 dark:text-amber-300">
                    Cortex recommendation was not applied. Current assignment reflects user-selected values.
                  </p>
                </div>
              ) : null}
            </div>

            <button
              type="button"
              onClick={() => setFullReasoningExpanded((open) => !open)}
              aria-expanded={fullReasoningExpanded ? "true" : "false"}
              aria-controls="cortex-decision-full-reasoning"
              className="text-xs font-semibold text-cortex-blue underline-offset-2 hover:text-cortex-blue-dark hover:underline dark:text-cortex-cyan"
            >
              {fullReasoningExpanded ? "Hide full reasoning" : "Show full reasoning"}
            </button>

            <div id="cortex-decision-full-reasoning" hidden={!fullReasoningExpanded}>
              {fullReasoningExpanded ? (
                <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-100 bg-slate-50/70 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
            <div className="min-w-0">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Decision Refresh
              </p>
              <p className="mt-0.5 text-xs text-slate-600 dark:text-slate-300">
                Re-runs the decision view only; final assignment is unchanged.
              </p>
            </div>
            <button
              type="button"
              onClick={() => setRefreshNonce((current) => current + 1)}
              disabled={loading || previewLoading || workloadLoading}
              className="rounded-md border border-cortex-blue/40 bg-white px-3 py-1.5 text-xs font-semibold text-cortex-blue transition hover:bg-cortex-blue/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-cortex-blue/50 dark:bg-slate-950 dark:text-cortex-cyan dark:hover:bg-slate-900"
            >
              {loading || previewLoading ? "Refreshing..." : "Re-run Decision"}
            </button>
          </div>

          {cortexDecision ? (
            <div className="rounded-lg border border-slate-200/90 bg-slate-50/70 px-3 py-3 dark:border-slate-700 dark:bg-slate-900/40">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Cortex Decision Context
              </p>
              <p className="mt-1 text-sm font-medium text-slate-900 dark:text-slate-100">
                {cortexDecision.summary}
              </p>
              <div className="mt-2 grid gap-1 text-xs text-slate-500 dark:text-slate-400 sm:grid-cols-2">
                <p>
                  Recommended owner: {synitiOwnerDisplay}
                </p>
                <p>
                  Final owner: {finalSynitiOwnerDisplay}
                </p>
                <p>
                  Decision strength: {recommendationStrength}
                </p>
                <p>
                  Manual override status: {hasManualOverride ? "Yes" : "No"}
                </p>
              </div>
            </div>
          ) : null}
          {/* A. Workload Signal - hero */}
          <div className="rounded-xl border border-slate-200/90 bg-gradient-to-br from-slate-50 to-white px-4 py-3.5 shadow-sm ring-1 ring-slate-200/60 dark:border-slate-600/70 dark:from-slate-900/80 dark:to-slate-950/60 dark:ring-slate-700/50">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">
                Workload Signal
              </span>
              <CortexTooltip content={WORKLOAD_SIGNAL_ABOUT_TOOLTIP}>
                <button
                  type="button"
                  className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-slate-200/90 bg-white text-slate-500 shadow-sm transition-colors hover:bg-slate-50 hover:text-slate-700 focus:outline-none focus:ring-2 focus:ring-slate-400/60 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200 dark:focus:ring-slate-500/50"
                  aria-label="About Workload Signal"
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 24 24"
                    fill="currentColor"
                    className="h-3.5 w-3.5"
                    aria-hidden="true"
                  >
                    <path
                      fillRule="evenodd"
                      d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm8.706-1.442c1.146-.573 2.437.463 2.126 1.706l-.709 2.836.042-.02a.75.75 0 1 1-.671 1.34l-.04-.022c-.666.44-1.567.22-2.006-.72l-.708-2.836-.042.02a.75.75 0 1 1 .671-1.34l.041.022ZM12 9a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Z"
                      clipRule="evenodd"
                    />
                  </svg>
                </button>
              </CortexTooltip>
              {workloadLoading ? (
                <span className="text-sm font-semibold text-slate-500 dark:text-slate-400">
                  …
                </span>
              ) : workloadPreview ? (
                <CortexTooltip content={WORKLOAD_SIGNAL_TOOLTIP[workloadPreview.signal]}>
                  <span
                    className={`cursor-help rounded-md px-2.5 py-1 text-sm font-semibold shadow-sm ${workloadSignalClassName(
                      workloadPreview.signal,
                    )}`}
                  >
                    {workloadPreview.signal}
                  </span>
                </CortexTooltip>
              ) : (
                <span className="text-sm font-medium text-slate-500 dark:text-slate-400">
                  No workload data
                </span>
              )}
            </div>
            {workloadLoading ? (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Estimating workload…
              </p>
            ) : workloadPreview ? (
              <p className="mt-2 text-sm font-medium leading-snug text-slate-800 dark:text-slate-100">
                {workloadPreview.sentence}
              </p>
            ) : workloadKeys.length === 0 ? (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Add owners to see workload context.
              </p>
            ) : (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Workload is not shown right now; recommendation details below still
                apply.
              </p>
            )}
          </div>

          {decisionImpact ? (
            <div
              className={`rounded-lg border px-3 py-2 ${
                decisionImpactImproved
                  ? "border-emerald-200/90 bg-emerald-50/70 dark:border-emerald-900/60 dark:bg-emerald-950/20"
                  : "border-slate-200/90 bg-slate-50/70 dark:border-slate-700 dark:bg-slate-900/40"
              }`}
            >
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  Cortex Impact
                </p>
                <span
                  className={
                    decisionImpactImproved
                      ? "rounded-md bg-emerald-100 px-2 py-0.5 text-[11px] font-semibold text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-100"
                      : "rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300"
                  }
                >
                  {decisionImpactImproved ? "Improved" : "Neutral"}
                </span>
              </div>
              <p className="mt-1 text-sm font-medium text-slate-800 dark:text-slate-100">
                {decisionImpact.summary}
              </p>
              <div className="mt-1.5 grid gap-1 text-xs text-slate-600 dark:text-slate-300 sm:grid-cols-3">
                <span>
                  Risk: {toTitleCaseWord(decisionImpact.previousRiskLevel)} →{" "}
                  {toTitleCaseWord(decisionImpact.currentRiskLevel)}
                </span>
                <span>
                  Workload: {Math.max(0, decisionImpact.previousOwnerWorkload)} →{" "}
                  {Math.max(0, decisionImpact.currentOwnerWorkload)}
                </span>
                <span>
                  Pressure:{" "}
                  {toTitleCaseWord(decisionImpact.previousPressureLevel)} →{" "}
                  {toTitleCaseWord(decisionImpact.currentPressureLevel)}
                </span>
              </div>
              <p className="mt-1.5 text-[11px] text-slate-500 dark:text-slate-400">
                Reassigned via {humanizeImpactSource(decisionImpact.source)}
              </p>
            </div>
          ) : null}

          {/* B. Cortex recommendation */}
          <div className="space-y-3">
            <div className="rounded-lg border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/40">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Cortex Decision Context
              </p>
              <div className="mt-1 grid gap-1 text-sm text-slate-700 dark:text-slate-200 sm:grid-cols-2">
                <p>Priority: {ticket.priority || "—"}</p>
                <p>SLA status: {ticket.slaStatus || "—"}</p>
                <p>
                  Current owner workload:{" "}
                  {currentOwnerWorkloadScore != null
                    ? Math.max(0, currentOwnerWorkloadScore)
                    : "—"}
                </p>
                <p>
                  Recommended owner workload:{" "}
                  {recommendedOwnerWorkloadScore != null
                    ? Math.max(0, recommendedOwnerWorkloadScore)
                    : "—"}
                </p>
                <p>Workload comparison: {workloadComparisonLabel}</p>
                <p>Action state: {actionState}</p>
              </div>
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2">
              {assignmentLine ? (
                <p className="text-sm font-medium text-slate-800 dark:text-slate-100">
                  {assignmentLine}
                </p>
              ) : null}
              {hasManualOverride ? (
                <CortexTooltip
                  content={
                    <div className="space-y-1">
                      <p>
                        Cortex recommended: Syniti {synitiOwnerDisplay}; Business{" "}
                        {businessOwnerDisplay}
                      </p>
                      <p>
                        Assigned: Syniti {finalSynitiOwnerDisplay}; Business{" "}
                        {finalBusinessOwnerDisplay}
                      </p>
                    </div>
                  }
                  side="left"
                >
                  <span className="cursor-help rounded-md bg-amber-100 px-2.5 py-1 text-xs font-semibold text-amber-950 dark:bg-amber-950/50 dark:text-amber-50">
                    ⚠️ Manual Override Detected
                  </span>
                </CortexTooltip>
              ) : null}
            </div>

            {!isLightweightNoRec ? (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border border-slate-100 bg-slate-50/80 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                      Recommended Syniti Owner
                    </p>
                    <RecommendationWorkloadPill
                      summary={synitiRecommendationWorkload}
                      loading={workloadLoading}
                    />
                  </div>
                  <p className="mt-1 text-sm font-medium text-slate-900 dark:text-slate-50">
                    {synitiOwnerDisplay}
                  </p>
                </div>
                <div className="rounded-lg border border-slate-100 bg-slate-50/80 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                      Recommended Business Owner
                    </p>
                    <RecommendationWorkloadPill
                      summary={businessRecommendationWorkload}
                      loading={workloadLoading}
                    />
                  </div>
                  <p className="mt-1 text-sm font-medium text-slate-900 dark:text-slate-50">
                    {businessOwnerDisplay}
                  </p>
                </div>
              </div>
            ) : (
              <p className="text-sm text-slate-700 dark:text-slate-200">
                {noRuleGuidanceCopy}
              </p>
            )}

            <div className="rounded-lg border border-slate-100 bg-white px-3 py-2.5 dark:border-slate-800 dark:bg-slate-950/40">
              <p className="text-sm font-semibold text-slate-700 dark:text-slate-200">
                Current Assignment
              </p>
              <div className="mt-1 grid gap-1 text-sm text-slate-700 dark:text-slate-200 sm:grid-cols-2">
                <p>
                  Syniti Owner:{" "}
                  <span className="font-medium text-slate-900 dark:text-slate-50">
                    {finalSynitiOwnerDisplay === "—" ? "Unassigned" : finalSynitiOwnerDisplay}
                  </span>
                </p>
                <p>
                  Business Owner:{" "}
                  <span className="font-medium text-slate-900 dark:text-slate-50">
                    {finalBusinessOwnerDisplay === "—" ? "Unassigned" : finalBusinessOwnerDisplay}
                  </span>
                </p>
              </div>
            </div>
          </div>

          {/* C. Decision reasoning */}
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Decision Factors
            </p>
            {displayedFactorLines.length > 0 ? (
              <>
                <ul className="mt-2 list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
                  {displayedFactorLines.map((line, idx) => (
                    <li key={`${idx}-${line}`}>{line}</li>
                  ))}
                </ul>
                {factorOverflow > 0 ? (
                  <p className="mt-1.5 text-xs text-slate-500 dark:text-slate-400">
                    +{factorOverflow} more
                  </p>
                ) : null}
              </>
            ) : fallbackFactorEntries.length > 0 ? (
              <ul className="mt-2 list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
                {fallbackFactorEntries.map(({ key, line }) => (
                  <li key={key}>{line}</li>
                ))}
              </ul>
            ) : (
              <p className="mt-2 text-sm text-slate-700 dark:text-slate-200">
                {routingReasoningEmptyCopy}
              </p>
            )}
          </div>

          <div className="space-y-2 rounded-lg border border-slate-100 bg-slate-50/70 px-3 py-3 dark:border-slate-800 dark:bg-slate-900/50">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Selected because
            </p>
            {selectedBecauseLines.length > 0 ? (
              <ul className="list-disc space-y-1 pl-4 text-sm text-slate-700 dark:text-slate-200">
                {selectedBecauseLines.map((line, index) => (
                  <li key={`${index}-${line}`}>{line}</li>
                ))}
              </ul>
            ) : isLightweightNoRec ? (
              <p className="text-sm text-slate-600 dark:text-slate-300">
                {noRuleGuidanceCopy}
              </p>
            ) : (
              <p className="text-sm text-slate-600 dark:text-slate-300">
                Routing signals matched, but no competing eligible owner was ranked higher on workload.
              </p>
            )}
          </div>

          <div className="space-y-2 rounded-lg border border-slate-100 bg-slate-50/70 px-3 py-3 dark:border-slate-800 dark:bg-slate-900/50">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Alternatives considered
            </p>
            {alternativesConsidered.length > 0 ? (
              <ul className="space-y-1.5 text-sm text-slate-700 dark:text-slate-200">
                {alternativesConsidered.map((entry, index) => (
                  <li key={`${entry.slotLabel}-${entry.ownerLabel}-${index}`}>
                    <span className="font-medium">{entry.ownerLabel}</span>
                    <span className="text-slate-500 dark:text-slate-400">
                      {" "}
                      ({entry.slotLabel}) - Not selected because {entry.reason}
                    </span>
                  </li>
                ))}
              </ul>
            ) : isLightweightNoRec ? (
              <p className="text-sm text-slate-600 dark:text-slate-300">
                {noRuleGuidanceCopy}
              </p>
            ) : (
              <p className="text-sm text-slate-600 dark:text-slate-300">
                Only one eligible owner was returned by the matched routing rule.
              </p>
            )}
          </div>

          {/* D. Override */}
          {override ? (
            <div className="rounded-lg border border-slate-200 bg-slate-50/90 px-3 py-2 text-xs dark:border-slate-700 dark:bg-slate-900/60">
              <p className="font-semibold text-slate-800 dark:text-slate-100">
                ⚠️ Manual Override Detected
              </p>
              <p className="mt-1 text-slate-600 dark:text-slate-300">
                {humanizeOverrideReason(override.overrideReasonType)}
                {override.overrideReasonText?.trim()
                  ? ` — ${override.overrideReasonText.trim()}`
                  : ""}
              </p>
              <p className="mt-1 text-[11px] text-slate-500 dark:text-slate-400">
                {new Date(override.createdDateUtc).toLocaleString(undefined, {
                  dateStyle: "medium",
                  timeStyle: "short",
                })}
              </p>
            </div>
          ) : null}

                </div>
              ) : null}
            </div>
          </div>
        )
        ) : null}
      </div>
    </div>
  );
}
