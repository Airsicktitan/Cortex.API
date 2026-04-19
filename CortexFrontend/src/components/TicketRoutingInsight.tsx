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
import { formatDisplayValue } from "../utils/presentation";
import { formatOwnerFieldForDisplay } from "../utils/ownerIdentity";

const API_AUDIENCE = "https://cortex-api";

/** Max bullets shown for routing factors before readability drops. */
const MAX_FACTOR_LINES = 4;

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

/** Native `title` tooltip: full explanation for SLA Risk (workload-based, not a prediction). */
const SLA_RISK_ABOUT_TOOLTIP =
  "About SLA Risk\n\nSLA Risk is a workload-based signal derived from the current open, at-risk, and breached ticket load across the assigned owners. It helps indicate whether this assignment may need closer monitoring. It is not a guaranteed SLA outcome.";

const SLA_RISK_LEVEL_TOOLTIP: Record<"Low" | "Medium" | "High", string> = {
  Low: "Current workload appears manageable.",
  Medium: "Current workload may increase SLA pressure.",
  High: "Current workload is elevated and may put SLA at risk.",
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
  return "Unknown board";
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
      return "Manual assignment";
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
    return "Current assignment was chosen manually.";
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
      return "Current assignment matches the routing recommendation.";
    }
    return "Current assignment was chosen manually.";
  }

  if (decision.outcomeType === "RuleMatch") {
    return "Routing applied a rule for this ticket.";
  }

  return "This assignment reflects manual selection.";
}

function hasOwnerRecommendation(decision: TicketRoutingDecisionDto): boolean {
  return (
    Boolean(decision.chosenSynitiOwner?.trim()) ||
    Boolean(decision.chosenBusinessOwner?.trim())
  );
}

type ConfidenceLevel = "High" | "Medium" | "Low";

function resolveConfidenceLevel(
  decision: TicketRoutingDecisionDto,
  matchedFactorCount: number,
): ConfidenceLevel {
  const raw = decision.confidenceLevel?.trim();
  if (raw === "High" || raw === "Medium" || raw === "Low") {
    return raw;
  }
  if (matchedFactorCount >= 3) {
    return "High";
  }
  if (matchedFactorCount === 2) {
    return "Medium";
  }
  return "Low";
}

function collectWorkloadOwnerKeys(
  decision: TicketRoutingDecisionDto | null,
  ticket: Ticket,
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

type SlaRiskLevel = "Low" | "Medium" | "High";

function aggregateWorkload(
  summaries: OwnerWorkloadSummaryDto[],
): {
  active: number;
  atRisk: number;
  breached: number;
  risk: SlaRiskLevel;
  sentence: string;
} {
  const active = summaries.reduce((a, s) => a + s.activeTicketCount, 0);
  const atRisk = summaries.reduce((a, s) => a + s.atRiskTicketCount, 0);
  const breached = summaries.reduce((a, s) => a + s.outsideSlaOpenCount, 0);

  let risk: SlaRiskLevel = "Low";
  if (active >= 10 || atRisk >= 3 || breached >= 2) {
    risk = "High";
  } else if (active >= 5 || atRisk >= 1 || breached >= 1) {
    risk = "Medium";
  }

  const multi = summaries.length > 1;
  let sentence: string;

  if (active === 0 && atRisk === 0 && breached === 0) {
    sentence =
      "No other active tickets are in view for the selected owners.";
  } else if (risk === "High") {
    sentence =
      "This assignment should be monitored closely based on current workload.";
  } else if (risk === "Medium") {
    sentence =
      "Current owner workload is elevated and may increase SLA risk.";
  } else if (active <= 4) {
    sentence = multi
      ? `Assigned owners currently have a light combined workload (${active} active tickets).`
      : `Assigned owners currently have a light workload (${active} active tickets).`;
  } else {
    sentence = multi
      ? `Combined workload is manageable (${active} active tickets) with SLA risk still low.`
      : `Workload is manageable (${active} active tickets) with SLA risk still low.`;
  }

  return { active, atRisk, breached, risk, sentence };
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
}

export default function TicketRoutingInsight({
  ticket,
  isModalOpen,
  ticketBoards,
  livePreview,
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
  const [techExpanded, setTechExpanded] = useState(false);
  const [decisionPanelExpanded, setDecisionPanelExpanded] = useState(true);

  const isLiveRoutingPreview = Boolean(livePreview && ticket.id);
  const debouncedPreviewTitle = useDebouncedValue(
    livePreview?.title ?? "",
    PREVIEW_TEXT_DEBOUNCE_MS,
  );
  const debouncedPreviewDepartment = useDebouncedValue(
    livePreview?.department ?? "",
    PREVIEW_TEXT_DEBOUNCE_MS,
  );

  useEffect(() => {
    setTechExpanded(false);
    setDecisionPanelExpanded(true);
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
          setData(null);
        } else {
          setData(latest);
          setPersistedOverride(null);
        }
      } catch {
        if (!cancelled) {
          setError("Routing details aren’t available right now.");
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
    getAccessTokenSilently,
  ]);

  useEffect(() => {
    if (!isModalOpen || !ticket.id || !isLiveRoutingPreview || !livePreview) {
      setLiveDecision(null);
      setPreviewLoading(false);
      return;
    }

    let cancelled = false;
    const body: RoutingPreviewRequest = {
      ticketId: ticket.id,
      boardId: livePreview.boardId,
      priority: livePreview.priority,
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
    livePreview?.boardId,
    livePreview?.priority,
    debouncedPreviewTitle,
    debouncedPreviewDepartment,
    getAccessTokenSilently,
  ]);

  const decision = isLiveRoutingPreview
    ? liveDecision
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

  const confidenceLevel = useMemo((): ConfidenceLevel | null => {
    if (!decision) {
      return null;
    }
    return resolveConfidenceLevel(decision, matchedFactorLines.length);
  }, [decision, matchedFactorLines.length]);

  const confidencePresentation = useMemo(() => {
    if (confidenceLevel === null) {
      return null;
    }
    const hasMatchedCriteria = matchedFactorLines.length > 0;
    if (confidenceLevel === "High" || confidenceLevel === "Medium") {
      return {
        kind: "standard" as const,
        text: `${confidenceLevel} confidence`,
      };
    }
    if (!hasMatchedCriteria) {
      return {
        kind: "soft" as const,
        text: "Limited routing signals available",
      };
    }
    return { kind: "standard" as const, text: "Low confidence" };
  }, [confidenceLevel, matchedFactorLines.length]);

  const workloadKeys = useMemo(
    () => (decision ? collectWorkloadOwnerKeys(decision, ticket) : []),
    [decision, ticket],
  );

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
    getAccessTokenSilently,
  ]);

  const slaPreview = useMemo(() => {
    if (!workload?.summaries?.length) {
      return null;
    }
    return aggregateWorkload(workload.summaries);
  }, [workload]);

  const isLightweightNoRec =
    decision != null &&
    !hasOwnerRecommendation(decision) &&
    decision.outcomeType === "Fallback";

  const routingReasoningEmptyCopy = isLightweightNoRec
    ? "No routing rules matched this ticket; the assignment reflects manual selection."
    : "No routing signals were applied for this decision.";

  if (!ticket.id) {
    return null;
  }

  return (
    <div className="mb-6 rounded-xl border border-slate-200/95 bg-white p-4 shadow-sm dark:border-slate-600/80 dark:bg-slate-950/40 dark:shadow-none">
      <button
        type="button"
        onClick={() => setDecisionPanelExpanded((open) => !open)}
        className="flex w-full items-start justify-between gap-2 border-b border-slate-100 pb-3 text-left transition-colors hover:bg-slate-50/50 dark:border-slate-800/80 dark:hover:bg-slate-900/30"
        aria-expanded={decisionPanelExpanded}
        aria-controls="cortex-decision-panel-body"
        id="cortex-decision-panel-header"
      >
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-semibold tracking-tight text-slate-900 dark:text-slate-50">
            Cortex Decision
          </h3>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
            Why this ticket was routed here
            {isLiveRoutingPreview && previewLoading && decision ? (
              <span className="ml-1.5 font-medium text-slate-400 dark:text-slate-500">
                · Updating…
              </span>
            ) : null}
          </p>
        </div>
        <span
          className="mt-0.5 shrink-0 text-slate-400 dark:text-slate-500"
          aria-hidden
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
              ? "Updating routing preview…"
              : isLiveRoutingPreview
                ? "Live routing preview isn’t available. Try again in a moment."
                : "No routing decision is on file yet. One is recorded when the ticket is created or routing inputs change."}
          </p>
        ) : (
          <div
            id="cortex-decision-panel-body"
            className={`mt-4 space-y-4 text-sm text-slate-800 dark:text-slate-100 ${
              isLiveRoutingPreview && previewLoading
                ? "opacity-[0.88] transition-opacity duration-200"
                : ""
            }`}
            role="region"
            aria-labelledby="cortex-decision-panel-header"
          >
          {/* A. SLA Risk — hero */}
          <div className="rounded-xl border border-slate-200/90 bg-gradient-to-br from-slate-50 to-white px-4 py-3.5 shadow-sm ring-1 ring-slate-200/60 dark:border-slate-600/70 dark:from-slate-900/80 dark:to-slate-950/60 dark:ring-slate-700/50">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wide text-slate-600 dark:text-slate-300">
                SLA Risk
              </span>
              <button
                type="button"
                className="inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full border border-slate-200/90 bg-white text-slate-500 shadow-sm transition-colors hover:bg-slate-50 hover:text-slate-700 focus:outline-none focus:ring-2 focus:ring-slate-400/60 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200 dark:focus:ring-slate-500/50"
                aria-label="About SLA Risk"
                title={SLA_RISK_ABOUT_TOOLTIP}
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                  className="h-3.5 w-3.5"
                  aria-hidden
                >
                  <path
                    fillRule="evenodd"
                    d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12Zm8.706-1.442c1.146-.573 2.437.463 2.126 1.706l-.709 2.836.042-.02a.75.75 0 1 1-.671 1.34l-.04-.022c-.666.44-1.567.22-2.006-.72l-.708-2.836-.042.02a.75.75 0 1 1 .671-1.34l.041.022ZM12 9a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>
              {workloadLoading ? (
                <span className="text-sm font-semibold text-slate-500 dark:text-slate-400">
                  …
                </span>
              ) : slaPreview ? (
                <span
                  title={SLA_RISK_LEVEL_TOOLTIP[slaPreview.risk]}
                  className={
                    slaPreview.risk === "High"
                      ? "cursor-help rounded-md bg-red-100 px-2.5 py-1 text-sm font-semibold text-red-900 shadow-sm dark:bg-red-950/60 dark:text-red-100"
                      : slaPreview.risk === "Medium"
                        ? "cursor-help rounded-md bg-amber-100 px-2.5 py-1 text-sm font-semibold text-amber-950 shadow-sm dark:bg-amber-950/50 dark:text-amber-50"
                        : "cursor-help rounded-md bg-emerald-100 px-2.5 py-1 text-sm font-semibold text-emerald-950 shadow-sm dark:bg-emerald-950/40 dark:text-emerald-100"
                  }
                >
                  {slaPreview.risk}
                </span>
              ) : (
                <span className="text-sm font-medium text-slate-500 dark:text-slate-400">
                  —
                </span>
              )}
            </div>
            {workloadLoading ? (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Estimating workload…
              </p>
            ) : slaPreview ? (
              <p className="mt-2 text-sm font-medium leading-snug text-slate-800 dark:text-slate-100">
                {slaPreview.sentence}
              </p>
            ) : workloadKeys.length === 0 ? (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Add owners to see workload-based SLA context.
              </p>
            ) : (
              <p className="mt-2 text-sm leading-snug text-slate-600 dark:text-slate-300">
                Workload isn’t shown right now; assignment details below still
                apply.
              </p>
            )}
          </div>

          {/* B. Current assignment */}
          <div className="space-y-2">
            {assignmentLine ? (
              <p className="text-sm font-medium text-slate-800 dark:text-slate-100">
                {assignmentLine}
              </p>
            ) : null}

            {!isLightweightNoRec ? (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border border-slate-100 bg-slate-50/80 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Syniti owner selected
                  </p>
                  <p className="mt-1 text-sm font-medium text-slate-900 dark:text-slate-50">
                    {formatDisplayValue(
                      formatOwnerFieldForDisplay(decision.chosenSynitiOwner) ||
                        undefined,
                    )}
                  </p>
                </div>
                <div className="rounded-lg border border-slate-100 bg-slate-50/80 px-3 py-2.5 dark:border-slate-800 dark:bg-slate-900/50">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Business owner selected
                  </p>
                  <p className="mt-1 text-sm font-medium text-slate-900 dark:text-slate-50">
                    {formatDisplayValue(
                      formatOwnerFieldForDisplay(
                        decision.chosenBusinessOwner,
                      ) || undefined,
                    )}
                  </p>
                </div>
              </div>
            ) : (
              <p className="text-sm text-slate-700 dark:text-slate-200">
                No routing rules selected owners for this ticket.
              </p>
            )}
          </div>

          {/* C. Routing reasoning */}
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Routing signals
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

          {confidencePresentation ? (
            <div className="border-t border-slate-100 pt-3 dark:border-slate-800/80">
              {confidencePresentation.kind === "soft" ? (
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {confidencePresentation.text}
                </p>
              ) : (
                <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                  <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                    Confidence
                  </span>
                  <span className="text-sm font-semibold text-slate-900 dark:text-slate-50">
                    {confidencePresentation.text}
                  </span>
                </div>
              )}
            </div>
          ) : null}

          {/* D. Override + technical */}
          {override ? (
            <div className="rounded-lg border border-slate-200 bg-slate-50/90 px-3 py-2 text-xs dark:border-slate-700 dark:bg-slate-900/60">
              <p className="font-semibold text-slate-800 dark:text-slate-100">
                Manual override recorded
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

          <div>
            <button
              type="button"
              onClick={() => setTechExpanded((open) => !open)}
              aria-expanded={techExpanded}
              className="text-xs font-medium text-slate-600 underline-offset-2 hover:text-slate-800 hover:underline dark:text-slate-400 dark:hover:text-slate-200"
            >
              {techExpanded ? "Hide technical details" : "Technical details"}
            </button>
            {techExpanded ? (
              <div className="mt-2 rounded-md border border-dashed border-slate-200/90 bg-slate-50/50 p-3 text-[11px] text-slate-500 dark:border-slate-700 dark:bg-slate-950/30 dark:text-slate-400">
                {decision.explanationText ? (
                  <p className="mb-2 text-slate-600 dark:text-slate-300">
                    {decision.explanationText}
                  </p>
                ) : null}
                {decision.matchedRuleId != null ? (
                  <p className="mb-1">
                    Rule #{decision.matchedRuleId} · Board{" "}
                    {formatDisplayValue(ticket.boardName)}
                  </p>
                ) : null}
                {explanation?.candidateCount != null ? (
                  <p className="mb-1">
                    Candidate rules evaluated: {explanation.candidateCount}
                  </p>
                ) : null}
                <p>
                  Engine {decision.engineVersion} ·{" "}
                  {new Date(decision.createdDateUtc).toLocaleString(undefined, {
                    dateStyle: "medium",
                    timeStyle: "short",
                  })}
                </p>
              </div>
            ) : null}
          </div>
        </div>
        )
      ) : null}
    </div>
  );
}
