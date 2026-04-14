import type { SlaConfiguration } from "../types/sla";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const slaService = {
  async getAll(token: string): Promise<SlaConfiguration[]> {
    const response = await fetch(`${API_BASE_URL}/settings/sla`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load SLA configuration");
    return response.json();
  },

  async update(
    policies: SlaConfiguration[],
    token: string,
  ): Promise<SlaConfiguration[]> {
    const response = await fetch(`${API_BASE_URL}/settings/sla`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify({ policies }),
    });

    await ensureSuccess(response, "Failed to save SLA configuration");
    return response.json();
  },
};
