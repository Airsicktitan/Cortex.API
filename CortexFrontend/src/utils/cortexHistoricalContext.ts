import type {
  CortexInsight,
  CortexInsightSimilarTicket,
  CortexLearningSignal,
} from "../types/cortexInsight";

const MEMORY_CONTEXT_CONFIDENCE_THRESHOLD = 50;
const MAX_HISTORICAL_CONTEXT_BULLETS = 3;
const RESOLVED_STATUSES = new Set(["resolved", "closed", "done", "completed"]);
const ROUTING_RULES_MISSING_MESSAGE =
  "Cortex needs routing rules and eligible owners before it can make useful recommendations.";
const STARTER_INTELLIGENCE_MESSAGE =
  "Cortex is using configured routing rules and current ticket signals because no similar resolved tickets exist yet.";

function containsAnyText(
  source: string | undefined | null,
  terms: string[],
): boolean {
  const normalized = source?.toLowerCase() ?? "";
  return terms.some((term) => normalized.includes(term));
}

function isMediumOrHighSignal(confidence: string | undefined | null): boolean {
  const normalized = confidence?.trim().toLowerCase();
  return normalized === "medium" || normalized === "high";
}

function hasMediumConfidenceInsight(insight?: CortexInsight | null): boolean {
  const matches = insight?.matches ?? [];
  const learningSignals = insight?.learningSignals ?? [];
  const hasMediumLearningSignal = learningSignals.some((signal) =>
    isMediumOrHighSignal(signal.confidence),
  );

  if (matches.length === 0 && !hasMediumLearningSignal) {
    return false;
  }

  return (
    (insight?.confidenceScore ?? 0) >= MEMORY_CONTEXT_CONFIDENCE_THRESHOLD ||
    matches.some(
      (match) =>
        match.confidenceScore >= MEMORY_CONTEXT_CONFIDENCE_THRESHOLD,
    ) ||
    hasMediumLearningSignal
  );
}

function learningSignalText(signal: CortexLearningSignal): string {
  return [
    signal.signalType,
    signal.title,
    signal.description,
    ...(signal.supportingFacts ?? []),
  ]
    .join(" ")
    .toLowerCase();
}

function historicalContextFromLearningSignal(
  signal: CortexLearningSignal,
): string | null {
  if (!isMediumOrHighSignal(signal.confidence)) {
    return null;
  }

  const text = learningSignalText(signal);
  const title = signal.title?.trim().toLowerCase() ?? "";

  if (
    containsAnyText(title, ["often needed follow-up"]) ||
    containsAnyText(text, [
      "follow-up",
      "follow up",
      "clarification",
      "more detail",
      "higher-than-average comment",
      "comment activity",
    ])
  ) {
    return "Similar tickets often required follow-up before approval";
  }

  if (
    containsAnyText(text, [
      "returned",
      "rejected",
      "needs more info",
      "approval friction",
      "before approval",
    ])
  ) {
    return "Similar tickets had approval friction";
  }

  if (
    containsAnyText(title, ["strong historical performance"]) ||
    containsAnyText(text, [
      "stable assignments",
      "low override activity",
    ])
  ) {
    return "Related tickets were resolved without reassignment";
  }

  if (
    containsAnyText(title, ["strong delivery history"]) ||
    containsAnyText(text, ["assigned owner has strong delivery history"])
  ) {
    return "Prior matching tickets resolved cleanly with this owner";
  }

  if (
    containsAnyText(text, [
      "reassign",
      "reassignment",
      "reassigned",
      "override",
      "overridden",
      "reopened",
      "rework",
    ])
  ) {
    return "Related tickets often needed ownership changes";
  }

  if (
    text.includes("sla") &&
    containsAnyText(text, [
      "breach",
      "breached",
      "pressure",
      "late",
      "missed",
      "at risk",
      "elevated",
    ])
  ) {
    return "Similar tickets showed SLA pressure";
  }

  return null;
}

function historicalContextFromSimilarTicket(
  match: CortexInsightSimilarTicket,
): string | null {
  const source = [
    match.status,
    match.lastMeaningfulComment,
    match.sourceQuote,
  ].join(" ");

  if (
    containsAnyText(source, [
      "returned",
      "returned for detail",
      "returned for details",
      "rejected",
      "needs more info",
      "needsmoreinfo",
      "needs more information",
    ])
  ) {
    return "Similar tickets had approval friction";
  }

  if (
    containsAnyText(source, [
      "reopened",
      "rework",
      "reassigned",
      "reassignment",
      "owner changed",
      "ownership changed",
    ])
  ) {
    return "Related tickets often needed ownership changes";
  }

  if (
    containsAnyText(source, [
      "resolved late",
      "breached",
      "sla breach",
      "outside sla",
      "outsidesla",
    ])
  ) {
    return "Similar tickets showed SLA pressure";
  }

  return null;
}

function cleanText(value: unknown): string {
  if (value === null || value === undefined) return "";

  if (typeof value === "string") {
    return value.replace(/\s+/g, " ").trim();
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value).replace(/\s+/g, " ").trim();
  }

  return "";
}

function normalizeReason(reason: string): string {
  return cleanText(reason)
    .replace(/^matched\s+/i, "")
    .replace(/^matches\s+/i, "")
    .replace(/^similarity\s+via\s+/i, "")
    .replace(/\.$/, "");
}

function isResolvedMatch(match: CortexInsightSimilarTicket): boolean {
  return RESOLVED_STATUSES.has(cleanText(match.status).toLowerCase());
}

function trimQuote(value?: string | null, maxLength = 120): string {
  const text = cleanText(value);
  if (!text) {
    return "";
  }
  if (text.length <= maxLength) {
    return text;
  }
  return `${text.slice(0, maxLength).trim()}...`;
}

function buildMatchReasonPhrase(match: CortexInsightSimilarTicket): string {
  const reasons = (match.matchReasons ?? [])
    .map(normalizeReason)
    .filter(Boolean)
    .slice(0, 2);

  if (reasons.length === 0) {
    return "";
  }

  return reasons.join(" and ");
}

function fallbackHistoricalContextFromSimilarTicket(
  match: CortexInsightSimilarTicket,
): string | null {
  if (match.confidenceScore < MEMORY_CONTEXT_CONFIDENCE_THRESHOLD) {
    return null;
  }

  const title = cleanText(match.title);
  const quote = trimQuote(match.sourceQuote);
  const reasonPhrase = buildMatchReasonPhrase(match);
  const resolved = isResolvedMatch(match);

  if (resolved && reasonPhrase) {
    return `Resolved similar work matched on ${reasonPhrase}.`;
  }

  if (resolved && quote) {
    return `A similar resolved ticket noted: "${quote}"`;
  }

  if (resolved && title) {
    return `A related resolved ticket was found: "${trimQuote(title, 90)}".`;
  }

  if (reasonPhrase) {
    return `Similar prior tickets matched on ${reasonPhrase}, which may help guide review.`;
  }

  if (quote) {
    return `Prior ticket evidence noted: "${quote}"`;
  }

  if (title) {
    return `A prior related ticket was found: "${trimQuote(title, 90)}".`;
  }

  return null;
}

export function deriveHistoricalContextFromInsight(
  insight?: CortexInsight | null,
): string[] {
  if (!hasMediumConfidenceInsight(insight)) {
    return [];
  }

  const bullets: string[] = [];
  const addBullet = (bullet: string | null) => {
    if (
      bullet &&
      bullets.length < MAX_HISTORICAL_CONTEXT_BULLETS &&
      !bullets.includes(bullet)
    ) {
      bullets.push(bullet);
    }
  };

  for (const signal of insight?.learningSignals ?? []) {
    addBullet(historicalContextFromLearningSignal(signal));
  }

  for (const match of insight?.matches ?? []) {
    if (match.confidenceScore >= MEMORY_CONTEXT_CONFIDENCE_THRESHOLD) {
      addBullet(historicalContextFromSimilarTicket(match));
    }
  }

  if (bullets.length === 0) {
    const prioritizedMatches = [...(insight?.matches ?? [])]
      .sort((left, right) => {
        const resolvedDelta = Number(isResolvedMatch(right)) - Number(isResolvedMatch(left));
        if (resolvedDelta !== 0) {
          return resolvedDelta;
        }
        return right.confidenceScore - left.confidenceScore;
      })
      .filter((match) =>
        isResolvedMatch(match) ||
        match.confidenceScore >= MEMORY_CONTEXT_CONFIDENCE_THRESHOLD ||
        cleanText(match.sourceQuote).length > 0 ||
        (match.matchReasons?.length ?? 0) > 0,
      );

    for (const match of prioritizedMatches) {
      addBullet(fallbackHistoricalContextFromSimilarTicket(match));
    }
  }

  return bullets;
}

export function hasMeaningfulHistoricalContext(
  insight?: CortexInsight | null,
): boolean {
  return deriveHistoricalContextFromInsight(insight).length > 0;
}

export type ColdStartSignal = {
  type:
    | "historical-context"
    | "starter-intelligence"
    | "starter-setup-needed";
  title: string;
  body?: string;
  bullets: string[];
};

export function deriveColdStartSignal(params: {
  historicalContextBullets?: string[];
  hasRoutingRecommendation?: boolean;
  hasRoutingRules?: boolean;
  hasEligibleOwners?: boolean;
  approvalStatus?: string | null;
  priority?: string | null;
  board?: string | null;
}): ColdStartSignal | null {
  const historicalBullets = params.historicalContextBullets ?? [];
  if (historicalBullets.length > 0) {
    return null;
  }

  if (params.hasRoutingRules === false || params.hasEligibleOwners === false) {
    return {
      type: "starter-setup-needed",
      title: "Starter Setup Needed",
      body: ROUTING_RULES_MISSING_MESSAGE,
      bullets: [
        "Add at least one routing rule for common work types.",
        "Mark users as eligible Syniti or Business owners.",
        "Configure a fallback rule to prevent unassigned tickets.",
      ],
    };
  }

  const board = cleanText(params.board);
  const priority = cleanText(params.priority);
  const approvalStatus = cleanText(params.approvalStatus);
  const recommendationLine = params.hasRoutingRecommendation === false
    ? "No direct owner recommendation is available yet; Cortex is still evaluating configured signals."
    : "Routing recommendation reflects the strongest currently available rule and ticket signals.";
  const boardPriorityLine =
    board && priority
      ? `Routing is based on ${board} board and ${priority} priority signals.`
      : "Routing is based on the current board, priority, and configured rule match.";
  const approvalLine = approvalStatus.toLowerCase() === "pendingapproval"
    ? "Reviewers can approve or override the recommendation while intake is pending approval."
    : "Reviewers can approve or override the recommendation; Cortex will learn from outcomes over time.";

  return {
    type: "starter-intelligence",
    title: "Starter Intelligence",
    body: STARTER_INTELLIGENCE_MESSAGE,
    bullets: [
      boardPriorityLine,
      recommendationLine,
      "Historical context will appear once similar resolved tickets exist.",
      approvalLine,
    ],
  };
}
