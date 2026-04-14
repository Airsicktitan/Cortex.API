import type { NotificationChannelConfiguration } from "../types/notificationChannelConfiguration";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

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
