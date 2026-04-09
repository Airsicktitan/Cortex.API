import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import { ApiError } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

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
    }
  } catch {
    // Ignore unreadable bodies and use the caller-provided fallback.
  }

  return fallbackMessage;
}

async function ensureSuccess(response: Response, fallbackMessage: string) {
  if (response.ok) {
    return;
  }

  const message = await readErrorMessage(response, fallbackMessage);
  throw new ApiError(message, response.status);
}

export const archiveConfigurationService = {
  async getAll(token: string): Promise<ArchiveConfiguration[]> {
    const response = await fetch(`${API_BASE_URL}/settings/archive`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load archive configuration");
    return response.json();
  },

  async create(
    configuration: ArchiveConfiguration,
    token: string,
  ): Promise<ArchiveConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/archive`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(configuration),
    });

    await ensureSuccess(response, "Failed to create archive configuration");
    return response.json();
  },

  async update(
    id: number,
    configuration: ArchiveConfiguration,
    token: string,
  ): Promise<ArchiveConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/archive/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(configuration),
    });

    await ensureSuccess(response, "Failed to save archive configuration");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/settings/archive/${id}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to delete archive configuration");
  },

  async runNow(token: string): Promise<{ archivedTicketCount: number }> {
    const response = await fetch(`${API_BASE_URL}/settings/archive/run`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to archive eligible tickets");
    return response.json();
  },
};
