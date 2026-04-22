import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { rebalanceService } from "../services/rebalanceService";
import { decisionService, getUserFacingErrorMessage } from "../services/api";
import { ScrollToBottomButton } from "./ui/ScrollToBottomButton";
import type {
  OperationalRiskLevel,
  OwnerWorkloadSummaryResponse,
  PressureLevel,
  RebalanceCandidateResponse,
  RebalanceOverviewResponse,
  SlaRiskLevel,
} from "../types/rebalance";
import type { RebalanceSuggestion } from "../types/cortexDecision";

interface RebalanceOverviewPanelProps {
  getApiToken: () => Promise<string>;
  /**
   * Reuses the existing TicketModal flow. The panel itself does not embed
   * reassignment UI — clicking a row opens the standard modal so the user
   * can drive the existing review-and-apply experience.
   */
  onOpenTicket: (ticketId: string) => Promise<void> | void;
  onRebalanceApplied?: () => Promise<void> | void;
}

const PRESSURE_BADGE: Record<PressureLevel, string> = {
  low: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200",
  moderate:
    "bg-sky-100 text-sky-800 dark:bg-sky-950/30 dark:text-sky-200",
  high: "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200",
  critical: "bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-200",
};

const RISK_BADGE: Record<OperationalRiskLevel, string> = {
  low: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200",
  moderate:
    "bg-sky-100 text-sky-800 dark:bg-sky-950/30 dark:text-sky-200",
  high: "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200",
  critical: "bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-200",
};

const SLA_BADGE: Record<SlaRiskLevel, string> = {
  safe: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200",
  at_risk:
    "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200",
  breached: "bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-200",
};

const SLA_LABEL: Record<SlaRiskLevel, string> = {
  safe: "SLA Safe",
  at_risk: "SLA At Risk",
  breached: "SLA Breached",
};

const PRESSURE_LABEL: Record<PressureLevel, string> = {
  low: "Low pressure",
  moderate: "Moderate pressure",
  high: "High pressure",
  critical: "Critical pressure",
};

function capitalize(value: string) {
  if (!value) {
    return value;
  }
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function normalizeOwnerToken(value: string | undefined | null): string {
  return (value ?? "").trim().toLowerCase();
}

export default function RebalanceOverviewPanel({
  getApiToken,
  onOpenTicket,
  onRebalanceApplied,
}: RebalanceOverviewPanelProps) {
  const rebalanceContentScrollRef = useRef<HTMLDivElement | null>(null);
  const [overview, setOverview] = useState<RebalanceOverviewResponse | null>(
    null,
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<RebalanceSuggestion[]>([]);
  const [executing, setExecuting] = useState(false);
  const [executionSummary, setExecutionSummary] = useState<string | null>(null);
  const [executionImpactDetails, setExecutionImpactDetails] = useState<string[]>(
    [],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = await getApiToken();
      const response = await rebalanceService.getOverview(token);
      const dynamicSuggestions = await decisionService.getRebalanceSuggestions(token);
      setOverview(response);
      setSuggestions(dynamicSuggestions);
    } catch (caughtError) {
      setError(
        getUserFacingErrorMessage(
          caughtError,
          "Unable to load rebalance overview",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [getApiToken]);

  const handleExecuteRebalance = useCallback(async () => {
    setExecuting(true);
    setError(null);
    setExecutionSummary(null);
    setExecutionImpactDetails([]);
    try {
      const token = await getApiToken();
      const result = await rebalanceService.executeRebalance(token);
      setExecutionSummary(result.summary);
      setExecutionImpactDetails(result.impactDetails ?? []);
      await onRebalanceApplied?.();
      await load();
    } catch (caughtError) {
      setError(
        getUserFacingErrorMessage(
          caughtError,
          "Unable to execute rebalance actions",
        ),
      );
    } finally {
      setExecuting(false);
    }
  }, [getApiToken, load, onRebalanceApplied]);

  useEffect(() => {
    void load();
  }, [load]);

  const overloadedOwners = overview?.overloadedOwners ?? [];
  const candidates = overview?.rebalanceCandidates ?? [];

  const hasAnyData = overloadedOwners.length > 0 || candidates.length > 0;

  return (
    <div className="relative">
      <div
        ref={rebalanceContentScrollRef}
        className="scroll-surface max-h-[calc(100vh-10rem)] space-y-6 overflow-y-auto pr-1"
      >
        <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Operational Rebalance
            </h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Insight-only view of overloaded owners and the tickets most likely
              to benefit from reassignment. No bulk actions — open any ticket to
              review and apply a move through the standard flow.
            </p>
          </div>

          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => void load()}
              disabled={loading || executing}
              className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              {loading ? "Refreshing..." : "Refresh"}
            </button>
            <button
              type="button"
              onClick={() => void handleExecuteRebalance()}
              disabled={executing || loading}
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {executing ? "Applying..." : "Apply Rebalance"}
            </button>
          </div>
        </div>
        </section>

        {executionSummary && (
          <section className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-900/50 dark:bg-emerald-950/20">
            <p className="text-sm font-semibold text-emerald-800 dark:text-emerald-200">
              Rebalance Applied
            </p>
            <p className="mt-1 text-sm text-emerald-700 dark:text-emerald-300">
              {executionSummary}
            </p>
            <p className="mt-1 text-xs text-emerald-700/90 dark:text-emerald-300/90">
              {`${
                executionImpactDetails.length > 0
                  ? executionImpactDetails.length
                  : 0
              } impact signals captured`}
            </p>
            {executionImpactDetails.length > 0 ? (
              <ul className="mt-2 list-disc space-y-1 pl-4 text-xs text-emerald-700 dark:text-emerald-300">
                {executionImpactDetails.map((detail, idx) => (
                  <li key={`${idx}-${detail}`}>{detail}</li>
                ))}
              </ul>
            ) : null}
          </section>
        )}

        {error && (
          <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
            <p className="text-red-700 dark:text-red-300">{error}</p>
          </div>
        )}

        <OverloadedOwnersSection
          owners={overloadedOwners}
          loading={loading && !overview}
        />

        <RebalanceCandidatesSection
          candidates={candidates}
          suggestions={suggestions}
          loading={loading && !overview}
          onOpenTicket={onOpenTicket}
        />

        {!loading && !error && overview && !hasAnyData && (
          <section className="rounded-lg border border-gray-200 bg-white p-6 text-center dark:border-slate-800 dark:bg-slate-900">
            <p className="text-sm text-gray-600 dark:text-slate-400">
              No overloaded owners or rebalance opportunities right now. Check
              back as the queue evolves.
            </p>
          </section>
        )}
      </div>
      <ScrollToBottomButton
        containerRef={rebalanceContentScrollRef}
        aria-label="Scroll rebalance content to bottom"
      />
    </div>
  );
}

interface OverloadedOwnersSectionProps {
  owners: OwnerWorkloadSummaryResponse[];
  loading: boolean;
}

function OverloadedOwnersSection({
  owners,
  loading,
}: OverloadedOwnersSectionProps) {
  const sorted = useMemo(() => {
    // Backend already orders by workload desc, but defensive stable sort
    // guards against any future re-ordering in transit.
    return [...owners].sort(
      (left, right) => right.workloadScore - left.workloadScore,
    );
  }, [owners]);

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
          Overloaded Owners
        </h3>
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
          {sorted.length} owner{sorted.length === 1 ? "" : "s"}
        </span>
      </div>
      <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
        Owners currently at high or critical workload pressure.
      </p>

      {loading ? (
        <p className="mt-6 text-sm text-gray-500 dark:text-slate-400">
          Loading owner workload...
        </p>
      ) : sorted.length === 0 ? (
        <p className="mt-6 text-sm text-gray-500 dark:text-slate-400">
          No owners are currently overloaded.
        </p>
      ) : (
        <ul className="mt-5 space-y-3">
          {sorted.map((owner) => (
            <li
              key={owner.ownerId}
              className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-gray-900 dark:text-slate-100">
                    {owner.ownerName || owner.ownerId}
                  </p>
                  <p className="mt-0.5 text-xs text-gray-500 dark:text-slate-400">
                    Owner key: {owner.ownerId}
                  </p>
                </div>
                <span
                  className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${PRESSURE_BADGE[owner.pressureLevel]}`}
                >
                  {PRESSURE_LABEL[owner.pressureLevel]}
                </span>
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-3 text-xs text-gray-700 dark:text-slate-300 sm:grid-cols-5">
                <StatBlock label="Open tickets" value={owner.totalOpenTickets} />
                <StatBlock
                  label="High priority"
                  value={owner.highPriorityCount}
                />
                <StatBlock label="SLA risk" value={owner.slaRiskCount} />
                <StatBlock
                  label="High-risk tickets"
                  value={owner.highRiskTicketCount}
                />
                <StatBlock
                  label="Workload score"
                  value={owner.workloadScore}
                  accent
                />
              </dl>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

interface StatBlockProps {
  label: string;
  value: number;
  accent?: boolean;
}

function StatBlock({ label, value, accent }: StatBlockProps) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wide text-gray-500 dark:text-slate-400">
        {label}
      </dt>
      <dd
        className={`mt-1 text-sm font-semibold ${
          accent
            ? "text-cortex-blue dark:text-sky-300"
            : "text-gray-900 dark:text-slate-100"
        }`}
      >
        {value}
      </dd>
    </div>
  );
}

interface RebalanceCandidatesSectionProps {
  candidates: RebalanceCandidateResponse[];
  suggestions: RebalanceSuggestion[];
  loading: boolean;
  onOpenTicket: (ticketId: string) => Promise<void> | void;
}

function RebalanceCandidatesSection({
  candidates,
  suggestions,
  loading,
  onOpenTicket,
}: RebalanceCandidatesSectionProps) {
  const [openingTicketId, setOpeningTicketId] = useState<string | null>(null);

  const handleOpen = useCallback(
    async (ticketId: string) => {
      if (!ticketId) {
        return;
      }
      setOpeningTicketId(ticketId);
      try {
        await onOpenTicket(ticketId);
      } finally {
        setOpeningTicketId((current) =>
          current === ticketId ? null : current,
        );
      }
    },
    [onOpenTicket],
  );

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
          Rebalance Opportunities
        </h3>
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Top {candidates.length} ticket{candidates.length === 1 ? "" : "s"}
        </span>
      </div>
      <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
        Tickets held by overloaded owners that are themselves risky and have
        at least one lower-pressure alternative. Ranked by operational risk,
        then SLA, then owner workload.
      </p>

      {loading ? (
        <p className="mt-6 text-sm text-gray-500 dark:text-slate-400">
          Loading candidates...
        </p>
      ) : suggestions.length > 0 ? (
        <ul className="mt-5 space-y-3">
          {suggestions.map((suggestion) => {
            const isOpening = openingTicketId === suggestion.ticketId;
            return (
              <li
                key={`${suggestion.ticketId}-${suggestion.toUserId}`}
                className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
              >
                <p className="text-sm text-gray-900 dark:text-slate-100">
                  Move{" "}
                  <button
                    type="button"
                    onClick={() => void handleOpen(suggestion.ticketId)}
                    disabled={isOpening}
                    className="font-semibold text-cortex-blue hover:underline dark:text-sky-300"
                  >
                    {suggestion.ticketKey}
                  </button>{" "}
                  from{" "}
                  <span className="font-medium">
                    {suggestion.fromDisplayName || suggestion.fromUserId}
                  </span>{" "}
                  to{" "}
                  <span className="font-medium">
                    {suggestion.toDisplayName || suggestion.toUserId}
                  </span>
                </p>
                <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                  Expected impact: {suggestion.expectedImpact}
                </p>
                {suggestion.aiHighRisk ? (
                  <p className="mt-1 text-xs font-medium text-amber-700 dark:text-amber-300">
                    AI risk-aware priority: this is a high-risk ticket.
                  </p>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : candidates.length === 0 ? (
        <p className="mt-6 text-sm text-gray-500 dark:text-slate-400">
          No actionable candidates right now.
        </p>
      ) : (
        <ul className="mt-5 space-y-3">
          {candidates.map((candidate) => {
            const isOpening = openingTicketId === candidate.ticketId;
            const hasValidTopAlternative =
              candidate.topSuggestedTarget != null &&
              normalizeOwnerToken(candidate.topSuggestedTarget.ownerKey) !==
                normalizeOwnerToken(candidate.currentOwnerId) &&
              normalizeOwnerToken(candidate.topSuggestedTarget.displayName) !==
                normalizeOwnerToken(candidate.currentOwnerName);
            return (
              <li
                key={candidate.ticketId}
                className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        type="button"
                        onClick={() => void handleOpen(candidate.ticketId)}
                        disabled={isOpening}
                        className="truncate text-left text-sm font-semibold text-cortex-blue transition-colors hover:underline disabled:opacity-60 dark:text-sky-300"
                        aria-label={`Open ticket ${candidate.ticketId}: ${candidate.title}`}
                      >
                        #{candidate.ticketId}
                      </button>
                      <span className="truncate text-sm text-gray-900 dark:text-slate-100">
                        {candidate.title}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      Current owner:{" "}
                      <span className="font-medium text-gray-700 dark:text-slate-300">
                        {candidate.currentOwnerName || candidate.currentOwnerId}
                      </span>{" "}
                      · workload {candidate.currentOwnerWorkloadScore} ·{" "}
                      {PRESSURE_LABEL[candidate.currentOwnerPressureLevel]}
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${RISK_BADGE[candidate.operationalRiskLevel]}`}
                    >
                      {capitalize(candidate.operationalRiskLevel)} risk
                    </span>
                    <span
                      className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${SLA_BADGE[candidate.slaRiskLevel]}`}
                    >
                      {SLA_LABEL[candidate.slaRiskLevel]}
                    </span>
                  </div>
                </div>

                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  <div className="rounded border border-gray-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                      Suggested alternative
                    </p>
                    {hasValidTopAlternative && candidate.topSuggestedTarget ? (
                      <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold text-gray-900 dark:text-slate-100">
                            {candidate.topSuggestedTarget.displayName ||
                              candidate.topSuggestedTarget.ownerKey}
                          </p>
                          <p className="text-xs text-gray-500 dark:text-slate-400">
                            Workload {candidate.topSuggestedTarget.workloadScore}{" "}
                            · {PRESSURE_LABEL[candidate.topSuggestedTarget.pressureLevel]}
                          </p>
                        </div>
                        <span
                          className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium ${PRESSURE_BADGE[candidate.topSuggestedTarget.pressureLevel]}`}
                        >
                          {capitalize(candidate.topSuggestedTarget.pressureLevel)}
                        </span>
                      </div>
                    ) : (
                      <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                        No lower-pressure alternatives surfaced.
                      </p>
                    )}
                    {hasValidTopAlternative &&
                      candidate.recommendedTargetCount > 1 && (
                      <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                        {candidate.recommendedTargetCount - 1} additional
                        candidate
                        {candidate.recommendedTargetCount - 1 === 1 ? "" : "s"}{" "}
                        available in the review flow.
                      </p>
                    )}
                  </div>

                  <div className="rounded border border-gray-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
                    <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                      Why it surfaced
                    </p>
                    <p className="mt-2 text-sm text-gray-700 dark:text-slate-300">
                      {candidate.potentialImpactSummary ||
                        "Holding owner is overloaded and this ticket is risky."}
                    </p>
                  </div>
                </div>

                <div className="mt-3 flex justify-end">
                  <button
                    type="button"
                    onClick={() => void handleOpen(candidate.ticketId)}
                    disabled={isOpening}
                    className="rounded-md bg-cortex-blue px-3 py-2 text-sm text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
                  >
                    {isOpening ? "Opening..." : "Review ticket"}
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
