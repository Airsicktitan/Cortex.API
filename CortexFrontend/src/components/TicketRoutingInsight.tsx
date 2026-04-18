import { useAuth0 } from "@auth0/auth0-react";
import { useEffect, useMemo, useState } from "react";
import type { Ticket } from "../types/ticket";
import type {
  RoutingExplanationPayload,
  TicketRoutingDecisionDto,
  TicketRoutingLatestResponse,
  TicketRoutingOverrideDto,
} from "../types/ticketRoutingInsight";
import { ticketService } from "../services/api";
import { formatDisplayValue } from "../utils/presentation";
import { formatOwnerFieldForDisplay } from "../utils/ownerIdentity";

const API_AUDIENCE = "https://cortex-api";

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

function humanizeOutcome(outcomeType: string): string {
  switch (outcomeType) {
    case "RuleMatch":
      return "Matched a routing rule";
    case "Fallback":
      return "No rule matched — routing used a fallback";
    default:
      return outcomeType.replace(/([A-Z])/g, " $1").trim();
  }
}

function humanizeConfidence(level: string): string {
  switch (level) {
    case "High":
      return "High";
    case "Medium":
      return "Medium";
    case "Low":
      return "Low";
    default:
      return level;
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
  priority: "Priority",
  requesterDepartment: "Requester department",
  requesterRole: "Requester role",
  legacyDepartment: "Department",
  legacyTitle: "Title",
};

function formatFactorLabel(key: string): string {
  return FACTOR_LABELS[key] ?? key.replace(/([A-Z])/g, " $1").trim();
}

function computeRoutingSummary(
  decision: TicketRoutingDecisionDto,
  override: TicketRoutingOverrideDto | null,
  ticket: Ticket,
): string {
  if (override) {
    return "Assignment overridden from recommendation";
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
      return "Auto-assigned based on routing rules";
    }
    return "Assignment overridden from recommendation";
  }

  if (decision.outcomeType === "RuleMatch") {
    return "Auto-assigned based on routing rules";
  }

  return "Manual assignment (no matching rule)";
}

function hasOwnerRecommendation(decision: TicketRoutingDecisionDto): boolean {
  return (
    Boolean(decision.chosenSynitiOwner?.trim()) ||
    Boolean(decision.chosenBusinessOwner?.trim())
  );
}

interface TicketRoutingInsightProps {
  ticket: Ticket;
  isModalOpen: boolean;
}

export default function TicketRoutingInsight({
  ticket,
  isModalOpen,
}: TicketRoutingInsightProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [data, setData] = useState<TicketRoutingLatestResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [detailsExpanded, setDetailsExpanded] = useState(false);

  useEffect(() => {
    setDetailsExpanded(false);
  }, [ticket.id]);

  useEffect(() => {
    if (!isModalOpen || !ticket.id) {
      setData(null);
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
        if (!cancelled) {
          setData(latest);
        }
      } catch {
        if (!cancelled) {
          setError("Routing insight could not be loaded.");
          setData(null);
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
    getAccessTokenSilently,
  ]);

  const decision = data?.decision ?? null;
  const override = data?.override ?? null;

  const explanation = useMemo(
    () => (decision ? parseExplanationJson(decision.explanationJson) : null),
    [decision],
  );

  const summaryHeadline = useMemo(() => {
    if (!decision) {
      return null;
    }
    return computeRoutingSummary(decision, override, ticket);
  }, [decision, override, ticket]);

  /** No rule owner recommendation: show heavy grids only when user expands. */
  const isLightweightNoRec =
    decision != null &&
    !hasOwnerRecommendation(decision) &&
    decision.outcomeType === "Fallback";

  if (!ticket.id) {
    return null;
  }

  return (
    <div className="mb-6 rounded-lg border border-slate-200/90 bg-slate-50/80 p-4 dark:border-slate-700/80 dark:bg-slate-900/35">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            Routing insight
          </h3>
          <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
            How this ticket was assigned relative to routing rules.
          </p>
          {!loading && !error && summaryHeadline ? (
            <p className="mt-2 text-sm font-medium text-slate-800 dark:text-slate-100">
              {summaryHeadline}
            </p>
          ) : null}
        </div>
        {decision && !loading && !error ? (
          <button
            type="button"
            onClick={() => setDetailsExpanded((open) => !open)}
            aria-expanded={detailsExpanded}
            className="shrink-0 rounded-md border border-slate-200/90 bg-white/80 px-3 py-1.5 text-xs font-medium text-slate-700 shadow-sm transition-colors hover:bg-white dark:border-slate-600 dark:bg-slate-900/60 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            {detailsExpanded ? "Hide details" : "Show details"}
          </button>
        ) : null}
      </div>

      {loading ? (
        <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
          Loading routing insight…
        </p>
      ) : error ? (
        <p className="mt-3 text-sm text-amber-700 dark:text-amber-400">{error}</p>
      ) : !decision ? (
        <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
          No routing decision is stored for this ticket yet. Decisions are
          recorded when a ticket is created or when routing inputs change.
        </p>
      ) : detailsExpanded ? (
        <div className="mt-3 space-y-3 text-sm text-slate-700 dark:text-slate-200">
          {!isLightweightNoRec ? (
            <>
              <div className="grid gap-1.5 text-xs sm:grid-cols-2">
                <div>
                  <span className="text-slate-500 dark:text-slate-400">Outcome</span>
                  <p className="font-medium">{humanizeOutcome(decision.outcomeType)}</p>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400">Confidence</span>
                  <p className="font-medium">
                    {humanizeConfidence(decision.confidenceLevel)}
                  </p>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400">Board</span>
                  <p className="font-medium">{formatDisplayValue(ticket.boardName)}</p>
                </div>
                {decision.matchedRuleId != null ? (
                  <div>
                    <span className="text-slate-500 dark:text-slate-400">Rule</span>
                    <p className="font-medium">#{decision.matchedRuleId}</p>
                  </div>
                ) : null}
              </div>

              <div className="rounded-md border border-slate-200/80 bg-white/60 px-3 py-2 dark:border-slate-700/60 dark:bg-slate-950/30">
                <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  Recommended at decision time
                </p>
                <div className="mt-1 grid gap-1 text-xs sm:grid-cols-2">
                  <p>
                    <span className="text-slate-500 dark:text-slate-400">Syniti: </span>
                    {formatDisplayValue(
                      formatOwnerFieldForDisplay(decision.chosenSynitiOwner) || undefined,
                    )}
                  </p>
                  <p>
                    <span className="text-slate-500 dark:text-slate-400">Business: </span>
                    {formatDisplayValue(
                      formatOwnerFieldForDisplay(decision.chosenBusinessOwner) ||
                        undefined,
                    )}
                  </p>
                </div>
              </div>
            </>
          ) : (
            <p className="text-xs text-slate-600 dark:text-slate-300">
              No routing rule recommended specific owners. Assignments were set
              manually or by defaults.
            </p>
          )}

          {decision.explanationText ? (
            <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">
              {decision.explanationText}
            </p>
          ) : null}

          {explanation?.matchedCriteria && explanation.matchedCriteria.length > 0 ? (
            <p className="text-xs text-slate-600 dark:text-slate-300">
              <span className="font-medium text-slate-700 dark:text-slate-200">
                Matched factors:{" "}
              </span>
              {explanation.matchedCriteria.join(" · ")}
            </p>
          ) : null}

          {override ? (
            <div className="rounded-md border border-slate-200 bg-white/70 px-3 py-2 text-xs dark:border-slate-600 dark:bg-slate-950/25">
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

          <div className="rounded-md border border-dashed border-slate-200/90 bg-white/50 p-3 text-xs text-slate-600 dark:border-slate-700 dark:bg-slate-950/20 dark:text-slate-400">
            {explanation?.factors && Object.keys(explanation.factors).length > 0 ? (
              <div className="mb-2">
                <p className="mb-1 font-medium text-slate-700 dark:text-slate-200">
                  Inputs considered
                </p>
                <ul className="space-y-0.5">
                  {Object.entries(explanation.factors).map(([key, val]) => (
                    <li key={key}>
                      <span className="text-slate-500 dark:text-slate-400">
                        {formatFactorLabel(key)}:{" "}
                      </span>
                      {val && String(val).trim() ? String(val).trim() : "—"}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            {decision.noMatchReason ? (
              <p className="mb-2">
                <span className="font-medium text-slate-700 dark:text-slate-200">
                  No-match reason:{" "}
                </span>
                {decision.noMatchReason.replace(/([A-Z])/g, " $1").trim()}
              </p>
            ) : null}
            {explanation?.candidateCount != null ? (
              <p className="mb-2">
                <span className="font-medium text-slate-700 dark:text-slate-200">
                  Candidate rules evaluated:{" "}
                </span>
                {explanation.candidateCount}
              </p>
            ) : null}
            <p className="text-[11px] text-slate-500 dark:text-slate-500">
              Engine {decision.engineVersion} ·{" "}
              {new Date(decision.createdDateUtc).toLocaleString(undefined, {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
          </div>
        </div>
      ) : null}
    </div>
  );
}
