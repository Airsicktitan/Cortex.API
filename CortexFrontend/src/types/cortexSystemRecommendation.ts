export interface CortexSystemRecommendation {
  id: string;
  type: "RoutingRule" | string;
  sourceType: string;
  sourceId: string;
  title: string;
  description: string;
  recommendation: string;
  confidence: "Low" | "Medium" | "High" | string;
  severity: "Low" | "Medium" | "High" | string;
  status: "Open" | "Accepted" | "Dismissed" | "Deferred" | string;
  actionLabel: string;
  actionPreview: string;
  dismissedReason?: string | null;
  generatedAtUtc: string;
  supportingFacts: string[];
}
