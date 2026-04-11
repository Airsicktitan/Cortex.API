import type { NotificationFeed, UserNotification } from "../types/notification";
import { ApiError } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
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

      const title = "title" in data ? data.title : undefined;
      if (typeof title === "string" && title.trim()) {
        return title;
      }
    }
  } catch {
    // Ignore unreadable response bodies and fall back to the caller-provided message.
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

export const notificationService = {
  async getFeed(token: string, take = 20): Promise<NotificationFeed> {
    const response = await fetch(`${API_BASE_URL}/notifications?take=${take}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to load notifications");
    return response.json();
  },

  async markRead(id: number, token: string): Promise<UserNotification> {
    const response = await fetch(`${API_BASE_URL}/notifications/${id}/read`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to mark notification as read");
    return response.json();
  },

  async markAllRead(token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/notifications/read-all`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to mark notifications as read");
  },
};
