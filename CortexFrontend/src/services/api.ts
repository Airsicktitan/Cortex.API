import type { CreateTicketInput, Ticket, TicketMutationInput } from "../types/ticket";
import type { ArchivedTicket } from "../types/archivedTicket";
import type { TicketAttachment } from "../types/attachment";
import type { TicketAuditEntry } from "../types/ticketAudit";
import type {
  AdminUpdateUserInput,
  CreateUserInput,
  OnlineUser,
  UpdateUserAccessInput,
  UpdateUserProfileInput,
  UserAccessUpdateResult,
  UserProfile,
  UserRecord,
} from "../types/user";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

/** User-visible copy only — use with `ensureSuccess` / `getUserFacingErrorMessage`. */
export const API_USER_MESSAGES = {
  generic: "Something went wrong. Please try again.",
  loadTickets: "Unable to load tickets",
  saveChanges: "Failed to save changes",
} as const;

export class ApiError extends Error {
  status: number;
  /** Parsed server body for diagnostics; never display to end users. */
  rawMessage?: string;

  constructor(message: string, status: number, rawMessage?: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.rawMessage = rawMessage;
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

/** Always throws with a caller-supplied, user-safe `userMessage`. Raw bodies are kept on `rawMessage` for dev logging only. */
export async function ensureSuccess(
  response: Response,
  userMessage: string,
): Promise<void> {
  if (response.ok) {
    return;
  }

  const parsed = await readErrorMessage(response, userMessage);
  const rawMessage =
    parsed !== userMessage && parsed.trim() ? parsed : undefined;

  if (import.meta.env.DEV && rawMessage) {
    console.warn("[API error]", response.status, userMessage, rawMessage);
  }

  throw new ApiError(userMessage, response.status, rawMessage);
}

export function isLikelyNetworkError(error: unknown): boolean {
  if (error instanceof ApiError) {
    return false;
  }

  if (error instanceof TypeError) {
    return true;
  }

  if (error instanceof Error) {
    const normalizedMessage = error.message.toLowerCase();
    return (
      normalizedMessage.includes("failed to fetch") ||
      normalizedMessage.includes("networkerror") ||
      normalizedMessage.includes("load failed")
    );
  }

  return false;
}

export function getUserFacingErrorMessage(
  error: unknown,
  fallback: string,
): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (isLikelyNetworkError(error)) {
    return "Unable to connect. Please try again.";
  }

  return fallback;
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

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
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

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
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

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json();
  },

  async getArchived(token: string): Promise<ArchivedTicket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/archived`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch archived tickets");
    return response.json();
  },

  async getHistory(id: string, token: string): Promise<TicketAuditEntry[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/history`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch ticket history");
    return response.json();
  },

  // Create ticket
  async create(
    ticket: CreateTicketInput,
    token: string,
  ): Promise<Ticket> {
    const { status: _ignoredStatus, ...createPayload } = ticket as CreateTicketInput & {
      status?: string;
    };

    const response = await fetch(`${API_BASE_URL}/tickets`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(createPayload),
    });

    await ensureSuccess(response, API_USER_MESSAGES.saveChanges);
    return response.json();
  },

  // Update ticket
  async update(
    id: string,
    ticket: TicketMutationInput,
    token: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(ticket),
    });

    await ensureSuccess(response, API_USER_MESSAGES.saveChanges);
    return response.json();
  },

  async archive(id: string, token: string): Promise<ArchivedTicket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/archive`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({}),
    });

    await ensureSuccess(response, "Failed to archive ticket");
    return response.json();
  },

  async archiveWithReason(
    id: string,
    changeReason: string | undefined,
    token: string,
  ): Promise<ArchivedTicket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/archive`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({
        changeReason: changeReason?.trim() || undefined,
      }),
    });

    await ensureSuccess(response, "Failed to archive ticket");
    return response.json();
  },

  async reactivateArchived(id: string, token: string): Promise<Ticket> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/archived/${id}/reactivate`,
      {
        method: "POST",
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Failed to reactivate archived ticket");
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

  async getOnlineUsers(token: string): Promise<OnlineUser[]> {
    const response = await fetch(`${API_BASE_URL}/users/online`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to fetch online users");
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

  async updatePresence(token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/users/me/presence`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to update presence");
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

  async updateUserAccess(
    id: number,
    access: UpdateUserAccessInput,
    token: string,
  ): Promise<UserAccessUpdateResult> {
    const response = await fetch(`${API_BASE_URL}/users/${id}/access`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(access),
    });

    await ensureSuccess(response, "Failed to update user access");
    return response.json();
  },

  async createUser(user: CreateUserInput, token: string): Promise<UserRecord> {
    const response = await fetch(`${API_BASE_URL}/users`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(user),
    });

    await ensureSuccess(response, "Failed to create user");
    return response.json();
  },

  async deleteUser(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: "DELETE",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to delete user");
  },
};
