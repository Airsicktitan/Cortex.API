import type { ArchiveConfiguration } from "../types/archiveConfiguration";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

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
