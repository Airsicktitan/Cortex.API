import type { NotificationChannelConfiguration } from "../types/notificationChannelConfiguration";
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
    // Ignore unreadable bodies and fall back to the caller-provided message.
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

export const notificationChannelConfigurationService = {
  async get(token: string): Promise<NotificationChannelConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/notification-channels`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load notification channel settings");
    return response.json();
  },

  async update(
    configuration: NotificationChannelConfiguration,
    token: string,
  ): Promise<NotificationChannelConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/notification-channels`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(configuration),
    });

    await ensureSuccess(response, "Failed to save notification channel settings");
    return response.json();
  },
};
