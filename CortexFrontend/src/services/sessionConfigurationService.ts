import type { SessionConfiguration } from "../types/sessionConfiguration";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const sessionConfigurationService = {
  async get(token: string): Promise<SessionConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/session`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load session configuration");
    return response.json();
  },

  async update(
    configuration: SessionConfiguration,
    token: string,
  ): Promise<SessionConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/session`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(configuration),
    });

    await ensureSuccess(response, "Failed to save session configuration");
    return response.json();
  },
};
