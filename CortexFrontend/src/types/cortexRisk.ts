export type CortexRiskLevel = "Low" | "Medium" | "High";

export interface CortexSlaRisk {
  ticketId: string;
  riskLevel: CortexRiskLevel;
  riskReasons: string[];
  recommendation: string;
  recommendationReason: string;
  confidence: number;
  slaStatus: string;
  evaluatedAtUtc: string;
}
