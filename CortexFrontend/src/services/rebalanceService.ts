import type { RebalanceOverviewResponse } from "../types/rebalance";
import type {
  ExecuteRebalanceResponse,
  RebalanceSuggestion,
} from "../types/cortexDecision";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  Authorization: `Bearer ${token}`,
});

/**
 * Client for the Operational Rebalance layer (v1).
 * Backed by GET /api/rebalance/overview which itself composes
 * OwnerWorkloadScoringService + OperationalRiskService + ReassignmentRecommendationService.
 */
export const rebalanceService = {
  async getOverview(token: string): Promise<RebalanceOverviewResponse> {
    const response = await fetch(`${API_BASE_URL}/rebalance/overview`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load workload optimization overview");
    return response.json() as Promise<RebalanceOverviewResponse>;
  },

  async executeRebalance(
    token: string,
    suggestions?: RebalanceSuggestion[],
    options?: { confirmedManualOverrideTicketIds?: string[]; dryRun?: boolean },
  ): Promise<ExecuteRebalanceResponse> {
    const response = await fetch(`${API_BASE_URL}/rebalance/execute`, {
      method: "POST",
      headers: {
        ...authHeaders(token),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        suggestions: suggestions ?? [],
        confirmedManualOverrideTicketIds:
          options?.confirmedManualOverrideTicketIds ?? [],
        dryRun: options?.dryRun === true,
      }),
    });

    await ensureSuccess(response, "Unable to execute workload optimization");
    return response.json() as Promise<ExecuteRebalanceResponse>;
  },
};
