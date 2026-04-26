/**
 * User-facing Improve Request intake-assist types.
 * The server owns the canonical clarity-state vocabulary; the client only switches UI off it.
 */
export type ClarityState =
  | "ready_for_execution"
  | "requires_clarification"
  | "would_have_required_follow_up";

export interface IntakeAssistRequest {
  /** Draft title as typed by the requester; may be empty. */
  title?: string;
  /** Draft description as typed by the requester; may be empty. */
  description?: string;
  /** Optional board name for background context; never echoed back to the requester. */
  boardName?: string;
  /** Existing ticket id in edit flow; metrics only. */
  ticketId?: string;
  /** create | edit — metrics only. */
  clientFlow?: "create" | "edit";
}

export interface IntakeAssistResult {
  suggestedSummary?: string | null;
  improvedDescription?: string | null;
  missingDetails: string[];
  clarityState: ClarityState;
  guidanceMessage?: string | null;
  /** True when AI is misconfigured or failed; UI should surface a soft notice and leave the form untouched. */
  unavailable?: boolean;
  unavailableReason?: string | null;
}

/** User-friendly label for each clarity state (kept with the type so the modal and any future surface stay in sync). */
export const CLARITY_STATE_LABEL: Record<ClarityState, string> = {
  ready_for_execution: "Already review-ready — minor refinements available",
  requires_clarification: "Cortex improvement available",
  would_have_required_follow_up: "Small gaps remain",
};

/** Tailwind pill class per clarity state; intentionally muted so the assist panel reads as coaching, not an alert. */
export const CLARITY_STATE_PILL_CLASS: Record<ClarityState, string> = {
  ready_for_execution:
    "bg-emerald-100 text-emerald-800 border border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-200 dark:border-emerald-800",
  requires_clarification:
    "bg-amber-100 text-amber-900 border border-amber-200 dark:bg-amber-900/30 dark:text-amber-200 dark:border-amber-800",
  would_have_required_follow_up:
    "bg-sky-100 text-sky-900 border border-sky-200 dark:bg-sky-900/30 dark:text-sky-200 dark:border-sky-800",
};

export function isClarityState(value: unknown): value is ClarityState {
  return (
    value === "ready_for_execution" ||
    value === "requires_clarification" ||
    value === "would_have_required_follow_up"
  );
}
