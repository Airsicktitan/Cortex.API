/** GET /api/tickets/{id}/routing/latest */
export interface TicketRoutingLatestResponse {
  decision: TicketRoutingDecisionDto | null;
  override: TicketRoutingOverrideDto | null;
}

export interface TicketRoutingDecisionDto {
  id: number;
  ticketId: string;
  matchedRuleId: number | null;
  outcomeType: string;
  confidenceLevel: string;
  noMatchReason: string | null;
  chosenSynitiOwner: string;
  chosenBusinessOwner: string;
  precedenceScore: number;
  tieBreakKey: string;
  explanationJson: string;
  explanationText: string;
  engineVersion: string;
  createdDateUtc: string;
}

export interface TicketRoutingOverrideDto {
  id: number;
  ticketId: string;
  overriddenByUserId: number;
  previousSynitiOwner: string;
  previousBusinessOwner: string;
  newSynitiOwner: string;
  newBusinessOwner: string;
  overrideReasonType: string;
  overrideReasonText: string;
  createdDateUtc: string;
}

/** Subset of persisted explanation JSON from the routing engine. */
export interface RoutingExplanationPayload {
  engine?: string;
  formula?: string;
  confidenceClassification?: string;
  decisionType?:
    | "rule_based"
    | "workload_balanced"
    | "workload_aware_routing_v1"
    | "manual_override";
  matchedRuleId?: number | null;
  factors?: Record<string, string | null | undefined>;
  matchedCriteria?: string[];
  slots?: {
    synitiOwner?: RoutingExplanationSlotDto;
    businessOwner?: RoutingExplanationSlotDto;
  };
  rulePriority?: number;
  weight?: number;
  candidateCount?: number;
  topStaticCandidateCount?: number;
  workloadTieBreakApplied?: boolean;
  selectedWorkloadScore?: number;
  eligibleAssignees?: RoutingExplanationOwnerWorkloadDto[];
  candidateAssignments?: Array<{
    matchedRuleId: number;
    synitiOwner: string | null;
    businessOwner: string | null;
    workloadScore: number;
    ownerScores: RoutingExplanationOwnerWorkloadDto[];
  }>;
  noMatchReason?: string;
}

export interface RoutingExplanationSlotCandidateDto {
  userId?: number;
  ownerKey?: string | null;
  displayName?: string | null;
  ruleId?: number;
  matchScore?: number;
  workloadPenalty?: number;
  finalScore?: number;
  activeTicketCount?: number;
  highPriorityTicketCount?: number;
  atRiskTicketCount?: number;
  outsideSlaOpenCount?: number;
  slaRiskTicketCount?: number;
  reason?: string;
}

export interface RoutingExplanationSlotDto {
  selectedOwnerId?: number | null;
  selectedOwnerKey?: string | null;
  selectedOwnerDisplayName?: string | null;
  applied?: boolean;
  appliedReason?: string;
  classification?: string;
  candidates?: RoutingExplanationSlotCandidateDto[];
  skippedReasons?: Array<{
    ruleId?: number;
    ownerKey?: string | null;
    userId?: number | null;
    reason?: string;
    message?: string;
  }>;
}

/**
 * Explanation JSON can include either `workloadScore` (DTO shape) or `score`
 * (anonymous object shape from serializer) depending on backend version.
 */
export interface RoutingExplanationOwnerWorkloadDto {
  ownerKey: string;
  workloadScore?: number;
  score?: number;
  activeTicketCount?: number;
  highPriorityTicketCount?: number;
  atRiskTicketCount?: number;
  outsideSlaOpenCount?: number;
  slaRiskTicketCount?: number;
}

/** POST /api/tickets/routing/workload-preview */
export interface OwnerWorkloadPreviewRequest {
  ownerKeys: string[];
  excludeTicketId?: string | null;
}

export interface OwnerWorkloadSummaryDto {
  ownerKey: string;
  activeTicketCount: number;
  highPriorityTicketCount: number;
  atRiskTicketCount: number;
  outsideSlaOpenCount: number;
  slaRiskTicketCount: number;
  workloadScore: number;
}

export interface OwnerWorkloadPreviewResponse {
  summaries: OwnerWorkloadSummaryDto[];
}

/** POST /api/tickets/routing/preview — live evaluation from draft fields (no save). */
export interface RoutingPreviewRequest {
  ticketId: string;
  boardId: number;
  priority: string;
  title?: string;
  department?: string;
}

export interface RoutingPreviewResponse {
  decision: TicketRoutingDecisionDto;
}
