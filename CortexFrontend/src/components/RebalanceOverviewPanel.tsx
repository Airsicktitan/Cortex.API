import { useCallback, useEffect, useMemo, useState } from "react";
import { rebalanceService } from "../services/rebalanceService";
import { decisionService, getUserFacingErrorMessage } from "../services/api";
import type {
  OwnerWorkloadSummaryResponse,
  PressureLevel,
  RebalanceCandidateResponse,
  RebalanceOverviewResponse,
} from "../types/rebalance";
import type {
  ExecuteRebalanceResponse,
  RebalanceSuggestion,
  RebalanceSuggestionAlternative,
} from "../types/cortexDecision";

interface RebalanceOverviewPanelProps {
  getApiToken: () => Promise<string>;
  /**
   * Reuses the existing TicketModal flow. The panel itself does not embed
   * reassignment UI - clicking a row opens the standard modal so the user
   * can drive the existing review-and-apply experience.
   */
  onOpenTicket: (ticketId: string) => Promise<void> | void;
  onRebalanceApplied?: (result: ExecuteRebalanceResponse) => Promise<void> | void;
}

const PRESSURE_BADGE: Record<PressureLevel, string> = {
  low: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200",
  moderate:
    "bg-sky-100 text-sky-800 dark:bg-sky-950/30 dark:text-sky-200",
  high: "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200",
  critical: "bg-red-100 text-red-800 dark:bg-red-950/30 dark:text-red-200",
};

const PRESSURE_LABEL: Record<PressureLevel, string> = {
  low: "Low pressure",
  moderate: "Moderate pressure",
  high: "High pressure",
  critical: "Critical pressure",
};

function resolveSuggestionStrength(suggestion: RebalanceSuggestion) {
  const explicitStrength = suggestion.recommendationStrength?.trim();
  if (explicitStrength) {
    return explicitStrength;
  }

  if (suggestion.confidenceScore >= 0.7) {
    return "Strong fit";
  }
  if (suggestion.confidenceScore >= 0.35) {
    return "Good fit";
  }
  return "Limited fit";
}

function getStrengthBadgeClass(strength: string) {
  const normalized = strength.toLowerCase();
  if (normalized.includes("strong")) {
    return "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200";
  }
  if (normalized.includes("good")) {
    return "bg-sky-100 text-sky-800 dark:bg-sky-950/30 dark:text-sky-200";
  }
  return "bg-amber-100 text-amber-800 dark:bg-amber-950/30 dark:text-amber-200";
}

function getSuggestionCopyList(items: string[] | undefined, fallback: string) {
  const cleanItems = Array.isArray(items)
    ? items.filter((item) => item.trim().length > 0)
    : [];
  return cleanItems.length > 0 ? cleanItems : [fallback];
}

function getOptionalSuggestionCopyList(
  items: string[] | undefined,
  fallback?: string,
) {
  const cleanItems = Array.isArray(items)
    ? items.filter((item) => item.trim().length > 0)
    : [];
  if (cleanItems.length > 0) {
    return cleanItems;
  }

  const cleanFallback = fallback?.trim();
  return cleanFallback ? [cleanFallback] : [];
}

function getSuggestionTicketTitle(suggestion: RebalanceSuggestion) {
  const title = suggestion.ticketTitle?.trim();
  if (title) {
    return title;
  }

  return "this ticket";
}

function getSuggestionTicketMeta(suggestion: RebalanceSuggestion) {
  const key = suggestion.ticketKey?.trim();
  return key && key !== suggestion.ticketId ? key : null;
}

function getOwnerDisplayName(displayName: string | undefined | null) {
  const clean = displayName?.trim();
  return clean || "Unassigned owner";
}

function resolveCandidateStrength(
  candidate: RebalanceCandidateResponse,
  hasValidTarget: boolean,
) {
  if (!hasValidTarget || !candidate.topSuggestedTarget) {
    return "Limited fit";
  }

  const targetPressure = candidate.topSuggestedTarget.pressureLevel;
  if (
    targetPressure === "low" &&
    (candidate.operationalRiskLevel === "critical" ||
      candidate.slaRiskLevel === "breached")
  ) {
    return "Strong fit";
  }

  if (targetPressure === "low" || targetPressure === "moderate") {
    return "Good fit";
  }

  return "Limited fit";
}

function normalizeOwnerToken(value: string | undefined | null): string {
  return (value ?? "").trim().toLowerCase();
}

function isSuggestionBlockedByManualOverride(suggestion: RebalanceSuggestion) {
  return suggestion.isBlockedByManualOverride === true;
}

function getSkipReasonSummary(skipped: ExecuteRebalanceResponse["skipped"]) {
  const counts = new Map<string, number>();
  skipped.forEach((item) => {
    const normalizedReason = item.reason?.trim() || "Skipped by safety checks.";
    counts.set(normalizedReason, (counts.get(normalizedReason) ?? 0) + 1);
  });

  return [...counts.entries()].sort((left, right) => right[1] - left[1]);
}

function mapCandidateToDisplaySuggestion(
  candidate: RebalanceCandidateResponse,
): RebalanceSuggestion | null {
  if (!candidate.topSuggestedTarget) {
    return null;
  }

  return {
    ticketId: candidate.ticketId,
    ticketKey: candidate.ticketId,
    ticketTitle: candidate.title,
    fromUserId: candidate.currentOwnerId,
    fromDisplayName: getOwnerDisplayName(candidate.currentOwnerName),
    toUserId: candidate.topSuggestedTarget.ownerKey,
    toDisplayName:
      getOwnerDisplayName(candidate.topSuggestedTarget.displayName),
    selectedOwnerName: getOwnerDisplayName(candidate.topSuggestedTarget.displayName),
    previousOwnerName: getOwnerDisplayName(candidate.currentOwnerName),
    reason: candidate.potentialImpactSummary || "Rebalance recommendation.",
    expectedImpact:
      candidate.potentialImpactSummary || "Reduces workload imbalance.",
    selectionReason:
      candidate.potentialImpactSummary || "Lower-pressure owner surfaced.",
    confidenceScore: 0.5,
    recommendationStrength: "Good fit",
    rationale: [
      `${getOwnerDisplayName(candidate.currentOwnerName)} is overloaded with workload score ${candidate.currentOwnerWorkloadScore}.`,
    ],
    impactPreview: [
      `Moves work toward ${
        getOwnerDisplayName(candidate.topSuggestedTarget.displayName)
      } at ${candidate.topSuggestedTarget.pressureLevel} pressure.`,
    ],
    whyTicketBullets: [
      `${getOwnerDisplayName(candidate.currentOwnerName)} is overloaded with workload score ${candidate.currentOwnerWorkloadScore}.`,
    ],
    whyOwnerBullets: [
      `${getOwnerDisplayName(candidate.topSuggestedTarget.displayName)} is at ${candidate.topSuggestedTarget.pressureLevel} pressure.`,
    ],
    expectedImpactBullets: [
      `Moves work toward ${
        getOwnerDisplayName(candidate.topSuggestedTarget.displayName)
      } at ${candidate.topSuggestedTarget.pressureLevel} pressure.`,
    ],
    tradeoffBullets: [],
    safetyNotes: [
      "Execution applies this displayed owner only after current ownership and eligibility checks pass.",
    ],
    alternativeOwners: (candidate.alternativeTargets ?? []).map(
      (target): RebalanceSuggestionAlternative => ({
        userId: target.ownerKey,
        displayName: target.displayName,
        workloadScore: target.workloadScore,
        projectedWorkloadScore: target.workloadScore,
        pressureLevel: target.pressureLevel,
      }),
    ),
    diversificationApplied: false,
    rawTopCandidateName: getOwnerDisplayName(candidate.topSuggestedTarget.displayName),
    finalCandidateName: getOwnerDisplayName(candidate.topSuggestedTarget.displayName),
    candidateRankBeforeDiversification: 1,
    candidateRankAfterDiversification: 1,
    aiHighRisk: candidate.operationalRiskLevel === "critical",
    isBlockedByManualOverride: false,
    blockedReason: null,
  };
}

function buildDisplayedSuggestions(
  suggestions: RebalanceSuggestion[],
  candidates: RebalanceCandidateResponse[],
) {
  if (suggestions.length > 0) {
    return suggestions;
  }

  return candidates
    .map(mapCandidateToDisplaySuggestion)
    .filter((suggestion): suggestion is RebalanceSuggestion => suggestion !== null);
}

function resolveBlockedState(reason: string) {
  const normalized = reason.toLowerCase();
  if (normalized.includes("manual override")) {
    return "Rule conflict";
  }
  if (normalized.includes("owner no longer eligible")) {
    return "Owner no longer eligible";
  }
  if (normalized.includes("rule conflict")) {
    return "Rule conflict";
  }
  if (normalized.includes("missing required data")) {
    return "Missing required data";
  }
  if (normalized.includes("previous owner") || normalized.includes("ping-pong")) {
    return "Move blocked to prevent repeated reassignment (workload instability)";
  }
  if (normalized.includes("stale") || normalized.includes("changed")) {
    return "Stale";
  }
  return "Blocked";
}

function resolveBlockedConstraintExplanation(blockedState: string): string {
  if (blockedState === "Rule conflict") {
    return "Blocked: Rule conflict. Current ownership controls this ticket, so Cortex will not apply a different move silently.";
  }
  if (blockedState === "Owner no longer eligible") {
    return "Blocked: Owner no longer eligible. Refresh rebalance to review a new recommendation.";
  }
  if (blockedState === "Missing required data") {
    return "Blocked: Missing required data. Review the ticket before applying a rebalance move.";
  }
  if (
    blockedState ===
    "Move blocked to prevent repeated reassignment (workload instability)"
  ) {
    return "Blocked: Recent rebalance would move this ticket back to its previous owner.";
  }
  if (blockedState === "Stale") {
    return "Stale: Ticket changed since suggestion was generated. Cortex will not choose a different owner silently.";
  }
  return "Blocked: Cortex could not safely apply this recommendation.";
}

export default function RebalanceOverviewPanel({
  getApiToken,
  onOpenTicket,
  onRebalanceApplied,
}: RebalanceOverviewPanelProps) {
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
  const [executionAppliedCount, setExecutionAppliedCount] = useState(0);
  const [executionEvaluatedCount, setExecutionEvaluatedCount] = useState(0);
  const [executionSkipSummary, setExecutionSkipSummary] = useState<
    Array<[string, number]>
  >([]);
  const [preflightAppliedTicketIds, setPreflightAppliedTicketIds] = useState<
    Set<string>
  >(new Set());
  const [preflightBlockedReasonByTicketId, setPreflightBlockedReasonByTicketId] =
    useState<Map<string, string>>(new Map());
  const [overrideTarget, setOverrideTarget] = useState<RebalanceSuggestion | null>(
    null,
  );
  const [overrideSubmitting, setOverrideSubmitting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = await getApiToken();
      const [response, dynamicSuggestions] = await Promise.all([
        rebalanceService.getOverview(token),
        decisionService.getRebalanceSuggestions(token),
      ]);
      const nextDisplayedSuggestions = buildDisplayedSuggestions(
        dynamicSuggestions,
        response.rebalanceCandidates ?? [],
      );
      const preflight = await rebalanceService.executeRebalance(
        token,
        nextDisplayedSuggestions,
        { dryRun: true },
      );
      setOverview(response);
      setSuggestions(dynamicSuggestions);
      setPreflightAppliedTicketIds(
        new Set(preflight.applied.map((item) => item.ticketId)),
      );
      setPreflightBlockedReasonByTicketId(
        new Map(preflight.skipped.map((item) => [item.ticketId, item.reason])),
      );
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

  const overloadedOwners = overview?.overloadedOwners ?? [];
  const candidates = overview?.rebalanceCandidates ?? [];
  const displayedSuggestions = useMemo(
    () => buildDisplayedSuggestions(suggestions, candidates),
    [candidates, suggestions],
  );
  const actionableSuggestions = useMemo(
    () =>
      displayedSuggestions.filter(
        (suggestion) =>
          preflightAppliedTicketIds.has(suggestion.ticketId) &&
          !isSuggestionBlockedByManualOverride(suggestion),
      ),
    [displayedSuggestions, preflightAppliedTicketIds],
  );
  const blockedSuggestions = useMemo(
    () =>
      displayedSuggestions.filter(
        (suggestion) =>
          !preflightAppliedTicketIds.has(suggestion.ticketId) ||
          isSuggestionBlockedByManualOverride(suggestion),
      ),
    [displayedSuggestions, preflightAppliedTicketIds],
  );
  const readySuggestionCount = actionableSuggestions.length;
  const blockedSuggestionCount = blockedSuggestions.length;
  const hasAnyData = overloadedOwners.length > 0 || candidates.length > 0;

  const handleExecuteRebalance = useCallback(async () => {
    setExecuting(true);
    setError(null);
    setExecutionSummary(null);
    setExecutionImpactDetails([]);
    setExecutionAppliedCount(0);
    setExecutionEvaluatedCount(0);
    setExecutionSkipSummary([]);
    try {
      const token = await getApiToken();
      const result = await rebalanceService.executeRebalance(
        token,
        actionableSuggestions,
      );
      setExecutionSummary(result.summary);
      setExecutionImpactDetails(result.impactDetails ?? []);
      setExecutionAppliedCount(result.totalApplied ?? 0);
      setExecutionEvaluatedCount(result.totalEvaluated ?? 0);
      setExecutionSkipSummary(getSkipReasonSummary(result.skipped ?? []));
      if (result.totalApplied === 0 && result.skipped.length > 0) {
        setExecutionSummary("No executable actions were available.");
      }
      await onRebalanceApplied?.(result);
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
  }, [actionableSuggestions, getApiToken, load, onRebalanceApplied]);

  const handleConfirmManualOverride = useCallback(async () => {
    if (!overrideTarget) {
      return;
    }

    setOverrideSubmitting(true);
    setError(null);
    try {
      const token = await getApiToken();
      const result = await rebalanceService.executeRebalance(
        token,
        [overrideTarget],
        { confirmedManualOverrideTicketIds: [overrideTarget.ticketId] },
      );
      setExecutionSummary(result.summary);
      setExecutionImpactDetails(result.impactDetails ?? []);
      setExecutionAppliedCount(result.totalApplied ?? 0);
      setExecutionEvaluatedCount(result.totalEvaluated ?? 0);
      setExecutionSkipSummary(getSkipReasonSummary(result.skipped ?? []));
      setOverrideTarget(null);
      await onRebalanceApplied?.(result);
      await load();
    } catch (caughtError) {
      setError(
        getUserFacingErrorMessage(
          caughtError,
          "Unable to apply manual override recommendation",
        ),
      );
    } finally {
      setOverrideSubmitting(false);
    }
  }, [getApiToken, load, onRebalanceApplied, overrideTarget]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="space-y-6">
        <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
              Operational Rebalance
            </h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Prioritized view of overloaded owners, recommended corrections,
              and expected operational impact. Each move is grounded in
              workload, risk, and routing signals.
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
              disabled={executing || loading || readySuggestionCount === 0}
              className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:opacity-60"
            >
              {executing
                ? "Applying..."
                : `Apply Rebalance (${readySuggestionCount} actions)`}
            </button>
          </div>
        </div>
        {blockedSuggestionCount > 0 ? (
          <p className="mt-3 text-xs text-amber-700 dark:text-amber-300">
            {blockedSuggestionCount} item
            {blockedSuggestionCount === 1 ? " requires" : "s require"} review
            and will not be included in bulk apply.
          </p>
        ) : null}
        </section>

        {executionSummary && (
          <section className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-900/50 dark:bg-emerald-950/20">
            <p className="text-sm font-semibold text-emerald-800 dark:text-emerald-200">
              Rebalance Execution Result
            </p>
            <p className="mt-1 text-sm text-emerald-700 dark:text-emerald-300">
              Applied {executionAppliedCount} of {executionEvaluatedCount} ready
              recommendation
              {executionEvaluatedCount === 1 ? "" : "s"}.
            </p>
            {executionSkipSummary.length > 0 ? (
              <ul className="mt-2 list-disc space-y-1 pl-4 text-xs text-emerald-700 dark:text-emerald-300">
                {executionSkipSummary.map(([reason, count]) => (
                  <li key={`${reason}-${count}`}>
                    {count} skipped - {reason}
                  </li>
                ))}
              </ul>
            ) : null}
            <p className="mt-2 text-xs text-emerald-700/90 dark:text-emerald-300/90">
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
          actionableSuggestions={actionableSuggestions}
          blockedSuggestions={blockedSuggestions}
          blockedReasonByTicketId={preflightBlockedReasonByTicketId}
          loading={loading && !overview}
          onOpenTicket={onOpenTicket}
          onOverrideAndApply={(suggestion) => setOverrideTarget(suggestion)}
        />

        {!loading && !error && overview && !hasAnyData && (
          <section className="rounded-lg border border-gray-200 bg-white p-6 text-center dark:border-slate-800 dark:bg-slate-900">
            <p className="text-sm text-gray-600 dark:text-slate-400">
              No overloaded owners or rebalance opportunities right now. Check
              back as the queue evolves.
            </p>
          </section>
        )}

        {overrideTarget ? (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
            <div className="w-full max-w-xl rounded-lg border border-gray-200 bg-white p-6 text-gray-900 shadow-xl dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100">
              <h3 className="text-lg font-semibold">Confirm Manual Override</h3>
              <p className="mt-2 text-sm text-gray-600 dark:text-slate-300">
                This ticket currently has a manual ownership override. Applying
                this recommendation will replace the current manual assignment.
              </p>
              <div className="mt-4 rounded-md border border-gray-200 bg-gray-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-800/60">
                <p>
                  Ticket:{" "}
                  <span className="font-semibold">
                    {getSuggestionTicketTitle(overrideTarget)}
                  </span>
                </p>
                <p className="mt-1">
                  Move from{" "}
                  <span className="font-medium">
                    {getOwnerDisplayName(overrideTarget.fromDisplayName)}
                  </span>{" "}
                  to{" "}
                  <span className="font-medium">
                    {getOwnerDisplayName(overrideTarget.toDisplayName)}
                  </span>
                </p>
              </div>
              <div className="mt-5 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setOverrideTarget(null)}
                  disabled={overrideSubmitting}
                  className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 disabled:opacity-60 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={() => void handleConfirmManualOverride()}
                  disabled={overrideSubmitting}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-sm text-white hover:bg-cortex-blue-dark disabled:opacity-60"
                >
                  {overrideSubmitting ? "Applying..." : "Override and apply"}
                </button>
              </div>
            </div>
          </div>
        ) : null}
    </div>
  );
}

function SuggestionDetail({
  whyTicket,
  whyOwner,
  impact,
  tradeoffs,
  safetyNotes,
  advisory,
}: {
  whyTicket: string[];
  whyOwner: string[];
  impact: string[];
  tradeoffs: string[];
  safetyNotes: string[];
  advisory: string[];
}) {
  return (
    <div className="mt-3 space-y-2 border-t border-gray-200 pt-3 dark:border-slate-700/60">
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
          Why this ticket
        </p>
        <ul className="mt-1 space-y-0.5">
          {whyTicket.map((item, i) => (
            <li
              key={i}
              className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
            >
              <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </div>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
          Why this owner
        </p>
        <ul className="mt-1 space-y-0.5">
          {whyOwner.map((item, i) => (
            <li
              key={i}
              className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
            >
              <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </div>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
          Expected Impact
        </p>
        <ul className="mt-1 space-y-0.5">
          {impact.map((item, i) => (
            <li
              key={i}
              className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
            >
              <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </div>
      {tradeoffs.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
            Tradeoffs
          </p>
          <ul className="mt-1 space-y-0.5">
            {tradeoffs.map((item, i) => (
              <li
                key={i}
                className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
              >
                <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {safetyNotes.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
            Safety notes
          </p>
          <ul className="mt-1 space-y-0.5">
            {safetyNotes.map((item, i) => (
              <li
                key={i}
                className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
              >
                <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      {advisory.length > 0 ? (
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
            Cortex advisory
          </p>
          <ul className="mt-1 space-y-0.5">
            {advisory.map((item, i) => (
              <li
                key={i}
                className="flex gap-1.5 text-xs text-gray-700 dark:text-slate-300"
              >
                <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function AlternativesSection({
  alternatives,
}: {
  alternatives: Array<{
    displayName: string;
    workloadScore: number;
    projectedWorkloadScore?: number;
    pressureLevel: string;
    reasonNotSelected?: string;
  }>;
}) {
  const items = alternatives.filter((a) => a.displayName?.trim());
  if (items.length === 0) {
    return null;
  }
  return (
    <div className="mt-2">
      <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-400 dark:text-slate-500">
        Alternatives considered
      </p>
      <ul className="mt-1 space-y-0.5">
        {items.map((alt) => (
          <li
            key={alt.displayName}
            className="flex gap-1.5 text-xs text-gray-600 dark:text-slate-400"
          >
            <span className="mt-0.5 shrink-0 select-none text-gray-400 dark:text-slate-500">·</span>
            <span>
              {alt.displayName}
              <span className="ml-1 text-gray-400 dark:text-slate-500">
                - workload {alt.projectedWorkloadScore ?? alt.workloadScore} ({alt.pressureLevel} pressure)
              </span>
              {alt.reasonNotSelected ? (
                <span className="ml-1 text-gray-500 dark:text-slate-400">
                  {alt.reasonNotSelected}
                </span>
              ) : null}
            </span>
          </li>
        ))}
      </ul>
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
                    {getOwnerDisplayName(owner.ownerName)}
                  </p>
                </div>
                <span
                  className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${PRESSURE_BADGE[owner.pressureLevel]}`}
                >
                  {PRESSURE_LABEL[owner.pressureLevel]}
                </span>
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-3 text-xs text-gray-700 dark:text-slate-300 sm:grid-cols-4 lg:grid-cols-7">
                <StatBlock label="Open tickets" value={owner.totalOpenTickets} />
                <StatBlock
                  label="High priority"
                  value={owner.highPriorityCount}
                />
                <StatBlock
                  label="SLA breached"
                  value={owner.overdueTicketCount}
                />
                <StatBlock label="Near SLA" value={owner.slaRiskCount} />
                <StatBlock label="Stale" value={owner.staleTicketCount} />
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
  actionableSuggestions: RebalanceSuggestion[];
  blockedSuggestions: RebalanceSuggestion[];
  blockedReasonByTicketId: Map<string, string>;
  loading: boolean;
  onOpenTicket: (ticketId: string) => Promise<void> | void;
  onOverrideAndApply: (suggestion: RebalanceSuggestion) => void;
}

function RebalanceCandidatesSection({
  candidates,
  actionableSuggestions,
  blockedSuggestions,
  blockedReasonByTicketId,
  loading,
  onOpenTicket,
  onOverrideAndApply,
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
          {actionableSuggestions.length + blockedSuggestions.length > 0
            ? `${actionableSuggestions.length + blockedSuggestions.length} recommendation${
                actionableSuggestions.length + blockedSuggestions.length === 1
                  ? ""
                  : "s"
              }`
            : `Top ${candidates.length} ticket${
                candidates.length === 1 ? "" : "s"
              }`}
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
      ) : actionableSuggestions.length > 0 || blockedSuggestions.length > 0 ? (
        <div className="mt-5 space-y-6">
          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              Actionable Suggestions ({actionableSuggestions.length})
            </h4>
            {actionableSuggestions.length === 0 ? (
              <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                No safe improvements available at this time.
              </p>
            ) : (
              <ul className="mt-3 space-y-3">
                {actionableSuggestions.map((suggestion) => {
                  const isOpening = openingTicketId === suggestion.ticketId;
                  const strength = resolveSuggestionStrength(suggestion);
                  const ticketTitle = getSuggestionTicketTitle(suggestion);
                  const ticketMeta = getSuggestionTicketMeta(suggestion);
                  const whyTicketItems = getSuggestionCopyList(
                    suggestion.whyTicketBullets ?? suggestion.rationale,
                    suggestion.reason || "Ticket flagged for rebalance based on workload and risk signals.",
                  );
                  const whyOwnerItems = getSuggestionCopyList(
                    suggestion.whyOwnerBullets,
                    suggestion.selectionReason ||
                      `Cortex selected ${getOwnerDisplayName(suggestion.toDisplayName)} after comparing eligible owners.`,
                  );
                  const impactItems = getSuggestionCopyList(
                    suggestion.expectedImpactBullets ?? suggestion.impactPreview,
                    suggestion.expectedImpact || "Reduces workload pressure on the current owner.",
                  );
                  const tradeoffItems = getOptionalSuggestionCopyList(
                    suggestion.tradeoffBullets,
                    suggestion.diversificationApplied
                      ? `Diversified from ${getOwnerDisplayName(suggestion.rawTopCandidateName)} to avoid concentrating new recommendations.`
                      : undefined,
                  );
                  const safetyItems = getOptionalSuggestionCopyList(
                    suggestion.safetyNotes,
                  );
                  const advisoryItems = getOptionalSuggestionCopyList([
                    suggestion.aiAdvisorySummary ?? "",
                    suggestion.aiRiskSummary ?? "",
                    suggestion.aiTradeoffSummary ?? "",
                    suggestion.aiConfidenceWording ?? "",
                  ]);
                  return (
                    <li
                      key={`${suggestion.ticketId}-${suggestion.toUserId}`}
                      className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
                    >
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="text-sm text-gray-900 dark:text-slate-100">
                            Move{" "}
                            <button
                              type="button"
                              onClick={() => void handleOpen(suggestion.ticketId)}
                              disabled={isOpening}
                              className="font-semibold text-cortex-blue hover:underline disabled:opacity-60 dark:text-sky-300"
                            >
                              {ticketTitle}
                            </button>{" "}
                            from{" "}
                            <span className="font-medium">
                              {getOwnerDisplayName(suggestion.fromDisplayName)}
                            </span>{" "}
                            to{" "}
                            <span className="font-medium">
                              {getOwnerDisplayName(suggestion.toDisplayName)}
                            </span>
                          </p>
                          {ticketMeta ? (
                            <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                              {ticketMeta}
                            </p>
                          ) : null}
                          <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                            Confidence: {Math.round((suggestion.confidenceScore ?? 0) * 100)}%
                          </p>
                        </div>
                        <span
                          className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${getStrengthBadgeClass(strength)}`}
                        >
                          {strength}
                        </span>
                      </div>
                      <SuggestionDetail
                        whyTicket={whyTicketItems}
                        whyOwner={whyOwnerItems}
                        impact={impactItems}
                        tradeoffs={tradeoffItems}
                        safetyNotes={safetyItems}
                        advisory={advisoryItems}
                      />
                      <AlternativesSection alternatives={suggestion.alternativeOwners ?? []} />
                      <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                        {suggestion.aiHighRisk ? (
                          <span className="inline-flex rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
                            Ready to apply - elevated delivery risk
                          </span>
                        ) : (
                          <span className="inline-flex rounded-full bg-gray-100 px-3 py-1 text-xs font-medium text-gray-700 dark:bg-slate-800 dark:text-slate-300">
                            Ready to apply
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() => void handleOpen(suggestion.ticketId)}
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
          </div>

          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              Blocked / Stale Suggestions ({blockedSuggestions.length})
            </h4>
            {blockedSuggestions.length === 0 ? (
              <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                No blocked recommendations.
              </p>
            ) : (
              <ul className="mt-3 space-y-3">
                {blockedSuggestions.map((suggestion) => {
                  const isOpening = openingTicketId === suggestion.ticketId;
                  const rawReason =
                    blockedReasonByTicketId.get(suggestion.ticketId) ??
                    suggestion.blockedReason ??
                    "Requires review before execution.";
                  const blockedState = resolveBlockedState(rawReason);
                  const constraintExplanation = resolveBlockedConstraintExplanation(blockedState);
                  const ticketTitle = getSuggestionTicketTitle(suggestion);
                  const ticketMeta = getSuggestionTicketMeta(suggestion);
                  const whyTicketItems = getSuggestionCopyList(
                    suggestion.whyTicketBullets ?? suggestion.rationale,
                    suggestion.reason || "Ticket flagged for rebalance based on workload and risk signals.",
                  );
                  const whyOwnerItems = getSuggestionCopyList(
                    suggestion.whyOwnerBullets,
                    suggestion.selectionReason ||
                      `Cortex selected ${getOwnerDisplayName(suggestion.toDisplayName)} after comparing eligible owners.`,
                  );
                  const impactItems = getSuggestionCopyList(
                    suggestion.expectedImpactBullets ?? suggestion.impactPreview,
                    suggestion.expectedImpact || "Reduces workload pressure on the current owner.",
                  );
                  const tradeoffItems = getOptionalSuggestionCopyList(
                    suggestion.tradeoffBullets,
                    suggestion.diversificationApplied
                      ? `Diversified from ${getOwnerDisplayName(suggestion.rawTopCandidateName)} to avoid concentrating new recommendations.`
                      : undefined,
                  );
                  const safetyItems = getOptionalSuggestionCopyList(
                    suggestion.safetyNotes,
                    constraintExplanation,
                  );
                  const advisoryItems = getOptionalSuggestionCopyList([
                    suggestion.aiAdvisorySummary ?? "",
                    suggestion.aiRiskSummary ?? "",
                    suggestion.aiTradeoffSummary ?? "",
                    suggestion.aiConfidenceWording ?? "",
                  ]);
                  return (
                    <li
                      key={`${suggestion.ticketId}-${suggestion.toUserId}-blocked`}
                      className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
                    >
                      <div className="min-w-0">
                        <p className="text-sm text-gray-900 dark:text-slate-100">
                          Move{" "}
                          <button
                            type="button"
                            onClick={() => void handleOpen(suggestion.ticketId)}
                            disabled={isOpening}
                            className="font-semibold text-cortex-blue hover:underline disabled:opacity-60 dark:text-sky-300"
                          >
                            {ticketTitle}
                          </button>{" "}
                          from{" "}
                          <span className="font-medium">
                            {getOwnerDisplayName(suggestion.fromDisplayName)}
                          </span>{" "}
                          to{" "}
                          <span className="font-medium">
                            {getOwnerDisplayName(suggestion.toDisplayName)}
                          </span>
                        </p>
                        {ticketMeta ? (
                          <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                            {ticketMeta}
                          </p>
                        ) : null}
                        <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                          Confidence: {Math.round((suggestion.confidenceScore ?? 0) * 100)}%
                        </p>
                      </div>
                      <SuggestionDetail
                        whyTicket={whyTicketItems}
                        whyOwner={whyOwnerItems}
                        impact={impactItems}
                        tradeoffs={tradeoffItems}
                        safetyNotes={safetyItems}
                        advisory={advisoryItems}
                      />
                      <AlternativesSection alternatives={suggestion.alternativeOwners ?? []} />
                      <div className="mt-3 rounded border border-rose-200 bg-rose-50/50 px-3 py-2 dark:border-rose-900/30 dark:bg-rose-950/10">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="inline-flex shrink-0 rounded-full bg-rose-100 px-2.5 py-0.5 text-xs font-medium text-rose-800 dark:bg-rose-950/30 dark:text-rose-200">
                            {blockedState}
                          </span>
                          <span className="text-xs text-rose-700 dark:text-rose-300">
                            {constraintExplanation}
                          </span>
                        </div>
                      </div>
                      <div className="mt-3 flex gap-2">
                        {blockedState === "Rule conflict" ? (
                          <button
                            type="button"
                            onClick={() => onOverrideAndApply(suggestion)}
                            className="rounded-md border border-rose-300 bg-white px-3 py-2 text-sm text-rose-700 transition-colors hover:bg-rose-50 dark:border-rose-700 dark:bg-slate-900 dark:text-rose-300 dark:hover:bg-rose-950/30"
                          >
                            Override and apply
                          </button>
                        ) : null}
                        <button
                          type="button"
                          onClick={() => void handleOpen(suggestion.ticketId)}
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
          </div>
        </div>
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
            const strength = resolveCandidateStrength(
              candidate,
              hasValidTopAlternative,
            );
            return (
              <li
                key={candidate.ticketId}
                className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-950/40"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm text-gray-900 dark:text-slate-100">
                      <button
                        type="button"
                        onClick={() => void handleOpen(candidate.ticketId)}
                        disabled={isOpening}
                        className="font-semibold text-cortex-blue hover:underline disabled:opacity-60 dark:text-sky-300"
                      >
                        {candidate.title || "Review ticket"}
                      </button>{" "}
                    </p>
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      Current owner: {getOwnerDisplayName(candidate.currentOwnerName)}
                    </p>
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      {hasValidTopAlternative && candidate.topSuggestedTarget
                        ? `Best surfaced alternative: ${getOwnerDisplayName(candidate.topSuggestedTarget.displayName)}`
                        : "No lower-pressure alternative surfaced."}
                    </p>
                  </div>
                  <span
                    className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${getStrengthBadgeClass(strength)}`}
                  >
                    {strength}
                  </span>
                </div>

                <AlternativesSection alternatives={candidate.alternativeTargets ?? []} />
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
