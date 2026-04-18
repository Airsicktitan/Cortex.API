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
  matchedRuleId?: number | null;
  factors?: Record<string, string | null | undefined>;
  matchedCriteria?: string[];
  rulePriority?: number;
  weight?: number;
  candidateCount?: number;
  noMatchReason?: string;
}
