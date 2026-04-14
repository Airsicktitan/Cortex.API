import type {
  TicketStatusDefinition,
  UpsertTicketStatusDefinitionInput,
} from "../types/ticketStatus";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const ticketStatusService = {
  async getAll(token: string): Promise<TicketStatusDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/ticket-statuses`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load ticket statuses");
    return response.json();
  },

  async create(
    definition: UpsertTicketStatusDefinitionInput,
    token: string,
  ): Promise<TicketStatusDefinition> {
    const response = await fetch(`${API_BASE_URL}/ticket-statuses`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to create ticket status");
    return response.json();
  },

  async update(
    id: number,
    definition: UpsertTicketStatusDefinitionInput,
    token: string,
  ): Promise<TicketStatusDefinition> {
    const response = await fetch(`${API_BASE_URL}/ticket-statuses/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to update ticket status");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/ticket-statuses/${id}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to delete ticket status");
  },
};
