/**
 * Recurring Issue Intelligence — types for the Reports page.
 *
 * Data is derived on the backend from live + archived tickets using a keyword
 * signature heuristic. Resolution time values are lifecycle durations (created → closed),
 * not human work hours. The UI reflects that phrasing.
 *
 * Endpoints:
 *   GET  /api/metrics/repeat-issues?topN=N
 *   GET  /api/metrics/repeat-issues/{groupKey}
 *   POST /api/metrics/repeat-issues/{groupKey}/ai-review  (rate-limited; ai-policy)
 */

export type RepeatIssueTrendLabel = "rising" | "falling" | "stable";

export interface RepeatIssueGroupSummary {
  groupKey: string;
  representativeTitle: string;
  signatureTokens: string[];
  boardId: number;
  boardName: string;
  repeatCount: number;
  openCount: number;
  firstSeenUtc: string;
  lastSeenUtc: string;
  /** Average lifecycle duration (hours) across closed/archived tickets; null when none. */
  avgResolutionHours: number | null;
  /** Sum of lifecycle durations (hours) across closed/archived tickets. */
  totalResolutionHours: number;
  /** Sum of comments across all tickets in the group. */
  operationalTouchCount: number;
  /** last-30d count minus prior-30d count. */
  trendDelta: number;
  trendLabel: RepeatIssueTrendLabel;
}

export interface RepeatIssueOverviewResponse {
  totalRecurringGroups: number;
  ticketsInRecurringGroups: number;
  openTicketsInRecurringGroups: number;
  totalResolutionHoursInRecurringGroups: number;
  minimumGroupSize: number;
  generatedUtc: string;
  groups: RepeatIssueGroupSummary[];
}

export interface RepeatIssueTicketSummary {
  ticketId: string;
  title: string;
  priority: string;
  status: string;
  isArchived: boolean;
  createdDate: string;
  closedDate: string | null;
  resolutionHours: number | null;
  commentCount: number;
  owner: string | null;
}

export interface RepeatIssueGroupDetailResponse {
  summary: RepeatIssueGroupSummary;
  boards: string[];
  owners: string[];
  dominantPriority: string | null;
  dominantStatus: string | null;
  tickets: RepeatIssueTicketSummary[];
}

export interface RepeatIssueSuggestedStep {
  category: string;
  rationale: string;
}

export interface RepeatIssueAiReviewResponse {
  summary: string | null;
  impact: string | null;
  trendCommentary: string | null;
  commonCharacteristics: string[];
  suggestedNextSteps: RepeatIssueSuggestedStep[];
  unavailable: boolean;
  unavailableReason: string | null;
}
