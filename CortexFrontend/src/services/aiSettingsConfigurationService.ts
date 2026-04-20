import type { AiSettingsConfiguration } from "../types/aiSettings";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const aiSettingsConfigurationService = {
  async get(token: string): Promise<AiSettingsConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/ai`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load AI settings");
    return response.json();
  },

  async update(
    configuration: AiSettingsConfiguration,
    token: string,
  ): Promise<AiSettingsConfiguration> {
    const response = await fetch(`${API_BASE_URL}/settings/ai`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(configuration),
    });

    await ensureSuccess(response, "Failed to save AI settings");
    return response.json();
  },
};
