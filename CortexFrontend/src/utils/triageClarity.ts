import type { ApprovalTriagePreview } from "../types/ticket";

/**
 * Intake clarity pill — answers whether a follow-up would have been needed before execution.
 * Heuristic-only (tunable); no backend fields required.
 */
export type TriageClarityState =
  | "ready_for_execution"
  | "requires_clarification"
  | "would_have_required_follow_up";

export type TriageClarityContext = {
  title: string;
  /** Full description body (may be empty in edge cases). */
  description: string;
  /** Optional triage summary for extra signal when title/description are thin. */
  triageSummary?: string;
};

type ExecutionRiskLevel = "high" | "medium" | "low" | "unknown";

const TONE_READY =
  "border-emerald-300/90 bg-emerald-50 text-emerald-900 dark:border-emerald-800/60 dark:bg-emerald-950/45 dark:text-emerald-100";

const TONE_WARN =
  "border-amber-300/90 bg-amber-50 text-amber-950 dark:border-amber-700/80 dark:bg-amber-950/40 dark:text-amber-100";

const CLARITY_LABELS: Record<TriageClarityState, string> = {
  ready_for_execution: "Ready for execution",
  requires_clarification: "Requires clarification",
  would_have_required_follow_up: "Would have required follow-up",
};

/** Blocking = would realistically force a follow-up before work can start. */
const BLOCKING_HINT_PATTERNS: RegExp[] = [
  /which\s+(system|file|process|environment|database|server|app|instance)/i,
  /what\s+(system|file|version|error|environment|customer)/i,
  /exact\s+(error|message|failure)/i,
  /failure\s+point|stack\s*trace/i,
  /impacted\s+(users|scope|teams)/i,
  /business\s+impact/i,
  /how\s+many\s+(users|records|rows)/i,
  /critical\s+timeline|when\s+is\s+this\s+(needed|due)/i,
  /\bdeadline\b|\bdue\s+date\b/i,
  /acceptance\s+criteria/i,
  /steps?\s+to\s+reproduce|repro\s+steps/i,
  /root\s+cause/i,
  /not\s+(specified|clear|provided|known)/i,
  /unclear\s+(whether|if|what|which|how)/i,
  /which\s+customer|what\s+customer/i,
  /prod(uction)?\s+vs\.?\s*(dev|test|uat)/i,
  /what\s+data|where\s+(does|is|can)/i,
];

/** Refinement = improves quality but does not block understanding of the work. */
const REFINEMENT_HINT_PATTERNS: RegExp[] = [
  /wording|word\s+choice|copy(\s|$)/i,
  /validation\s+message(\s+text)?/i,
  /\btooltip\b/i,
  /warning\s+vs|hard[- ]?stop|soft[- ]?stop/i,
  /secondary\s+(role|impact)/i,
  /ux\s+wording|display\s+preference/i,
  /\bcosmetic\b|nice\s+to\s+have|\boptional\b/i,
  /prefer(red)?\s+(that|to|if)/i,
  /\bcolor\b|\bstyling\b|\btheme\b/i,
  /banner\s+text|minor\s+(change|tweak|adjustment)/i,
  /label\s+text|microcopy/i,
];

function normalizeExecutionRisk(
  triage: ApprovalTriagePreview | null | undefined,
): ExecutionRiskLevel {
  if (!triage) {
    return "low";
  }
  const raw = triage.potentialSlaRisk?.trim().toLowerCase();
  if (raw === "high") {
    return "high";
  }
  if (raw === "medium") {
    return "medium";
  }
  if (raw === "low") {
    return "low";
  }
  if (triage.slaRiskReason?.trim()) {
    return "unknown";
  }
  return "low";
}

function combinedTicketText(ctx: TriageClarityContext): string {
  return [ctx.title, ctx.description, ctx.triageSummary ?? ""]
    .map((s) => s.trim())
    .filter(Boolean)
    .join("\n");
}

function wordCount(text: string): number {
  return text.trim().split(/\s+/).filter(Boolean).length;
}

/**
 * True when the ticket already states a concrete action, target, and expected outcome
 * (so refinement-only hints should not imply a meeting).
 */
export function hasClearlyDefinedAsk(ctx: TriageClarityContext): boolean {
  const combined = combinedTicketText(ctx);
  if (combined.length < 24) {
    return false;
  }

  const wc = wordCount(combined);
  const hasAction = /\b(add|update|prevent|block|validate|fix|change|include|remove|disable|enable|export|import)\b/i.test(
    combined,
  );
  const hasTarget = /\b(field|column|template|validation|export|form|upload|vendor|invoice|submit|record|id|table|row|button|screen|page|report)\b/i.test(
    combined,
  );
  const hasOutcome =
    /\b(prevent|required|empty|blank|invalid|must|should\s+not|when|if|before|after|acceptance)\b/i.test(
      combined,
    );

  let score = 0;
  if (hasAction) {
    score += 1;
  }
  if (hasTarget) {
    score += 1;
  }
  if (hasOutcome) {
    score += 1;
  }
  if (wc >= 8) {
    score += 1;
  }
  if (/\bto\s+prevent\b|\bto\s+include\b|\bto\s+block\b|\badd\s+validation\b/i.test(combined)) {
    score += 1;
  }

  return score >= 3;
}

/**
 * Short or generic text with no identifiable work object — likely to need discovery.
 */
export function isVagueUnderspecifiedTicket(ctx: TriageClarityContext): boolean {
  if (hasClearlyDefinedAsk(ctx)) {
    return false;
  }

  const combined = combinedTicketText(ctx);
  const wc = wordCount(combined);
  const len = combined.length;

  if (len > 0 && len < 55 && wc <= 7) {
    return true;
  }

  if (wc <= 5 && len < 80) {
    return true;
  }

  const genericOnly =
    /^(need\s+help|help\s+with|issue|problem|question|support|assist)/i.test(
      combined.trim(),
    ) && !/\b(add|update|fix|change|prevent|validate|block)\b/i.test(combined);

  return genericOnly;
}

type HintKind = "blocking" | "refinement";

function classifyMissingHint(
  hint: string,
  clearlyDefined: boolean,
): HintKind {
  const h = hint.trim();
  if (!h) {
    return clearlyDefined ? "refinement" : "blocking";
  }

  const blockingHit = BLOCKING_HINT_PATTERNS.some((re) => re.test(h));
  const refinementHit = REFINEMENT_HINT_PATTERNS.some((re) => re.test(h));

  if (blockingHit && !refinementHit) {
    return "blocking";
  }
  if (refinementHit && !blockingHit) {
    return "refinement";
  }
  if (blockingHit && refinementHit) {
    return "blocking";
  }
  return clearlyDefined ? "refinement" : "blocking";
}

function countBlockingHints(
  hints: string[] | undefined,
  clearlyDefined: boolean,
): number {
  if (!hints?.length) {
    return 0;
  }
  return hints.filter((hint) => classifyMissingHint(hint, clearlyDefined) === "blocking")
    .length;
}

/**
 * Returns the clarity state for the intake pill. Requires triage object when used from the panel.
 */
export function getTriageClarityState(
  triage: ApprovalTriagePreview | null | undefined,
  context: TriageClarityContext,
): TriageClarityState | null {
  if (!triage) {
    return null;
  }

  const clearlyDefined = hasClearlyDefinedAsk(context);
  const vague = isVagueUnderspecifiedTicket(context);
  const risk = normalizeExecutionRisk(triage);
  const blocking = countBlockingHints(triage.missingDetailHints, clearlyDefined);

  if (risk === "high") {
    return "would_have_required_follow_up";
  }
  if (blocking >= 2) {
    return "would_have_required_follow_up";
  }
  if (vague) {
    return "would_have_required_follow_up";
  }

  if (clearlyDefined && blocking === 0) {
    return "ready_for_execution";
  }

  if (risk === "medium" || risk === "unknown") {
    return "requires_clarification";
  }
  if (blocking === 1) {
    return "requires_clarification";
  }

  return "ready_for_execution";
}

export function getTriageClarityPresentation(state: TriageClarityState): {
  label: string;
  toneClass: string;
} {
  return {
    label: CLARITY_LABELS[state],
    toneClass: state === "ready_for_execution" ? TONE_READY : TONE_WARN,
  };
}

/**
 * Convenience: state + presentation for the pill (null when no triage).
 */
export function getTriageClarityIndicator(
  triage: ApprovalTriagePreview | null | undefined,
  context: TriageClarityContext,
): { label: string; toneClass: string } | null {
  const state = getTriageClarityState(triage, context);
  if (!state) {
    return null;
  }
  return getTriageClarityPresentation(state);
}
