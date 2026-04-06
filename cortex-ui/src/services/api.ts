import type { Ticket } from "../types/ticket";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

export const ticketService = {
  // Get User
  async getCurrentUser(token: string) {
    const response = await fetch(`${API_BASE_URL}/users/me`, {
      headers: authHeaders(token),
    });

    if (!response.ok) throw new Error("Failed to fetch current user");
    return response.json();
  },

  // Get all tickets
  async getAll(token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets`, {
      headers: authHeaders(token),
    });

    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Get ticket by ID
  async getById(id: string, token: string): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      headers: authHeaders(token),
    });

    if (!response.ok) throw new Error("Failed to fetch ticket");
    return response.json();
  },

  // Get tickets by status
  async getByStatus(status: string, token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/status/${status}`, {
      headers: authHeaders(token),
    });

    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Get tickets by priority
  async getByPriority(priority: string, token: string): Promise<Ticket[]> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/priority/${priority}`,
      {
        headers: authHeaders(token),
      },
    );

    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Create ticket
  async create(
    ticket: Omit<Ticket, "id" | "createdDate" | "createdBy">,
    token: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(ticket),
    });

    if (!response.ok) throw new Error("Failed to create ticket");
    return response.json();
  },

  // Update ticket
  async update(
    id: string,
    ticket: Partial<Ticket>,
    token: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(ticket),
    });

    if (!response.ok) throw new Error("Failed to update ticket");
    return response.json();
  },

  // Delete ticket
  async delete(id: string, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "DELETE",
      headers: authHeaders(token),
    });

    if (!response.ok) throw new Error("Failed to delete ticket");
  },
};
