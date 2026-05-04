/** Read-only Tier 11 rule health (GET /settings/ticket-routing/rule-health). */
export interface RoutingRuleHealthRow {
  ruleId: number;
  ruleName: string;
  boardName: string;
  priorityName: string;
  isEnabled: boolean;
  matchCount: number;
  sampleSize: number;
  overrideCount: number;
  overridePercent: number;
  slaBreachedCount: number;
  slaSuccessPercent: number;
  returnedForDetailCount: number;
  reassignedCount: number;
  lastMatchedAtUtc: string | null;
  healthStatus: string;
  healthSummary: string;
  recommendedAction: string;
}

export interface RoutingRuleHealthOverview {
  rules: RoutingRuleHealthRow[];
}
