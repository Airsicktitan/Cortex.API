import type {
  CortexAutonomySettings,
  CortexAutonomySummary,
  UpdateCortexAutonomySettingsInput,
} from "../types/cortexAutonomy";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, json = false): HeadersInit => ({
  Authorization: `Bearer ${token}`,
  ...(json ? { "Content-Type": "application/json" } : {}),
});

export const systemAutonomyService = {
  async getSummary(
    token: string,
    signal?: AbortSignal,
  ): Promise<CortexAutonomySummary> {
    const response = await fetch(`${API_BASE_URL}/system/autonomy/summary`, {
      headers: authHeaders(token),
      signal,
    });
    await ensureSuccess(response, "Unable to load Cortex autonomy summary");
    return response.json() as Promise<CortexAutonomySummary>;
  },

  async updateConfig(
    input: UpdateCortexAutonomySettingsInput,
    token: string,
  ): Promise<CortexAutonomySettings> {
    const response = await fetch(`${API_BASE_URL}/system/autonomy/config`, {
      method: "PATCH",
      headers: authHeaders(token, true),
      body: JSON.stringify(input),
    });
    await ensureSuccess(response, "Unable to update Cortex autonomy config");
    return response.json() as Promise<CortexAutonomySettings>;
  },
};
