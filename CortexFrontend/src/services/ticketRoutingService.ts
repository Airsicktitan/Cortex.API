import type {
  TicketRoutingRule,
  UpsertTicketRoutingRuleInput,
} from "../types/ticketRouting";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const ticketRoutingService = {
  async getAll(token: string): Promise<TicketRoutingRule[]> {
    const response = await fetch(`${API_BASE_URL}/settings/ticket-routing`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load ticket routing rules");
    return response.json();
  },

  async create(
    definition: UpsertTicketRoutingRuleInput,
    token: string,
  ): Promise<TicketRoutingRule> {
    const response = await fetch(`${API_BASE_URL}/settings/ticket-routing`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to create ticket routing rule");
    return response.json();
  },

  async update(
    id: number,
    definition: UpsertTicketRoutingRuleInput,
    token: string,
  ): Promise<TicketRoutingRule> {
    const response = await fetch(`${API_BASE_URL}/settings/ticket-routing/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to save ticket routing rule");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/settings/ticket-routing/${id}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to delete ticket routing rule");
  },
};
