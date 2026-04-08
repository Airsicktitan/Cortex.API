import type { Ticket } from "../types/ticket";
import type { ArchivedTicket } from "../types/archivedTicket";
import type { TicketAttachment } from "../types/attachment";
import type {
  AdminUpdateUserInput,
  UpdateUserProfileInput,
  UserProfile,
  UserRecord,
} from "../types/user";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function readErrorMessage(response: Response, fallbackMessage: string) {
  try {
    const data = (await response.json()) as unknown;

    if (typeof data === "string" && data.trim()) {
      return data;
    }

    if (typeof data === "object" && data !== null) {
      const message = "message" in data ? data.message : undefined;
      if (typeof message === "string" && message.trim()) {
        return message;
      }

      const title = "title" in data ? data.title : undefined;
      if (typeof title === "string" && title.trim()) {
        return title;
      }
    }
  } catch {
    // Ignore unreadable response bodies and fall back to the provided message.
  }

  return fallbackMessage;
}

async function ensureSuccess(response: Response, fallbackMessage: string) {
  if (response.ok) return;

  const message = await readErrorMessage(response, fallbackMessage);
  throw new ApiError(message, response.status);
}

export const ticketService = {
  // Get User
  async getCurrentUser(token: string) {
    const response = await fetch(`${API_BASE_URL}/users/me`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch current user");
    return response.json();
  },

  // Get all tickets
  async getAll(token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch tickets");
    return response.json();
  },

  // Get ticket by ID
  async getById(id: string, token: string): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch ticket");
    return response.json();
  },

  // Get tickets by status
  async getByStatus(status: string, token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/status/${status}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch tickets");
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

    await ensureSuccess(response, "Failed to fetch tickets");
    return response.json();
  },

  async getArchived(token: string): Promise<ArchivedTicket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/archived`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch archived tickets");
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

    await ensureSuccess(response, "Failed to create ticket");
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

    await ensureSuccess(response, "Failed to update ticket");
    return response.json();
  },

  async archive(id: string, token: string): Promise<ArchivedTicket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/archive`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to archive ticket");
    return response.json();
  },

  // Delete ticket
  async delete(id: string, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "DELETE",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to delete ticket");
  },
};

export const attachmentService = {
  async getByTicket(ticketId: string, token: string): Promise<TicketAttachment[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/${ticketId}/attachments`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch attachments");
    return response.json();
  },

  async upload(
    ticketId: string,
    files: File[],
    token: string,
  ): Promise<TicketAttachment[]> {
    const formData = new FormData();
    files.forEach((file) => formData.append("files", file));

    const response = await fetch(`${API_BASE_URL}/tickets/${ticketId}/attachments`, {
      method: "POST",
      headers: authHeaders(token),
      body: formData,
    });

    await ensureSuccess(response, "Failed to upload attachments");
    return response.json();
  },

  async download(ticketId: string, attachmentId: number, token: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${ticketId}/attachments/${attachmentId}/download`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Failed to download attachment");
    return response.blob();
  },
};

export const userService = {
  async getCurrentUser(token: string): Promise<UserProfile> {
    const response = await fetch(`${API_BASE_URL}/users/me`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch current user");
    return response.json();
  },

  async getAll(token: string): Promise<UserRecord[]> {
    const response = await fetch(`${API_BASE_URL}/users`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch users");
    return response.json();
  },

  async updateProfile(
    profile: UpdateUserProfileInput,
    token: string,
  ): Promise<UserProfile> {
    const response = await fetch(`${API_BASE_URL}/users/profile`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(profile),
    });

    await ensureSuccess(response, "Failed to update profile");
    return response.json();
  },

  async updateUser(
    id: number,
    user: AdminUpdateUserInput,
    token: string,
  ): Promise<UserRecord> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(user),
    });

    await ensureSuccess(response, "Failed to update user");
    return response.json();
  },
};
