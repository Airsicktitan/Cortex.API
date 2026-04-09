import type { ScheduledJob, UpsertScheduledJobInput } from "../types/scheduledJob";
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
    // Ignore unreadable response bodies and use the fallback.
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

export const scheduledJobService = {
  async getAll(token: string): Promise<ScheduledJob[]> {
    const response = await fetch(`${API_BASE_URL}/jobs`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load jobs");
    return response.json();
  },

  async create(
    job: UpsertScheduledJobInput,
    token: string,
  ): Promise<ScheduledJob> {
    const response = await fetch(`${API_BASE_URL}/jobs`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(job),
    });

    await ensureSuccess(response, "Failed to create job");
    return response.json();
  },

  async update(
    id: number,
    job: UpsertScheduledJobInput,
    token: string,
  ): Promise<ScheduledJob> {
    const response = await fetch(`${API_BASE_URL}/jobs/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(job),
    });

    await ensureSuccess(response, "Failed to update job");
    return response.json();
  },

  async runNow(id: number, token: string): Promise<ScheduledJob> {
    const response = await fetch(`${API_BASE_URL}/jobs/${id}/run`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to run job");
    return response.json();
  },
};
