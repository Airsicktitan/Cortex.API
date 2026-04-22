export interface WorkloadSnapshot {
  userId: string;
  displayName: string;
  activeTicketCount: number;
  highPriorityCount: number;
  slaRiskCount: number;
  workloadScore: number;
  status: "Available" | "Balanced" | "Overloaded" | string;
}

export interface CortexDecisionCandidate {
  userId: string;
  displayName: string;
  eligible: boolean;
  activeTicketCount: number;
  highPriorityCount: number;
  slaRiskCount: number;
  workloadScore: number;
  ruleMatched: boolean;
  preferredByBoard: boolean;
  currentlyOverloaded: boolean;
  totalScore: number;
  notes: string[];
}

export interface CortexDecisionResult {
  decisionType: string;
  recommendedOwnerUserId?: string | null;
  recommendedOwnerDisplayName?: string | null;
  currentOwnerUserId?: string | null;
  summary: string;
  confidenceScore: number;
  reasons: string[];
  warnings: string[];
  candidates: CortexDecisionCandidate[];
  factorBreakdown: Record<string, string>;
  aiSummary?: string | null;
  aiRiskLevel?: string | null;
  aiConfidence?: number | null;
  aiRecommendedPriority?: string | null;
  aiRecommendedOwner?: string | null;
}

export interface RebalanceSuggestion {
  ticketId: string;
  ticketKey: string;
  fromUserId: string;
  fromDisplayName: string;
  toUserId: string;
  toDisplayName: string;
  reason: string;
  expectedImpact: string;
  aiHighRisk?: boolean;
}

export interface AppliedRebalance {
  ticketId: string;
  ticketKey: string;
  fromUserId: string;
  toUserId: string;
  reason: string;
}

export interface SkippedRebalance {
  ticketId: string;
  reason: string;
}

export interface ExecuteRebalanceResponse {
  totalEvaluated: number;
  totalApplied: number;
  applied: AppliedRebalance[];
  skipped: SkippedRebalance[];
  summary: string;
  impactDetails?: string[];
}
