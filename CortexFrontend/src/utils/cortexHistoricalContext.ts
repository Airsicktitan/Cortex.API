import type {
  CortexInsight,
  CortexInsightSimilarTicket,
  CortexLearningSignal,
} from "../types/cortexInsight";

const MEMORY_CONTEXT_CONFIDENCE_THRESHOLD = 50;
const MAX_HISTORICAL_CONTEXT_BULLETS = 2;

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

  return bullets;
}
