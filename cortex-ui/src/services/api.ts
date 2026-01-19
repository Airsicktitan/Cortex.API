import type { Ticket } from "../types/ticket";

const API_BASE_URL = "http://localhost:5000/api";

export const ticketService = {
  // Get all tickets
  async getAll(): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets`);
    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Get ticket by ID
  async getById(id: string): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`);
    if (!response.ok) throw new Error("Failed to fetch ticket");
    return response.json();
  },

  // Get tickets by status
  async getByStatus(status: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/status/${status}`);
    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Get tickets by priority
  async getByPriority(priority: string): Promise<Ticket[]> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/priority/${priority}`,
    );
    if (!response.ok) throw new Error("Failed to fetch tickets");
    return response.json();
  },

  // Create ticket
  async create(
    ticket: Omit<Ticket, "id" | "createdDate" | "createdBy">,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(ticket),
    });
    if (!response.ok) throw new Error("Failed to create ticket");
    return response.json();
  },

  // Update ticket
  async update(id: string, ticket: Partial<Ticket>): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(ticket),
    });
    if (!response.ok) throw new Error("Failed to update ticket");
    return response.json();
  },
};
