import type { Comment } from "../types/comment";
import { ApiError } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

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

export const commentService = {
  async getByTicket(ticketId: string, token: string): Promise<Comment[]> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(res, "Failed to load comments");
    return res.json();
  },

  async create(
    ticketId: string,
    body: string,
    token: string,
  ): Promise<Comment> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ body }),
    });

    await ensureSuccess(res, "Failed to create comment");
    return res.json();
  },
};
