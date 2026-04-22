export type CortexAiAssessment = {
  summary: string;
  recommendedPriority: string;
  recommendedStatus: string;
  recommendedCategory: string;
  recommendedOwnerUserId: string | null;
  riskLevel: string;
  confidenceScore: number;
  reasons: string[];
  missingInformation: string[];
  evidence: string[];
};

export function mapAiConfidenceBand(
  confidenceScore: number,
): "High" | "Medium" | "Low" {
  if (confidenceScore >= 0.8) {
    return "High";
  }
  if (confidenceScore >= 0.55) {
    return "Medium";
  }
  return "Low";
}
