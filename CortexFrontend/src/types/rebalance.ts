/**
 * Types mirroring the Cortex.API.DTO.* response shapes emitted by
 * GET /api/rebalance/overview. Keep in sync with:
 *  - Cortex.API.DTO.RebalanceOverviewResponse
 *  - Cortex.API.DTO.OwnerWorkloadSummaryResponse
 *  - Cortex.API.DTO.RebalanceCandidateResponse
 *  - Cortex.API.DTO.RebalanceSuggestedTargetResponse
 */

/** low | moderate | high | critical */
export type PressureLevel = "low" | "moderate" | "high" | "critical";

/** low | moderate | high | critical */
export type OperationalRiskLevel = "low" | "moderate" | "high" | "critical";

/** safe | at_risk | breached */
export type SlaRiskLevel = "safe" | "at_risk" | "breached";

export interface OwnerWorkloadSummaryResponse {
  ownerId: string;
  ownerName: string;
  totalOpenTickets: number;
  highPriorityCount: number;
  slaRiskCount: number;
  workloadScore: number;
  pressureLevel: PressureLevel;
  highRiskTicketCount: number;
}

export interface RebalanceSuggestedTargetResponse {
  ownerKey: string;
  displayName: string;
  workloadScore: number;
  pressureLevel: PressureLevel;
}

export interface RebalanceCandidateResponse {
  ticketId: string;
  title: string;
  currentOwnerId: string;
  currentOwnerName: string;
  currentOwnerWorkloadScore: number;
  currentOwnerPressureLevel: PressureLevel;
  operationalRiskLevel: OperationalRiskLevel;
  slaRiskLevel: SlaRiskLevel;
  recommendedTargetCount: number;
  topSuggestedTarget: RebalanceSuggestedTargetResponse | null;
  potentialImpactSummary: string;
}

export interface RebalanceOverviewResponse {
  overloadedOwners: OwnerWorkloadSummaryResponse[];
  rebalanceCandidates: RebalanceCandidateResponse[];
}
